using System;
using System.Collections.Generic;
using System.Linq;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector
{
    public sealed partial class Inspector
    {
        private const int InspectorRecordHeight = 25;
        private readonly GUILayoutOption[] _inspectorTypeWidth = { GUILayout.Width(170), GUILayout.MaxWidth(170) };
        private readonly GUILayoutOption[] _inspectorNameWidth = { GUILayout.Width(240), GUILayout.MaxWidth(240) };
        private readonly GUILayoutOption _inspectorRecordHeight = GUILayout.Height(InspectorRecordHeight);
        private readonly GUILayoutOption _dnSpyButtonOptions = GUILayout.Width(19);
        private readonly int _windowId;

        private readonly List<InspectorTab> _tabs = new List<InspectorTab>();
        private InspectorTab _currentTab;
        private InspectorTab GetCurrentTab() => _currentTab ?? (_currentTab = _tabs.FirstOrDefault());
        private Vector2 _tabScrollPos = Vector2.zero;

        private GUIStyle _alignedButtonStyle;
        private static Rect _inspectorWindowRect;

        internal static int MaxWindowY => (int)_inspectorWindowRect.height;

        private bool _show;
        public bool Show
        {
            get => _show;
            set
            {
                if (_show == value) return;
                _show = value;
                VariableFieldDrawer.ClearCache();
            }
        }

        private bool _focusSearchBox;
        private const string SearchBoxName = "InspectorFilterBox";

        public string SearchString
        {
            get
            {
                var currentStackItem = GetCurrentTab()?.CurrentStackItem;
                return currentStackItem != null ? currentStackItem.SearchString : string.Empty;
            }
            set
            {
                var inspectorStackEntryBase = GetCurrentTab()?.CurrentStackItem;
                if (inspectorStackEntryBase != null) inspectorStackEntryBase.SearchString = value;
            }
        }

        private static Action<Transform> _treeListShowCallback;

        public Inspector(Action<Transform> treeListShowCallback)
        {
            _treeListShowCallback = treeListShowCallback ?? throw new ArgumentNullException(nameof(treeListShowCallback));
            _windowId = GetHashCode();
        }

        private void DrawVariableNameEnterButton(ICacheEntry field)
        {
            if (_alignedButtonStyle == null)
            {
                _alignedButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true
                };
            }

            if (GUILayout.Button(field.Name(), _alignedButtonStyle, _inspectorNameWidth))
            {
                var val = field.EnterValue();
                if (val != null)
                {
                    var entry = val as InspectorStackEntryBase ?? new InstanceStackEntry(val, field.Name(), field);
                    Push(entry, IsContextClick());
                }
            }
        }

        [Obsolete("Use push and Show instead")]
        public void InspectorClear() { }

        [Obsolete("Use push instead")]
        public void InspectorPush(InspectorStackEntryBase stackEntry)
        {
            Push(stackEntry, true);
        }

        public void Push(InspectorStackEntryBase stackEntry, bool newTab)
        {
            var tab = GetCurrentTab();
            if (tab == null || newTab)
            {
                tab = new InspectorTab();
                _tabs.Add(tab);
                _currentTab = tab;
            }
            tab.Push(stackEntry);

            _focusSearchBox = true;
            //tab.SearchString = string.Empty;

            Show = true;
        }

        private void RemoveTab(InspectorTab tab)
        {
            _tabs.Remove(tab);
            if (_currentTab == tab)
                _currentTab = null;
        }

        public object GetInspectedObject()
        {
            if (GetCurrentTab()?.CurrentStackItem is InstanceStackEntry se)
                return se.Instance;
            return null;
        }

        private void InspectorWindow(int id)
        {
            try
            {
                GUILayout.BeginVertical();
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.ExpandWidth(true));
                        {
                            GUILayout.Label("Filter:", GUILayout.ExpandWidth(false));

                            GUI.SetNextControlName(SearchBoxName);
                            SearchString = GUILayout.TextField(SearchString, GUILayout.ExpandWidth(true));

                            if (_focusSearchBox)
                            {
                                GUI.FocusWindow(id);
                                GUI.FocusControl(SearchBoxName);
                                _focusSearchBox = false;
                            }

                            GUILayout.Label("Find:", GUILayout.ExpandWidth(false));
                            foreach (var obj in new[]
                            {
                                new KeyValuePair<object, string>(
                                    EditorUtilities.GetInstanceClassScanner().OrderBy(x => x.Name()), "Instances"),
                                new KeyValuePair<object, string>(EditorUtilities.GetComponentScanner().OrderBy(x => x.Name()),
                                    "Components"),
                                new KeyValuePair<object, string>(
                                    EditorUtilities.GetMonoBehaviourScanner().OrderBy(x => x.Name()), "MonoBehaviours"),
                                new KeyValuePair<object, string>(EditorUtilities.GetTransformScanner().OrderBy(x => x.Name()),
                                    "Transforms")
                                //                            new KeyValuePair<object, string>(GetTypeScanner(CurrentTab.InspectorStack.Peek().GetType()).OrderBy(x=>x.Name()), CurrentTab.InspectorStack.Peek().GetType().ToString()+"s"),
                            })
                            {
                                if (obj.Key == null) continue;
                                if (GUILayout.Button(obj.Value, GUILayout.ExpandWidth(false)))
                                    Push(new InstanceStackEntry(obj.Key, obj.Value), true);
                            }
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.Space(6);

                        GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Width(160));
                        {
                            if (GUILayout.Button("Help"))
                                Push(InspectorHelpObject.Create(), true);
                            if (GUILayout.Button("Close"))
                                Show = false;
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndHorizontal();

                    var currentTab = GetCurrentTab();
                    var defaultGuiColor = GUI.color;
                    if (_tabs.Count >= 2)
                    {
                        _tabScrollPos = GUILayout.BeginScrollView(_tabScrollPos, false, false,
                            GUI.skin.horizontalScrollbar, GUIStyle.none, GUIStyle.none); //, GUILayout.Height(46)
                        {
                            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                            for (var index = 0; index < _tabs.Count; index++)
                            {
                                var tab = _tabs[index];

                                if (currentTab == tab)
                                    GUI.color = Color.cyan;

                                if (GUILayout.Button($"Tab {index + 1}: {LimitStringLengthForPreview(tab?.CurrentStackItem?.Name, 18)}", GUILayout.ExpandWidth(false)))
                                {
                                    if (IsContextClick())
                                        RemoveTab(tab);
                                    else
                                        _currentTab = tab;

                                    GUI.color = defaultGuiColor;
                                    break;
                                }

                                GUI.color = defaultGuiColor;
                            }

                            GUILayout.FlexibleSpace();
                            GUI.color = new Color(1, 1, 1, 0.6f);
                            if (GUILayout.Button("Close all"))
                            {
                                _tabs.Clear();
                                _currentTab = null;
                            }
                            GUI.color = defaultGuiColor;

                            GUILayout.EndHorizontal();
                        }
                        GUILayout.EndScrollView();
                    }

                    if (currentTab != null)
                    {
                        currentTab.InspectorStackScrollPos = GUILayout.BeginScrollView(currentTab.InspectorStackScrollPos, false, false,
                            GUI.skin.horizontalScrollbar, GUIStyle.none, GUIStyle.none); //, GUILayout.Height(46)
                        {
                            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                            var stackEntries = currentTab.InspectorStack.Reverse().ToArray();
                            for (var i = 0; i < stackEntries.Length; i++)
                            {
                                var item = stackEntries[i];

                                if (i + 1 == stackEntries.Length)
                                    GUI.color = Color.cyan;

                                if (GUILayout.Button(LimitStringLengthForPreview(item.Name, 90), GUILayout.ExpandWidth(false)))
                                {
                                    currentTab.PopUntil(item);
                                    GUI.color = defaultGuiColor;
                                    return;
                                }

                                if (i + 1 < stackEntries.Length)
                                    GUILayout.Label(">", GUILayout.ExpandWidth(false));

                                GUI.color = defaultGuiColor;
                            }
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.EndScrollView();

                        GUILayout.BeginVertical(GUI.skin.box);
                        {
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Space(1);
                                GUILayout.Label("Value/return type", GUI.skin.box, _inspectorTypeWidth);
                                GUILayout.Space(2);
                                GUILayout.Label("Member name", GUI.skin.box, _inspectorNameWidth);
                                GUILayout.Space(1);
                                GUILayout.Label("Value", GUI.skin.box, GUILayout.ExpandWidth(true));
                            }
                            GUILayout.EndHorizontal();

                            DrawContentScrollView(currentTab);
                        }
                        GUILayout.EndVertical();
                    }
                    else
                    {
                        GUILayout.Label("Nothing to show. Click on objects in the scene browser to open them in a new tab.");
                        GUILayout.Label("Tip: You can right click on a member inside inspector to open in a new tab, and on a tab to close it.");
                        GUILayout.FlexibleSpace();
                    }
                }
                GUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, "[Inspector] GUI crash: " + ex);
                //CurrentTab?.Pop();
            }

            VariableFieldDrawer.DrawCurrentDropdown();

            GUI.DragWindow();
        }

        private static string LimitStringLengthForPreview(string name, int maxLetters)
        {
            if (name == null) name = "NULL";
            if (name.Length >= maxLetters) name = name.Substring(0, maxLetters - 2) + "...";
            return name;
        }

        private static bool IsContextClick()
        {
            return Event.current.button >= 1;
        }

        private void DrawContentScrollView(InspectorTab tab)
        {
            if (tab == null || tab.InspectorStack.Count == 0)
            {
                GUILayout.FlexibleSpace();
                return;
            }

            var currentItem = tab.CurrentStackItem;
            currentItem.ScrollPosition = GUILayout.BeginScrollView(currentItem.ScrollPosition);
            {
                GUILayout.BeginVertical();
                {
                    var visibleFields = string.IsNullOrEmpty(SearchString) ?
                        tab.FieldCache :
                        tab.FieldCache.Where(x =>
                        {
                            var name = x.Name();
                            if (name != null && name.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) return true;
                            var typeName = x.TypeName();
                            if (typeName != null && typeName.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) return true;
                            var value = x.GetValue();
                            return value != null && value.ToString().Contains(SearchString, StringComparison.OrdinalIgnoreCase);
                        }).ToList();

                    var firstIndex = (int)(currentItem.ScrollPosition.y / InspectorRecordHeight);

                    GUILayout.Space(firstIndex * InspectorRecordHeight);

                    var currentVisibleCount = (int)(_inspectorWindowRect.height / InspectorRecordHeight) - 4;
                    for (var index = firstIndex; index < Mathf.Min(visibleFields.Count, firstIndex + currentVisibleCount); index++)
                    {
                        var entry = visibleFields[index];
                        DrawSingleContentEntry(entry);
                    }
                    try
                    {
                        GUILayout.Space(Mathf.FloorToInt(Mathf.Max(_inspectorWindowRect.height / 2, (visibleFields.Count - firstIndex - currentVisibleCount) * InspectorRecordHeight)));
                        // Fixes layout exploding when searching
                        GUILayout.FlexibleSpace();
                    }
                    catch
                    {
                        // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        private void DrawSingleContentEntry(ICacheEntry entry)
        {
            GUILayout.BeginHorizontal(_inspectorRecordHeight);
            {
                try
                {
                    GUILayout.Label(entry.TypeName(), _inspectorTypeWidth);

                    var value = entry.GetValue();

                    if (entry.CanEnterValue() || value is Exception)
                        DrawVariableNameEnterButton(entry);
                    else
                        GUILayout.TextArea(entry.Name(), GUI.skin.label, _inspectorNameWidth);

                    VariableFieldDrawer.DrawSettingValue(entry, value);

                    if (DnSpyHelper.IsAvailable && GUILayout.Button("^", _dnSpyButtonOptions))
                        DnSpyHelper.OpenInDnSpy(entry);
                }
                catch (Exception ex)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, $"Failed to draw setting {entry?.Name()} - {ex.Message}");
                    GUILayout.TextArea(ex.Message, GUI.skin.label, GUILayout.ExpandWidth(true));
                }
            }
            GUILayout.EndHorizontal();
        }

        public void DisplayInspector()
        {
            if (!Show) return;

            // Clean up dead tab contents
            foreach (var tab in _tabs.ToList())
            {
                while (tab.InspectorStack.Count > 0 && !tab.InspectorStack.Peek().EntryIsValid())
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, $"[Inspector] Removed invalid/removed stack object: \"{tab.InspectorStack.Peek().Name}\"");
                    tab.Pop();
                }

                if (tab.InspectorStack.Count == 0) RemoveTab(tab);
            }

            _inspectorWindowRect = GUILayout.Window(_windowId, _inspectorWindowRect, InspectorWindow, "Inspector");
            InterfaceMaker.EatInputInRect(_inspectorWindowRect);
        }

        public void UpdateWindowSize(Rect windowRect)
        {
            _inspectorWindowRect = windowRect;
        }
    }
}