using System;
using System.Collections.Generic;
using System.Linq;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector
{
    public sealed partial class Inspector : Window<Inspector>
    {
        private const int InspectorRecordHeight = 25;
        private readonly GUILayoutOption[] _inspectorTypeWidth = { GUILayout.Width(170), GUILayout.MaxWidth(170) };
        private readonly GUILayoutOption[] _inspectorNameWidth = { GUILayout.Width(240), GUILayout.MaxWidth(240) };
        private readonly GUILayoutOption _inspectorRecordHeight = GUILayout.Height(InspectorRecordHeight);

        private readonly GUILayoutOption _buttonDnspyOptions = GUILayout.Width(19);
        private readonly GUIContent _buttonDnspyContent = new GUIContent("^", "Open in DnSpy (launches DnSpy, loads assembly containing this member, and opens a new tab focused on this member)");
        private readonly GUIContent _buttonCopyContent = new GUIContent("C", "Copy to clipboard");

        private readonly List<InspectorTab> _tabs = new List<InspectorTab>();
        private InspectorTab _currentTab;
        private InspectorTab GetCurrentTab() => _currentTab ?? (_currentTab = _tabs.FirstOrDefault());
        private Vector2 _tabScrollPos = Vector2.zero;

        private GUIStyle _alignedButtonStyle;

        internal static int MaxWindowY => (int)Instance.WindowRect.height;

        protected override void VisibleChanged(bool visible)
        {
            base.VisibleChanged(visible);
            VariableFieldDrawer.ClearCache();
        }

        protected override void Initialize(InitSettings initSettings)
        {
            Title = "Inspector";
            MinimumSize = new Vector2(570, 170);
            Enabled = false;
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

        private bool _showFields = true;
        private bool _showProperties = true;
        private bool _showMethods = true;
        private bool _showEvents = true;
        private bool _showDeclaredOnly = false;
        private bool _showTooltips = true;

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

            if (GUILayout.Button(field.GetNameContent(), _alignedButtonStyle, _inspectorNameWidth))
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

            Enabled = true;
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

        protected override Rect GetDefaultWindowRect(Rect screenRect)
        {
            return MakeDefaultWindowRect(screenRect, TextAlignment.Center);
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            VariableFieldDrawer.DrawInvokeWindow();
        }

        protected override void DrawContents()
        {
            // Close the invoke window if the main inspector window was clicked (event check needs to be inside of the clicked window func)
            if (Event.current.type == EventType.MouseDown)
                VariableFieldDrawer.ShowInvokeWindow(null, null);

            // Clean up dead tab contents
            foreach (var tab in _tabs.ToList())
            {
                while (tab.InspectorStack.Count > 0 && !tab.InspectorStack.Peek().EntryIsValid())
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, $"[{Title}] Removed invalid/removed stack object: \"{tab.InspectorStack.Peek().Name}\"");
                    tab.Pop();
                }

                if (tab.InspectorStack.Count == 0) RemoveTab(tab);
            }

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
                            GUI.FocusWindow(WindowId);
                            GUI.FocusControl(SearchBoxName);
                            _focusSearchBox = false;
                        }

                        _showFields = GUILayout.Toggle(_showFields, "Fields");
                        _showProperties = GUILayout.Toggle(_showProperties, "Properties");
                        _showMethods = GUILayout.Toggle(_showMethods, "Methods");
                        _showEvents = GUILayout.Toggle(_showEvents, "Events");
                        _showDeclaredOnly = GUILayout.Toggle(_showDeclaredOnly, "Only declared");

                        /* todo
                        GUILayout.Label("Find:", GUILayout.ExpandWidth(false));
                        foreach (var obj in new[]
                        {
                                new KeyValuePair<object, string>(EditorUtilities.GetInstanceClassScanner().OrderBy(x => x.Name()), "Instances"),
                                new KeyValuePair<object, string>(EditorUtilities.GetComponentScanner().OrderBy(x => x.Name()), "Components"),
                                new KeyValuePair<object, string>(EditorUtilities.GetMonoBehaviourScanner().OrderBy(x => x.Name()), "MonoBehaviours"),
                                new KeyValuePair<object, string>(EditorUtilities.GetTransformScanner().OrderBy(x => x.Name()), "Transforms")
                                //new KeyValuePair<object, string>(GetTypeScanner(CurrentTab.InspectorStack.Peek().GetType()).OrderBy(x=>x.Name()), CurrentTab.InspectorStack.Peek().GetType().ToString()+"s"),
                            })
                        {
                            if (obj.Key == null) continue;
                            if (GUILayout.Button(obj.Value, GUILayout.ExpandWidth(false)))
                                Push(new InstanceStackEntry(obj.Key, obj.Value), true);
                        }*/
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Width(80));
                    {
                        if (_tabs.Count == 0) GUI.enabled = false;
                        if (GUILayout.Button("Close all tabs"))
                        {
                            _tabs.Clear();
                            _currentTab = null;
                        }
                        GUI.enabled = true;
                        
                        _showTooltips = GUILayout.Toggle(_showTooltips, "Tooltips");
                        
                        if (GUILayout.Button("Help"))
                            Push(InspectorHelpObject.Create(), true);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndHorizontal();

                var currentTab = GetCurrentTab();
                var defaultGuiColor = GUI.backgroundColor;
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
                                GUI.backgroundColor = Color.cyan;

                            if (GUILayout.Button($"Tab {index + 1}: {LimitStringLengthForPreview(tab?.CurrentStackItem?.Name, 18)}", GUILayout.ExpandWidth(false)))
                            {
                                if (IsContextClick())
                                    RemoveTab(tab);
                                else
                                    _currentTab = tab;

                                GUI.backgroundColor = defaultGuiColor;
                                break;
                            }

                            GUI.backgroundColor = defaultGuiColor;
                        }

                        GUI.backgroundColor = defaultGuiColor;

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
                            var stackEntry = stackEntries[i];

                            if (stackEntry == currentTab.CurrentStackItem)
                                GUI.backgroundColor = Color.cyan;

                            if (GUILayout.Button(LimitStringLengthForPreview(stackEntry.Name, 90), GUILayout.ExpandWidth(false)))
                            {
                                currentTab.CurrentStackItem = stackEntry;
                                GUI.backgroundColor = defaultGuiColor;
                                return;
                            }

                            if (i + 1 < stackEntries.Length)
                                GUILayout.Label(">", GUILayout.ExpandWidth(false));

                            GUI.backgroundColor = defaultGuiColor;
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
                    // Nothing to show
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        GUILayout.Space(8);
                        GUILayout.BeginVertical();
                        {
                            GUILayout.Space(8);
                            GUILayout.Label("Nothing to show. You can inspect objects by clicking the \"Inspect\" buttons in other windows. Each object will be opened in a new tab. You can also send instances or types to be inspected from repl by using the \"seti(instance)\" and \"setis(type)\" commands.");
                            GUILayout.Label("Tip: You can right click on a member inside inspector to open it in a new tab instead of opening it in the current tab. You can also right click on tabs to close them.");
                            GUILayout.Space(8);
                        }
                        GUILayout.EndVertical();
                        GUILayout.Space(8);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                }
            }
            GUILayout.EndVertical();

            VariableFieldDrawer.DrawCurrentDropdown();

            if (!_showTooltips)
                GUI.tooltip = string.Empty;
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
                    //todo optimize gc
                    IEnumerable<ICacheEntry> visibleFieldsQuery = tab.FieldCache;
                    if (!string.IsNullOrEmpty(SearchString))
                    {
                        visibleFieldsQuery = visibleFieldsQuery.Where(x =>
                        {
                            var name = x.Name();
                            if (name != null && name.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) return true;
                            var typeName = x.TypeName();
                            if (typeName != null && typeName.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) return true;
                            var value = x.GetValue();
                            return value != null && value.ToString().Contains(SearchString, StringComparison.OrdinalIgnoreCase);
                        });
                    }
                    visibleFieldsQuery = visibleFieldsQuery.Where(x =>
                    {
                        switch (x)
                        {
                            case PropertyCacheEntry p when !_showProperties || _showDeclaredOnly && !p.IsDeclared:
                            case FieldCacheEntry f when !_showFields || _showDeclaredOnly && !f.IsDeclared:
                            case MethodCacheEntry m when !_showMethods || _showDeclaredOnly && !m.IsDeclared:
                            case EventCacheEntry e when !_showEvents || _showDeclaredOnly && !e.IsDeclared:
                                return false;
                            default:
                                return true;
                        }
                    });
                    var visibleFields = visibleFieldsQuery.ToList();

                    var firstIndex = (int)(currentItem.ScrollPosition.y / InspectorRecordHeight);

                    GUILayout.Space(firstIndex * InspectorRecordHeight);

                    var currentVisibleCount = (int)(WindowRect.height / InspectorRecordHeight) - 4;
                    for (var index = firstIndex; index < Mathf.Min(visibleFields.Count, firstIndex + currentVisibleCount); index++)
                    {
                        var entry = visibleFields[index];
                        DrawSingleContentEntry(entry);
                    }
                    try
                    {
                        GUILayout.Space(Mathf.FloorToInt(Mathf.Max(WindowRect.height / 2, (visibleFields.Count - firstIndex - currentVisibleCount) * InspectorRecordHeight)));
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
                        GUILayout.Label(entry.GetNameContent(), _inspectorNameWidth);

                    VariableFieldDrawer.DrawSettingValue(entry, value);

                    if (Clipboard.ClipboardWindow.Initialized && value != null && GUILayout.Button(_buttonCopyContent, _buttonDnspyOptions))
                    {
                        Clipboard.ClipboardWindow.Contents.Add(value);
                        Clipboard.ClipboardWindow.Instance.Enabled = true;
                    }

                    if (DnSpyHelper.IsAvailable && GUILayout.Button(_buttonDnspyContent, _buttonDnspyOptions))
                        DnSpyHelper.OpenInDnSpy(entry);
                }
                catch (Exception ex)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, $"[{Title}] Failed to draw setting {entry?.Name()} - {ex.Message}");
                    GUILayout.TextArea(ex.Message, GUI.skin.label, GUILayout.ExpandWidth(true));
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}