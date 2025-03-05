using System.Collections.Generic;
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
        private static readonly List<BreakpointHit> _hits = new List<BreakpointHit>();

        protected override void Initialize(InitSettings initSettings)
        {
            DisplayName = "Breakpoints";
            Title = "Breakpoint manager and breakpoint hit history";
            DefaultScreenPosition = ScreenPartition.CenterLower;

            Breakpoints.OnBreakpointHit += hit => _hits.Add(hit);
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
        private string _searchString = "";

        protected override void DrawContents()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    Breakpoints.Enabled = GUILayout.Toggle(Breakpoints.Enabled, "Enabled", IMGUIUtils.LayoutOptionsExpandWidthFalse);

                    if (!Breakpoints.Enabled)
                        GUI.color = Color.gray;

                    GUILayout.Space(10);

                    GUILayout.Label("Show ", IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    if (GUILayout.Toggle(!_showingHits, "active breakpoints", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                        _showingHits = false;
                    if (GUILayout.Toggle(_showingHits, "breakpoint hits", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                        _showingHits = true;

                    GUILayout.Space(10);

                    if (_showingHits)
                    {
                        if (GUILayout.Button("Clear hits", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                            _hits.Clear();
                    }
                    else
                    {
                        if (GUILayout.Button("Remove all", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                            Breakpoints.DetachAll();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    GUILayout.Label("Search: ", IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    _searchString = GUILayout.TextField(_searchString, IMGUIUtils.LayoutOptionsExpandWidthTrue);

                    GUILayout.Space(10);

                    GUILayout.Label("Break attached debugger: ", IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    if (GUILayout.Toggle(Breakpoints.DebuggerBreaking == DebuggerBreakType.None, "No", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                        Breakpoints.DebuggerBreaking = DebuggerBreakType.None;
                    if (GUILayout.Toggle(Breakpoints.DebuggerBreaking == DebuggerBreakType.ThrowCatch, new GUIContent("Throw an exception", null, $"Throw and catch a {nameof(BreakpointHitException)}. The most reliable way."), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                        Breakpoints.DebuggerBreaking = DebuggerBreakType.ThrowCatch;
                    if (GUILayout.Toggle(Breakpoints.DebuggerBreaking == DebuggerBreakType.DebuggerBreak, new GUIContent("Debugger.Break", null, "This might not work with some debugging methods, and it might hard-crash some games."), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                        Breakpoints.DebuggerBreaking = DebuggerBreakType.DebuggerBreak;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndHorizontal();

            if (_showingHits)
                DrawHits();
            else
                DrawBreakpoints();

            GUI.color = Color.white;
        }

        private void DrawBreakpoints()
        {
            _scrollPosBreakpoints = GUILayout.BeginScrollView(_scrollPosBreakpoints, false, true);
            {
                if (Breakpoints.AppliedPatches.Count > 0)
                {
                    foreach (var appliedPatch in Breakpoints.AppliedPatches)
                    {
                        if (!string.IsNullOrEmpty(_searchString))
                        {
                            if (!appliedPatch.GetSearchableString().Contains(_searchString))
                                continue;
                        }

                        GUILayout.BeginHorizontal(GUI.skin.box);
                        {
                            DrawHitOriginButton(appliedPatch);

                            if (GUILayout.Button("Remove breakpoint", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                                Breakpoints.DetachBreakpoint(appliedPatch.Target, null);

                            if (appliedPatch.InstanceFilters.Count > 0)
                            {
                                GUILayout.Label("or remove watched instances:", IMGUIUtils.LayoutOptionsExpandWidthFalse);

                                var instanceFilters = appliedPatch.InstanceFilters;
                                for (var i = 0; i < instanceFilters.Count; i++)
                                {
                                    var obj = instanceFilters[i];
                                    if (GUILayout.Button(obj.ToString(), GUILayout.Width(80)))
                                        Breakpoints.DetachBreakpoint(appliedPatch.Target, obj);
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

                        if (!string.IsNullOrEmpty(_searchString))
                        {
                            if (!hit.GetSearchableString().Contains(_searchString))
                                continue;
                        }

                        GUILayout.BeginHorizontal(GUI.skin.box);
                        {
                            GUILayout.Label($"{hit.Time:HH:mm:ss.fff}", GUILayout.Width(85));

                            DrawHitOriginButton(hit.Origin);

                            if (GUILayout.Button(new GUIContent("Trace", null, hit.TraceString + "\n\nClick to copy to clipboard\nMiddle click to inspect\nRight click for more options"), GUI.skin.label, GUILayout.Width(60)))
                            {
                                if (IMGUIUtils.IsMouseRightClick())
                                    ContextMenu.Instance.Show(hit.Trace);
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

        private static void DrawHitOriginButton(BreakpointPatchInfo hitOrigin)
        {
            if (GUILayout.Button(new GUIContent(hitOrigin.Target.Name, null, $"Target: {hitOrigin.Target.FullDescription()}\n\nClick to open in dnSpy, right click for more options."), GUI.skin.label, GUILayout.Width(150)))
            {
                if (IMGUIUtils.IsMouseRightClick())
                    ContextMenu.Instance.Show(hitOrigin.Target);
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
                    ContextMenu.Instance.Show(obj);
                else
                    Inspector.Inspector.Instance.Push(new InstanceStackEntry(obj, objName), true);
            }
        }
    }
}