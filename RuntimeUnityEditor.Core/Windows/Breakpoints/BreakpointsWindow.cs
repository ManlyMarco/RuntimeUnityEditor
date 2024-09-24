using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Breakpoints
{
    // TODO aggregate results, etc.
    public class BreakpointsWindow : Window<BreakpointsWindow>
    {
        #region Functionality

        private static readonly Harmony _harmony = new Harmony("RuntimeUnityEditor.Core.Breakpoints");
        private static readonly Dictionary<MethodBase, PatchInfo> _appliedPatches = new Dictionary<MethodBase, PatchInfo>();
        private static readonly HarmonyMethod _handlerMethodRet = new HarmonyMethod(typeof(BreakpointsWindow), nameof(BreakpointHandlerReturn));
        private static readonly HarmonyMethod _handlerMethodNoRet = new HarmonyMethod(typeof(BreakpointsWindow), nameof(BreakpointHandlerNoReturn));

        private static readonly List<BrekapointHit> _hits = new List<BrekapointHit>();

        private sealed class PatchInfo
        {
            public MethodBase Target { get; }
            public MethodInfo Patch { get; }
            public List<object> InstanceFilters { get; } = new List<object>();

            public PatchInfo(MethodBase target, MethodInfo patch, object instanceFilter)
            {
                Target = target;
                Patch = patch;
                if (instanceFilter != null)
                    InstanceFilters.Add(instanceFilter);
            }
        }

        private sealed class BrekapointHit
        {
            public BrekapointHit(PatchInfo origin, object instance, object[] args, object result, StackTrace trace)
            {
                Origin = origin;
                Instance = instance;
                Args = args;
                Result = result;
                Trace = trace;
                TraceString = trace.ToString();
                Time = DateTime.UtcNow;
            }

            public readonly PatchInfo Origin;
            public readonly object Instance;
            public readonly object[] Args;
            public readonly object Result;
            public readonly StackTrace Trace;
            internal readonly string TraceString;
            public readonly DateTime Time;
        }

        public static bool AttachBreakpoint(MethodBase target, object instance)
        {
            if (_appliedPatches.TryGetValue(target, out var pi))
            {
                if (instance != null)
                    pi.InstanceFilters.Add(instance);
                else
                    pi.InstanceFilters.Clear();
                return true;
            }

            var hasReturn = target is MethodInfo mi && mi.ReturnType != typeof(void);
            var patch = _harmony.Patch(target, postfix: hasReturn ? _handlerMethodRet : _handlerMethodNoRet);
            if (patch != null)
            {
                _appliedPatches[target] = new PatchInfo(target, patch, instance);
                return true;
            }

            return false;
        }

        public static bool DetachBreakpoint(MethodBase target, object instance)
        {
            if (_appliedPatches.TryGetValue(target, out var pi))
            {
                if (instance == null)
                    pi.InstanceFilters.Clear();
                else
                    pi.InstanceFilters.Remove(instance);

                if (pi.InstanceFilters.Count == 0)
                {
                    _harmony.Unpatch(target, pi.Patch);
                    _appliedPatches.Remove(target);
                    return true;
                }
            }

            return false;
        }

        private static void BreakpointHandlerReturn(object __instance, MethodBase __originalMethod, object[] __args, object __result)
        {
            AddHit(__instance, __originalMethod, __args, __result);
        }
        private static void BreakpointHandlerNoReturn(object __instance, MethodBase __originalMethod, object[] __args)
        {
            AddHit(__instance, __originalMethod, __args, null);
        }
        private static void AddHit(object __instance, MethodBase __originalMethod, object[] __args, object __result)
        {
            if (_appliedPatches.TryGetValue(__originalMethod, out var pi))
            {
                if (pi.InstanceFilters.Count == 0 || pi.InstanceFilters.Contains(__instance))
                    _hits.Add(new BrekapointHit(pi, __instance, __args, __result, new StackTrace(1, true)));
            }
        }

        public static bool IsAttached(MethodBase target, object instance)
        {
            if (_appliedPatches.TryGetValue(target, out var pi))
            {
                return instance == null && pi.InstanceFilters.Count == 0 || pi.InstanceFilters.Contains(instance);
            }
            return false;
        }

        #endregion

        #region UI

        protected override void Initialize(InitSettings initSettings)
        {
            DisplayName = "Breakpoints";
            Title = "Breakpoint manager and breakpoint hit history";
            DefaultScreenPosition = ScreenPartition.CenterLower;
        }

        protected override void LateUpdate()
        {
            if (_hits.Count > _maxHitsToKeep)
                _hits.RemoveRange(0, _hits.Count - _maxHitsToKeep);

            base.LateUpdate();
        }

        private Vector2 _scrollPosHits, _scrollPosBreakpoints;

        private bool _showingHits = true;
        private int _maxHitsToKeep = 100;

        protected override void DrawContents()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                if (GUILayout.Toggle(!_showingHits, "Show active breakpoints"))
                    _showingHits = false;
                if (GUILayout.Toggle(_showingHits, "Show breakpoint hits"))
                    _showingHits = true;
                GUILayout.Space(10);
                if (GUILayout.Button("Remove all"))
                {
                    _harmony.UnpatchSelf();
                    _appliedPatches.Clear();
                }
                if (GUILayout.Button("Clear hits"))
                {
                    _hits.Clear();
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

            if (_showingHits)
                DrawHits();
            else
                DrawBreakpoints();
        }

        private void DrawBreakpoints()
        {
            _scrollPosBreakpoints = GUILayout.BeginScrollView(_scrollPosBreakpoints, false, true);
            {
                if (_appliedPatches.Count > 0)
                {
                    foreach (var appliedPatch in _appliedPatches)
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box);
                        {
                            DrawHitOriginButton(appliedPatch.Value);

                            if (GUILayout.Button("Remove breakpoint", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                                DetachBreakpoint(appliedPatch.Key, null);

                            if (appliedPatch.Value.InstanceFilters.Count > 0)
                            {
                                GUILayout.Label("or remove watched instances:", IMGUIUtils.LayoutOptionsExpandWidthFalse);

                                var instanceFilters = appliedPatch.Value.InstanceFilters;
                                for (var i = 0; i < instanceFilters.Count; i++)
                                {
                                    var obj = instanceFilters[i];
                                    if (GUILayout.Button(obj.ToString(), GUILayout.Width(80)))
                                        DetachBreakpoint(appliedPatch.Key, obj);
                                }
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                else
                {
                    GUILayout.Label("This window lists breakpoints set on methods and on property getters/setters.\nA breakpoint is tiggered whenever a method/property is called. The stack trace as well as any parameters and result value of that trigger are then stored in the list of hits.\n\nTo add breakpoints right click on methods and properties, and look for the 'Attach breakpoint' options. It's easiest to do in inspector by right clicking on the member names.");
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawHits()
        {
            _scrollPosHits = GUILayout.BeginScrollView(_scrollPosHits, false, true);
            {
                if (_hits.Count > 0)
                {
                    for (int i = _hits.Count - 1; i >= 0; i--)
                    {
                        var hit = _hits[i];

                        GUILayout.BeginHorizontal(GUI.skin.box);
                        {
                            GUILayout.Label($"{hit.Time:HH:mm:ss.fff}", GUILayout.Width(85));

                            DrawHitOriginButton(hit.Origin);

                            if (GUILayout.Button(new GUIContent("Trace", null, hit.TraceString + "\n\nClick to copy to clipboard\nMiddle click to inspect\nRight click for more options"), GUI.skin.label, GUILayout.Width(60)))
                            {
                                if (IMGUIUtils.IsMouseRightClick())
                                    ContextMenu.Instance.Show(hit.Trace, null);
                                else if (IMGUIUtils.IsMouseWheelClick())
                                    Inspector.Inspector.Instance.Push(new InstanceStackEntry(hit.Trace.GetFrames(), "StackTrace"), true);
                                else
                                {
                                    UnityFeatureHelper.systemCopyBuffer = hit.TraceString;
                                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, "Copied stack trace to clipboard");
                                }
                            }

                            ShowObjectButton(hit.Instance, "Call instance", GUILayout.Width(80));
                            ShowObjectButton(hit.Result, "Method return value", GUILayout.Width(80));

                            //GUILayout.Label(hit.Args.Length.ToString() + ":", GUILayout.Width(40));

                            for (int j = 0; j < hit.Args.Length; j++)
                            {
                                ShowObjectButton(hit.Args[j], "Method argument #" + j, GUILayout.Width(80));
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                else
                {
                    GUILayout.Label("This window lists the last " + _maxHitsToKeep + " breakpoint hits that were caught (in IL2CPP some purely native calls can be missed and stacktraces may be crap).\nA breakpoint is tiggered whenever a method/property is called. The stack trace as well as any parameters and result value of that trigger are then stored in the list of hits.\n\nTo add breakpoints right click on methods and properties, and look for the 'Attach breakpoint' options. It's easiest to do in inspector by right clicking on the member names.");
                }
            }
            GUILayout.EndScrollView();
        }

        private static void DrawHitOriginButton(PatchInfo hitOrigin)
        {
            if (GUILayout.Button(new GUIContent(hitOrigin.Target.Name, null, $"Target: {hitOrigin.Target.FullDescription()}\n\nClick to open in dnSpy, right click for more options."), GUI.skin.label, GUILayout.Width(150)))
            {
                if (IMGUIUtils.IsMouseRightClick())
                    ContextMenu.Instance.Show(null, hitOrigin.Target);
                else
                    DnSpyHelper.OpenInDnSpy(hitOrigin.Target);
            }
        }

        private static void ShowObjectButton(object obj, string objName, params GUILayoutOption[] options)
        {
            var text = obj?.ToString() ?? "NULL";
            if (GUILayout.Button(new GUIContent(text, null, $"Name: {objName}\nType: {obj?.GetType().FullDescription() ?? "NULL"}\nToString: {text}\n\nClick to open in inspector, right click for more options."), GUI.skin.label, options) && obj != null)
            {
                if (IMGUIUtils.IsMouseRightClick())
                    ContextMenu.Instance.Show(obj, null);
                else
                    Inspector.Inspector.Instance.Push(new InstanceStackEntry(obj, objName), true);
            }
        }

        #endregion
    }
}