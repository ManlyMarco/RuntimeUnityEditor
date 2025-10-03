﻿using System;
using System.Collections.Generic;
using System.Linq;
using RuntimeUnityEditor.Core.Inspector.Entries;
#if IL2CPP
using RuntimeUnityEditor.Core.Inspector.IL2CPP;
#endif
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector
{
    /// <summary>
    /// Window that allows browsing and editing of the contents of types and their instances.
    /// </summary>
    public sealed partial class Inspector : Window<Inspector>
    {
        internal const int InspectorRecordInitialHeight = 25;
        private readonly GUILayoutOption[] _inspectorTypeWidth = { GUILayout.Width(170), GUILayoutShim.MaxWidth(170) };
        private readonly GUILayoutOption[] _inspectorNameWidth = { GUILayout.Width(240), GUILayoutShim.MaxWidth(240) };

        private readonly List<InspectorTab> _tabs = new List<InspectorTab>();
        private InspectorTab _currentTab;
        private InspectorTab GetCurrentTab() => _currentTab ?? (_currentTab = _tabs.FirstOrDefault());
        private Vector2 _tabScrollPos = Vector2.zero;

        private GUIStyle _alignedButtonStyle, _alignedButtonStyleUnclickable;

        internal static int MaxWindowY => (int)Instance.WindowRect.height;

        /// <inheritdoc />
        protected override void VisibleChanged(bool visible)
        {
            base.VisibleChanged(visible);
            VariableFieldDrawer.ClearCache();
        }

        /// <inheritdoc />
        protected override void Initialize(InitSettings initSettings)
        {
            Title = "Inspector";
            MinimumSize = new Vector2(570, 170);
            Enabled = false;
            DefaultScreenPosition = ScreenPartition.CenterUpper;
        }

        private bool _focusSearchBox;
        private const string SearchBoxName = "InspectorFilterBox";

        /// <summary>
        /// Only show members that match the search string.
        /// Each stack item holds its own search string. This property shows the search string of the current tab item's visible stack entry.
        /// </summary>
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
#if IL2CPP
        private bool _showNative = true;
        private bool _showManaged = true;
        private readonly Color _il2CPPMemberColor = new(1f, 1f, 0.6f);
#endif
        private bool _showDeclaredOnly;
        private bool _showTooltips = true;

        private void DrawVariableNameEnterButton(ICacheEntry field)
        {
            if (_alignedButtonStyle == null || _alignedButtonStyleUnclickable == null)
            {
                _alignedButtonStyle = GUI.skin.button.CreateCopy();
                _alignedButtonStyle.alignment = TextAnchor.MiddleLeft;
                _alignedButtonStyle.wordWrap = true;

                _alignedButtonStyleUnclickable = _alignedButtonStyle.CreateCopy();
                _alignedButtonStyleUnclickable.normal.background = null;
                _alignedButtonStyleUnclickable.onNormal.background = null;
            }

            var canEnterValue = field.CanEnterValue();
            var val = field.GetValue();
#if IL2CPP
            if (IL2CPPCacheEntryHelper.IsIl2CppCacheEntry(field))
                GUI.color = _il2CPPMemberColor;
#endif
            if (GUILayout.Button(field.GetNameContent(), canEnterValue ? _alignedButtonStyle : _alignedButtonStyleUnclickable, _inspectorNameWidth))
            {
                if (IMGUIUtils.IsMouseRightClick())
                {
                    ContextMenu.Instance.Show(val, field);
                }
                else if (canEnterValue || val is Exception)
                {
                    var enterValue = field.EnterValue();
                    if (enterValue != null)
                    {
                        var entry = enterValue as InspectorStackEntryBase ?? new InstanceStackEntry(enterValue, field.Name(), field);

                        Push(entry, IMGUIUtils.IsMouseWheelClick());
                    }
                }
            }
            GUI.color = Color.white;
        }

        /// <summary>
        /// Show an object inside inspector.
        /// </summary>
        /// <param name="stackEntry">Object to show, wrapped in an appropriate stack entry.</param>
        /// <param name="newTab">Should the object be shown in a new tab, or at the top of current tab.</param>
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

        /// <summary>
        /// Get instance of currently visible object. If something other than an object instance is shown, null is returned.
        /// </summary>
        public object GetInspectedObject()
        {
            if (GetCurrentTab()?.CurrentStackItem is InstanceStackEntry se)
                return se.Instance;
            return null;
        }

        /// <inheritdoc />
        protected override void OnGUI()
        {
            base.OnGUI();

            VariableFieldDrawer.DrawInvokeWindow();
        }

        /// <inheritdoc />
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
                    GUILayout.BeginHorizontal(GUI.skin.box, IMGUIUtils.LayoutOptionsExpandWidthTrue);
                    {
                        GUILayout.Label("Filter:", IMGUIUtils.LayoutOptionsExpandWidthFalse);

                        GUI.SetNextControlName(SearchBoxName);
                        SearchString = GUILayout.TextField(SearchString, IMGUIUtils.LayoutOptionsExpandWidthTrue);

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
#if IL2CPP
                        GUI.color = _il2CPPMemberColor;
                        _showNative = GUILayout.Toggle(_showNative, new GUIContent("Native", null, "Display members from the IL2CPP runtime (i.e. the game code)."));
                        GUI.color = Color.white;
                        _showManaged = GUILayout.Toggle(_showManaged, new GUIContent("Managed", null, "Display members from the BepInEx's runtime (i.e. interop and plugin code)."));
#endif
                        _showDeclaredOnly = GUILayout.Toggle(_showDeclaredOnly, "Only declared");

                        /* todo
                        GUILayout.Label("Find:", IMGUIUtils.LayoutOptionsExpandWidthFalse);
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
                            if (GUILayout.Button(obj.Value, IMGUIUtils.LayoutOptionsExpandWidthFalse))
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

                            if (GUILayout.Button($"Tab {index + 1}: {LimitStringLengthForPreview(tab?.CurrentStackItem?.Name, 18)}", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                            {
                                // todo custom context menu for the tab bar? IMGUIUtils.IsMouseRightClick()
                                if (IMGUIUtils.IsMouseWheelClick())
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

                            if (GUILayout.Button(LimitStringLengthForPreview(stackEntry.Name, 90), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                            {
                                if (IMGUIUtils.IsMouseRightClick())
                                    stackEntry.ShowContextMenu();
                                else
                                    currentTab.CurrentStackItem = stackEntry;

                                GUI.backgroundColor = defaultGuiColor;
                                return;
                            }

                            if (i + 1 < stackEntries.Length)
                                GUILayout.Label(">", IMGUIUtils.LayoutOptionsExpandWidthFalse);

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
                            GUILayout.Label("Value", GUI.skin.box, IMGUIUtils.LayoutOptionsExpandWidthTrue);
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
                            if (value == null || (value is UnityEngine.Object obj && !obj)) return false;
                            return value.ToString().Contains(SearchString, StringComparison.OrdinalIgnoreCase);
                        });
                    }
                    visibleFieldsQuery = visibleFieldsQuery.Where(x =>
                    {
#if IL2CPP
                        if (IL2CPPCacheEntryHelper.IsIl2CppCacheEntry(x))
                        {
                            if (!_showNative)
                                return false;
                        }
                        else
                        {
                            if (!_showManaged)
                                return false;
                        }
                        if (x is IL2CPPFieldCacheEntry cf)
                            return _showFields && (!_showDeclaredOnly || cf.IsDeclared);
#endif
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

                    var scrollPositionY = (int)currentItem.ScrollPosition.y;
                    var scrollMaxVisibleY = scrollPositionY + ((int)WindowRect.height - 130); // TODO conservative value, properly measure at runtime for less overdraw

                    var topCombinedHeight = 0;
                    var index = 0;

                    // Empty space at the top
                    for (; index < visibleFields.Count; index++)
                    {
                        var newHeight = topCombinedHeight + visibleFields[index].ItemHeight;
                        if (newHeight >= scrollPositionY) break;
                        else topCombinedHeight = newHeight;
                    }

                    if (topCombinedHeight > 0)
                        GUILayout.Space(topCombinedHeight);

                    // Actual entries
                    for (; index < visibleFields.Count; index++)
                    {
                        var entry = visibleFields[index];

                        DrawSingleContentEntry(entry);

                        topCombinedHeight += entry.ItemHeight;

                        if (Event.current.type == EventType.Repaint)
                        {
                            var measured = (int)GUILayoutUtilityShim.GetLastRect().height;
                            entry.ItemHeight = Mathf.Max(entry.ItemHeight, measured);
                        }

                        if (topCombinedHeight > scrollMaxVisibleY)
                        {
                            index++;
                            break;
                        }
                    }

                    // Empty space at the bottom
                    var bottomCombinedHeight = 0;
                    for (; index < visibleFields.Count; index++)
                        bottomCombinedHeight += visibleFields[index].ItemHeight;

                    try
                    {
                        var extraSpace = Mathf.FloorToInt(WindowRect.height / 2);
                        GUILayout.Space(bottomCombinedHeight + extraSpace);
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
            GUILayout.BeginHorizontal(GUILayout.Height(entry.ItemHeight));
            {
                try
                {
                    GUILayout.Label(entry.TypeName(), _inspectorTypeWidth);

                    var value = entry.GetValue();

                    DrawVariableNameEnterButton(entry);

                    VariableFieldDrawer.DrawSettingValue(entry, value);
                }
                catch (Exception ex)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, $"[{Title}] Failed to draw setting {entry?.Name()} - {ex.Message}");
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}
