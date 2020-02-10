using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
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

        private InspectorTab _currentTab;
        [CanBeNull] private InspectorTab CurrentTab => _currentTab ?? (_currentTab = _tabs.FirstOrDefault());

        private readonly List<InspectorTab> _tabs = new List<InspectorTab>();

        private GUIStyle _alignedButtonStyle;
        private Rect _inspectorWindowRect;

        private object _currentlyEditingTag;
        private string _currentlyEditingText;
        private bool _userHasHitReturn;

        private bool _focusSearchBox;
        private const string SearchBoxName = "InspectorFilterBox";
        private string _searchString = "";

        public string SearchString
        {
            get => _searchString;
            // The string can't be null under unity 5.x or we crash
            set => _searchString = value ?? "";
        }

        private static Action<Transform> _treeListShowCallback;

        public Inspector(Action<Transform> treeListShowCallback)
        {
            _treeListShowCallback = treeListShowCallback ?? throw new ArgumentNullException(nameof(treeListShowCallback));
            _windowId = GetHashCode();
        }

        private void DrawEditableValue(ICacheEntry field, object value, params GUILayoutOption[] layoutParams)
        {
            var isBeingEdited = _currentlyEditingTag == field;
            var text = isBeingEdited ? _currentlyEditingText : ToStringConverter.GetEditValue(field, value);
            var result = GUILayout.TextField(text, layoutParams);

            if (!Equals(text, result) || isBeingEdited)
                if (_userHasHitReturn)
                {
                    _currentlyEditingTag = null;
                    _userHasHitReturn = false;
                    try
                    {
                        ToStringConverter.SetEditValue(field, value, result);
                    }
                    catch (Exception ex)
                    {
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, "[Inspector] Failed to set value - " + ex.Message);
                    }
                }
                else
                {
                    _currentlyEditingText = result;
                    _currentlyEditingTag = field;
                }
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
                    if (val is InspectorStackEntryBase sb)
                        InspectorPush(sb);
                    else
                        InspectorPush(new InstanceStackEntry(val, field.Name(), field));
                }
            }
        }

        public void InspectorClear()
        {
            CurrentTab.InspectorClear();
        }

        private void InspectorPop()
        {
            _focusSearchBox = true;
            SearchString = null;

            CurrentTab.InspectorPop();
        }

        public void InspectorPush(InspectorStackEntryBase stackEntry)
        {
            _focusSearchBox = true;
            SearchString = null;

            CurrentTab.InspectorPush(stackEntry);
        }

        public object GetInspectedObject()
        {
            if (CurrentTab.CurrentStackItem is InstanceStackEntry se)
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
                                {
                                    InspectorClear();
                                    InspectorPush(new InstanceStackEntry(obj.Key, obj.Value));
                                }
                            }
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.Space(6);

                        GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Width(160));
                        {
                            if (GUILayout.Button("Help")) 
                                InspectorPush(InspectorHelpObject.Create());
                            if (GUILayout.Button("Close")) 
                                InspectorClear();
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndHorizontal();

                    CurrentTab.InspectorStackScrollPos = GUILayout.BeginScrollView(CurrentTab.InspectorStackScrollPos, true, false,
                         GUI.skin.horizontalScrollbar, GUIStyle.none, GUIStyle.none, GUILayout.Height(46));
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.ExpandWidth(false),
                            GUILayout.ExpandHeight(false));
                        foreach (var item in CurrentTab.InspectorStack.Reverse().ToArray())
                        {
                            if (GUILayout.Button(item.Name, GUILayout.ExpandWidth(false)))
                            {
                                while (CurrentTab.InspectorStack.Peek() != item) InspectorPop();

                                return;
                            }
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

                        DrawContentScrollView();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, "[Inspector] GUI crash: " + ex);
                InspectorClear();
            }

            GUI.DragWindow();
        }

        private void DrawContentScrollView()
        {
            if (CurrentTab.InspectorStack.Count == 0) return;

            var currentItem = CurrentTab.CurrentStackItem;
            currentItem.ScrollPosition = GUILayout.BeginScrollView(currentItem.ScrollPosition);
            {
                GUILayout.BeginVertical();
                {
                    var visibleFields = string.IsNullOrEmpty(SearchString) ?
                        CurrentTab.FieldCache :
                        CurrentTab.FieldCache.Where(x => x.Name().Contains(SearchString, StringComparison.OrdinalIgnoreCase) || x.TypeName().Contains(SearchString, StringComparison.OrdinalIgnoreCase)).ToList();

                    var firstIndex = (int)(currentItem.ScrollPosition.y / InspectorRecordHeight);

                    GUILayout.Space(firstIndex * InspectorRecordHeight);

                    var currentVisibleCount = (int)(_inspectorWindowRect.height / InspectorRecordHeight) - 4;
                    for (var index = firstIndex; index < Mathf.Min(visibleFields.Count, firstIndex + currentVisibleCount); index++)
                    {
                        var entry = visibleFields[index];
                        try
                        {
                            DrawSingleContentEntry(entry);
                        }
                        catch (ArgumentException)
                        {
                            // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                        }
                    }
                    try
                    {
                        GUILayout.Space(Mathf.Max(_inspectorWindowRect.height / 2, (visibleFields.Count - firstIndex - currentVisibleCount) * InspectorRecordHeight));
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
                GUILayout.Label(entry.TypeName(), _inspectorTypeWidth);

                var value = entry.GetValue();

                if (entry.CanEnterValue() || value is Exception)
                    DrawVariableNameEnterButton(entry);
                else
                    GUILayout.TextArea(entry.Name(), GUI.skin.label, _inspectorNameWidth);

                if (entry.CanSetValue() && ToStringConverter.CanEditValue(entry, value))
                    DrawEditableValue(entry, value, GUILayout.ExpandWidth(true));
                else
                    GUILayout.TextArea(ToStringConverter.ObjectToString(value), GUI.skin.label, GUILayout.ExpandWidth(true));

                if (DnSpyHelper.IsAvailable && GUILayout.Button("^", _dnSpyButtonOptions))
                    DnSpyHelper.OpenInDnSpy(entry);
            }
            GUILayout.EndHorizontal();
        }

        public void DisplayInspector()
        {
            if (Event.current.isKey && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)) _userHasHitReturn = true;

            while (CurrentTab.InspectorStack.Count > 0 && !CurrentTab.InspectorStack.Peek().EntryIsValid())
            {
                var se = CurrentTab.InspectorStack.Pop();
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, $"[Inspector] Removed invalid/removed stack object: \"{se.Name}\"");
            }

            if (CurrentTab.InspectorStack.Count != 0)
            {
                _inspectorWindowRect = GUILayout.Window(_windowId, _inspectorWindowRect, InspectorWindow, "Inspector");
                InterfaceMaker.EatInputInRect(_inspectorWindowRect);
            }
        }

        public void UpdateWindowSize(Rect windowRect)
        {
            _inspectorWindowRect = windowRect;
        }

        public void InspectorUpdate()
        {
        }
    }
}