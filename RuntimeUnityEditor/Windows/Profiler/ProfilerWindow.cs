using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;
#pragma warning disable CS1591

namespace RuntimeUnityEditor.Core.Profiler
{
    /// <summary>
    /// Simple profiler that tracks common Unity messages like Update or OnGUI.
    /// </summary>
    public sealed class ProfilerWindow : Window<ProfilerWindow>
    {
        private const string OnGuiMethodName = "OnGUI";
        private static readonly string[] _orderingStrings = { "#", "Time", "Memory", "Name" };
        private static readonly GUILayoutOption[] _cGcW = { GUILayout.MinWidth(50), GUILayout.MaxWidth(50) };
        private static readonly GUILayoutOption[] _cOrderW = { GUILayout.MinWidth(30), GUILayout.MaxWidth(30) };
        private static readonly GUILayoutOption[] _cRanHeaderW = { GUILayout.MinWidth(43), GUILayout.MaxWidth(43) };
        private static readonly GUILayoutOption[] _cRanW2 = { GUILayout.MinWidth(25), GUILayout.MaxWidth(25) };
        private static readonly GUILayoutOption[] _cTicksW = { GUILayout.MinWidth(50), GUILayout.MaxWidth(50) };
        private static readonly GUILayoutOption[] _expandW = { GUILayout.ExpandWidth(true) };
        private static readonly GUILayoutOption[] _expandWno = { GUILayout.ExpandWidth(false) };
        private static readonly GUIContent _cColOrder = new GUIContent("#", "Relative order of execution in a frame. Methods are called one by one on the main unity thread in this order.\n\nMethods that did not run during this frame are also included, so this number does not equal how many methods were called on this frame.");
        private static readonly GUIContent _cColRan = new GUIContent("Ran", "Left toggle indicates if this method was executed in this frame (all Harmony patches were called, and the original method was called if not disabled by a Harmony patch).\n\nRight toggle indicates if the original method was executed (original method being skipped is usually caused by a false postfix in a Harmony patch)");
        private static readonly GUIContent _cColTime = new GUIContent("Time", "Time spent executing this method (all Harmony patches included).\n\nBy default it's shown in ticks (smallest measurable unit of time). Resolution of ticks depends on Stopwatch.Frequency, but usually 10000 = 1ms.\n\nHigh values will drop FPS. If the value is much higher on some frames it can be felt as the game stuttering.\n\nIn methods running on every frame this should be as low as possible.");
        private static readonly GUIContent _cColMem = new GUIContent("Mem", "Bytes of memory allocated in the managed heap during this method's execution (all Harmony patches included). The value is approximate and might be inaccurate, especially if there is code running on background threads.\n\nHigh values (usually caused by constantly allocating and discarding objects, e.g. using linq queries) will trigger garbage collections, causing the game to randomly stutter. Magnitude of the stutters can be lowered by very fast CPUs and the incremental GC being enabled (only Unity 2019+).\n\nIn methods running on every frame this should be 0 (or as close to 0 as possible).");
        private static readonly GUIContent _cColName = new GUIContent("Full method name", "Name format:\nName of GameObject that the component running this method is attached to > Full name of the component and name of the method (OnGUI event type)");
        private static readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();

        private static readonly Dictionary<long, ProfilerInfo> _data = new Dictionary<long, ProfilerInfo>();
        private static readonly List<ProfilerInfo> _dataDisplay = new List<ProfilerInfo>();

        private static readonly Harmony _hi = new Harmony("rue-profiler");

        private static bool _fixed, _update, _late, _ongui;
        private static bool _pause, _hideInputEvents, _msTime, _aggregation;
        private static int _ordering;
        private static int _currentExecutionCount;
        private static bool _needResort;

        private static bool _searchCaseSensitive, _searchHighlightMode;
        private static string _searchText = "";

        private static Vector2 _scrollPos;
        private static int _singleObjectTreeItemHeight;

        protected override void Initialize(InitSettings initSettings)
        {
            Title = "Profiler";
            MinimumSize = new Vector2(570, 170);
            Enabled = false;
            DefaultScreenPosition = ScreenPartition.CenterUpper;

            RuntimeUnityEditorCore.PluginObject.StartCoroutine(FrameEndCo());
        }

        protected override void DrawContents()
        {
            //todo 
            // allow disabling the method by a false prefix (still appears on list but grayed out)
            // allow reordering execution order
            // ? track object destructions
            // ? track object instantiations

            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    _fixed = GUILayout.Toggle(_fixed, "FixedUpdate");
                    _update = GUILayout.Toggle(_update, "Update");
                    _late = GUILayout.Toggle(_late, "LateUpdate");
                    _ongui = GUILayout.Toggle(_ongui, OnGuiMethodName);
                    if (GUILayout.Button("Apply hooks")) Patch();
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    _pause = GUILayout.Toggle(_pause, "Pause");
                    _hideInputEvents = GUILayout.Toggle(_hideInputEvents, "Hide input events");
                    _msTime = GUILayout.Toggle(_msTime, "Time in ms");
                    _aggregation = GUILayout.Toggle(_aggregation, "Aggregation");
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginHorizontal(GUI.skin.box, _expandW);
                {
                    GUILayout.Label("Search: ", _expandWno);
                    _searchText = GUILayout.TextField(_searchText, _expandW);
                    if (GUILayout.Button("Clear", _expandWno)) _searchText = "";
                    _searchHighlightMode = GUILayout.Toggle(_searchHighlightMode, "Highlight mode");
                    _searchCaseSensitive = GUILayout.Toggle(_searchCaseSensitive, "Case sensitive");
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUI.skin.box, _expandWno);
                {
                    GUILayout.Label("Order: ");
                    GUI.changed = false;
                    _ordering = GUILayout.SelectionGrid(_ordering, _orderingStrings, 4, _expandWno);
                    if (GUI.changed) _needResort = true;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndHorizontal();

            var isSearching = _searchText.Length > 0;
            var searchCase = _searchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var anyShown = false;
            var dispNameColor = Color.white;
            var currentCount = 0;

            var origColor = GUI.color;

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Space(5);
                    GUILayout.Label(_cColOrder, _cOrderW);
                    GUILayout.Label(_cColRan, _cRanHeaderW);
                    GUILayout.Label(_cColTime, _cTicksW);
                    GUILayout.Label(_cColMem, _cGcW);
                    GUILayout.Label(_cColName, _expandW);
                }
                GUILayout.EndHorizontal();

                _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true);
                {
                    foreach (var pd in _dataDisplay)
                    {
                        if (_hideInputEvents && pd.GuiEvent >= 0 && pd.GuiEvent != EventType.Layout && pd.GuiEvent != EventType.Repaint)
                            continue;

                        if (isSearching)
                        {
                            var match = pd.DisplayName.Contains(_searchText, searchCase);
                            if (_searchHighlightMode)
                                dispNameColor = match ? Color.yellow : Color.white;
                            else if (!match)
                                continue;
                        }

                        anyShown = true;
                        currentCount++;

                        var needsHeightMeasure = _singleObjectTreeItemHeight == 0;

                        var isVisible = currentCount * _singleObjectTreeItemHeight >= _scrollPos.y &&
                                        (currentCount - 1) * _singleObjectTreeItemHeight <= _scrollPos.y + WindowRect.height - 50;

                        if (isVisible || needsHeightMeasure)
                        {
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Label(pd.HighestExecutionOrder.ToString(), _cOrderW); // # called order

                                var ran = pd.SinceLastRun < 2;
                                if (!ran && !pd.Owner) _needResort = true;

                                GUILayout.Toggle(ran, GUIContent.none); // enabled
                                GUILayout.Toggle(pd.OriginalRan, GUIContent.none, _cRanW2); // enabled

                                var ticks = pd.TicksSpent.GetAverage();
                                var ms = ConvertTicksToMs(ticks);
                                if (ms >= 0.2f) GUI.color = Color.red;
                                else if (ms >= 0.1f) GUI.color = Color.yellow;
                                GUILayout.Label(_msTime ? ms.ToString("F2") + "ms" : ticks.ToString(), _cTicksW);
                                GUI.color = origColor;

                                var bytes = pd.GcBytes.GetAverage();
                                if (bytes > 100) GUI.color = Color.red;
                                else if (bytes > 50) GUI.color = Color.yellow;
                                GUILayout.Label(bytes.ToString(), _cGcW);
                                GUI.color = origColor;

                                GUI.color = dispNameColor;
                                GUILayout.Label(pd.DisplayName, _expandW); //fullname
                                GUI.color = origColor;

                                GUILayout.FlexibleSpace();

                                ContextMenu.Instance.DrawContextButton(pd.Owner, pd.Method);
                                DnSpyHelper.DrawDnSpyButtonIfAvailable(pd.Method);
                            }
                            GUILayout.EndHorizontal();

                            if (needsHeightMeasure && Event.current.type == EventType.repaint)
                                _singleObjectTreeItemHeight = Mathf.CeilToInt(GUILayoutUtility.GetLastRect().height);
                        }
                        else
                            GUILayout.Space(_singleObjectTreeItemHeight);
                    }

                    GUI.color = Color.white;

                    if (!anyShown)
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(isSearching ? "Nothing was found, check your filters" : "No methods are being tracked, apply hooks to start profiling");
                            GUILayout.FlexibleSpace();
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.FlexibleSpace();
                    }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }

        private static void Patch()
        {
            _dataDisplay.Clear();
            _data.Clear();

            _hi.UnpatchSelf();

            var mbType = typeof(MonoBehaviour);

            var hits = AppDomain.CurrentDomain.GetAssemblies()
                //.Where(x => Array.FindIndex(x.GetReferencedAssemblies(), name => name.Name == "UnityEngine" || name.Name == "UnityEngine.CoreModule") >= 0) // Dont search assemblies that can't have MBs in them
                .SelectMany(x => x.GetTypesSafe())
                .Where(x => mbType.IsAssignableFrom(x))
                .Select(t =>
                {
                    if (t.ContainsGenericParameters)
                    {
                        try
                        {
                            var genericType = t.MakeGenericType(t.GetGenericArguments().Select(x => x.BaseType).ToArray());
                            //RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, $"[Profiler] Hooking in generic class {t.FullName} -> {genericType.FullName}");
                            return genericType;
                        }
                        catch (Exception e)
                        {
                            RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, $"[Profiler] Failed to hook in class {t.FullName} - {e.Message}");
                            return null;
                        }
                    }

                    return t;
                })
                .SelectMany(x => x?.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? new MethodBase[0])
                .Where(x =>
                {
                    var name = x.Name;
                    if (_fixed && name == "FixedUpdate") return true;
                    if (_late && name == "LateUpdate") return true;
                    if (_update && name == "Update") return true;
                    if (_ongui && name == OnGuiMethodName) return true;
                    return false;
                })
                .ToList();

            foreach (var hit in hits)
            {
                try
                {
                    _hi.Patch(original: hit,
                              prefix: new HarmonyMethod(typeof(ProfilerWindow), nameof(Prefix)) { priority = int.MaxValue },
                              postfix: new HarmonyMethod(typeof(ProfilerWindow), nameof(Postfix)) { priority = int.MinValue });
                }
                catch (Exception e)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, $"[Profiler] Failed to hook {hit.FullDescription()} - {e.Message}");
                }
            }
        }

        private IEnumerator FrameEndCo()
        {
            while (true)
            {
                yield return _waitForEndOfFrame;

                if (!Enabled) continue; //todo allow running in bg?

                _currentExecutionCount = 0;

                if (_ordering == 1 || _ordering == 2)
                    _needResort = true;

                if (!_pause)
                {
                    foreach (var info in _data.ToList())
                    {
                        if (info.Value.Owner == null)
                        {
                            _data.Remove(info.Key);
                            continue;
                        }

                        info.Value.SinceLastRun++;
                        if (_needResort) info.Value.HighestExecutionOrder = info.Value.ExecutionOrder;
                    }
                }

                if (_needResort)
                {
                    _dataDisplay.Clear();

                    IEnumerable<ProfilerInfo> infos = _data.Values;

                    if ( _aggregation )
                    {
                        infos = infos
                            .GroupBy(x => x.FullName)
                            .Select(group =>
                                group.Aggregate(new ProfilerInfo(group.First(), $"[{group.Count(),3}] {group.First().FullName}"),
                                (a, b) => ProfilerInfo.Add(a, b))
                                );
                    }

                    _dataDisplay.AddRange(infos.OrderBy<ProfilerInfo, object>(x =>
                        {
                            switch (_ordering)
                            {
                                case 0: return x.HighestExecutionOrder;
                                case 1: return -x.TicksSpent.GetAverage();
                                case 2: return -x.GcBytes.GetAverage();
                                case 3: return x.DisplayName;
                                default: throw new ArgumentOutOfRangeException("unknown ordering " + _ordering);
                            }
                        }));
                }

                _needResort = false;
            }
        }

        private static long GetKeyHash(MethodBase __originalMethod, MonoBehaviour __instance)
        {
            return __instance.GetHashCode() + ((long)__originalMethod.GetHashCode() << 32);
        }

        private static float ConvertTicksToMs(long ticks)
        {
            if (Stopwatch.IsHighResolution)
            {
                var tickFrequency = 10000000.0d / Stopwatch.Frequency;
                return (float)(ticks * tickFrequency / 10000L);
            }

            return ticks / 10000f;
        }

        private sealed class ProfilerInfo
        {
            public readonly string DisplayName;

            public readonly string FullName;

            public readonly MovingAverage GcBytes = new MovingAverage(800); // this needs A LOT of samples because the data is very coarse

            public readonly EventType GuiEvent;
            public readonly MethodBase Method;
            public readonly MonoBehaviour Owner;
            public readonly MovingAverage TicksSpent = new MovingAverage(55);

            public readonly Stopwatch Timer;

            private int _executionOrder;
            //public readonly string FullPath;

            public long GcBytesAtStart;

            internal int HighestExecutionOrder;
            public bool OriginalRan;

            public bool PostfixRan;

            internal byte SinceLastRun;

            public ProfilerInfo(MethodBase method, MonoBehaviour owner, EventType guiEvent = (EventType)(-1))
            {
                Method = method;
                Owner = owner;
                FullName = owner.GetType().FullDescription() + "::" + method.Name;
                if (guiEvent >= 0) FullName += $"({guiEvent})";
                //FullPath = owner.transform.GetFullTransfromPath();
                DisplayName = $"{owner.transform.name} > {FullName}";
                Timer = new Stopwatch();
                GuiEvent = guiEvent;
                _needResort = true;
            }

            public int ExecutionOrder
            {
                get => _executionOrder;
                set
                {
                    _executionOrder = value;
                    if (HighestExecutionOrder < value)
                    {
                        HighestExecutionOrder = value;
                        _needResort = true;
                    }
                }
            }

            // for aggregate
            public ProfilerInfo( ProfilerInfo parent, string fullName = null )
            {
                Method = parent.Method;
                Owner = null;
                FullName = fullName ?? parent.FullName;
                DisplayName = FullName;
                Timer = new Stopwatch();
                GuiEvent = parent.GuiEvent;
                ExecutionOrder = parent._executionOrder;
            }

            static public ProfilerInfo Add( ProfilerInfo x, ProfilerInfo y )
            {
                ProfilerInfo sum = new ProfilerInfo(x);
                sum.TicksSpent.Sample(x.TicksSpent.GetAverage() + y.TicksSpent.GetAverage());
                sum.GcBytes.Sample(x.GcBytes.GetAverage() + y.GcBytes.GetAverage());
                return sum;
            }
        }

        private static bool Prefix(MethodBase __originalMethod, MonoBehaviour __instance, ref ProfilerInfo __state)
        {
            if (_pause) return true;

            var hash = GetKeyHash(__originalMethod, __instance);

            if (__originalMethod.Name == OnGuiMethodName) hash ^= (long)Event.current.type << 17;

            if (!_data.TryGetValue(hash, out var info))
            {
                info = new ProfilerInfo(__originalMethod, __instance, __originalMethod.Name == OnGuiMethodName ? Event.current.type : (EventType)(-1));
                _data.Add(hash, info);
            }

            info.ExecutionOrder = _currentExecutionCount++;
            info.Timer.Reset();
            info.Timer.Start();
            info.PostfixRan = false;
            info.OriginalRan = false;
            info.SinceLastRun = 0;

            info.GcBytesAtStart = GC.GetTotalMemory(false);

            __state = info;

            return true;
        }

        private static void Postfix(bool __runOriginal, ProfilerInfo __state)
        {
            if (__state == null) return;

            var info = __state;

            info.TicksSpent.Sample(info.Timer.ElapsedTicks); //todo switch between ticks/ms? averaging?
            info.PostfixRan = true;
            info.OriginalRan = __runOriginal;

            var bytes = GC.GetTotalMemory(false) - info.GcBytesAtStart;
            if (bytes >= 0) // skip negative - collection happened as method was running
                info.GcBytes.Sample(bytes);
        }
    }
}
