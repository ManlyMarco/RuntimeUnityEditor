using System;
using System.Collections.Generic;
using System.Reflection;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using RuntimeUnityEditor.Core.Utils.ObjectDumper;
using UnityEngine;
#pragma warning disable CS1591

namespace RuntimeUnityEditor.Core.Clipboard
{
    /// <summary>
    /// Window that allows copying references to objects and using them later when invoking methods or setting fields/props.
    /// </summary>
    public class ClipboardWindow : Window<ClipboardWindow>
    {
        /// <summary>
        /// Contents of the clipboard.
        /// </summary>
        public static readonly List<object> Contents = new List<object>();
        private Vector2 _scrollPos;

        private string _pasteModeCurrentValueString;
        private MemberInfo _pasteModeMemberInfo;
        private object _pasteModeOwnerInstance;
        public bool InPasteMode => _pasteModeMemberInfo != null;

        protected override void Initialize(InitSettings initSettings)
        {
            Title = "Clipboard";
            MinimumSize = new Vector2(250, 100);
            DefaultScreenPosition = ScreenPartition.LeftUpper;
        }

        protected override void DrawContents()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true);

            var inPasteMode = InPasteMode;

            if (Contents.Count == 0)
            {
                if (inPasteMode) ExitPasteMode();

                GUILayout.BeginVertical();
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("You can copy objects to clipboard by clicking the 'C' button in inspector, or by running the 'copy(object)' command in REPL. Structs are copied by value, classes by reference.\n\n" +
                                    "Clipboard contents can be used in REPL by running the 'paste(index)' command, or in inspector when invoking a method.\n\n" +
                                    "Press 'X' to remove item from clipboard, right click on it to open a menu with more options.", GUILayout.ExpandWidth(true));
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndVertical();
            }
            else
            {
                // Draw clipboard items
                GUILayout.BeginVertical();
                {
                    if (inPasteMode)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"Select which value to paste into {_pasteModeMemberInfo.GetFancyDescription()}  (current value: {_pasteModeCurrentValueString ?? "NULL"})");
                        if (GUILayout.Button("Cancel")) ExitPasteMode();
                        GUILayout.EndHorizontal();
                    }

                    const int widthIndex = 35;
                    const int widthName = 70;

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Index", GUILayout.Width(widthIndex), GUILayout.ExpandWidth(false));
                        GUILayout.Label("Type", GUILayout.Width(widthName), GUILayout.ExpandWidth(false));
                        GUILayout.Label("Value", GUILayout.ExpandWidth(true));
                    }
                    GUILayout.EndHorizontal();

                    for (var index = 0; index < Contents.Count; index++)
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box);
                        {
                            var content = Contents[index];

                            if (inPasteMode)
                            {
                                if (GUILayout.Button("Paste", GUILayout.Width(widthIndex), GUILayout.ExpandWidth(false)))
                                {
                                    DoPaste(content);
                                }
                            }
                            else
                            {
                                if (GUILayout.Button(index.ToString(), GUI.skin.label, GUILayout.Width(widthIndex), GUILayout.ExpandWidth(false)) && IMGUIUtils.IsMouseRightClick())
                                    ContextMenu.Instance.Show(content, null, null);
                            }

                            var type = content?.GetType();

                            if (GUILayout.Button(type?.Name ?? "NULL", GUI.skin.label, GUILayout.Width(widthName), GUILayout.ExpandWidth(false)) && IMGUIUtils.IsMouseRightClick())
                                ContextMenu.Instance.Show(content, null, null);

                            var prevEnabled = GUI.enabled;
                            GUI.enabled = type != null && typeof(IConvertible).IsAssignableFrom(type);
                            GUI.changed = false;
                            var newVal = GUILayout.TextField(ToStringConverter.ObjectToString(content), GUILayout.ExpandWidth(true));
                            if (GUI.changed && type != null)
                            {
                                try
                                {
                                    Contents[index] = Convert.ChangeType(newVal, type);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"Could not convert string \"{newVal}\" to type \"{type.Name}\": {e.Message}");
                                }
                            }

                            GUI.enabled = prevEnabled;

                            if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                                Contents.RemoveAt(index);
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
        }

        private void DoPaste(object content)
        {
            switch (_pasteModeMemberInfo)
            {
                case PropertyInfo propertyInfo:
                    propertyInfo.SetValue(_pasteModeOwnerInstance, content, null);
                    break;
                case FieldInfo fieldInfo:
                    fieldInfo.SetValue(_pasteModeOwnerInstance, content);
                    break;
            }

            ExitPasteMode();
        }

        public void EnterPasteMode(object currentValue, MemberInfo memberInfo, object ownerInstance)
        {
            if (Contents.Count == 0 || memberInfo == null)
            {
                ExitPasteMode();
                return;
            }

            switch (memberInfo)
            {
                case PropertyInfo propertyInfo:
                    if (!propertyInfo.CanWrite)
                    {
                        ExitPasteMode();
                        return;
                    }

                    break;
                case FieldInfo fieldInfo:
                    if (fieldInfo.IsLiteral)
                    {
                        ExitPasteMode();
                        return;
                    }

                    break;
                default:
                    ExitPasteMode();
                    return;
            }

            _pasteModeCurrentValueString = currentValue?.ToString() ?? "NULL";
            _pasteModeMemberInfo = memberInfo;
            _pasteModeOwnerInstance = ownerInstance;
        }

        public void ExitPasteMode()
        {
            _pasteModeCurrentValueString = null;
            _pasteModeMemberInfo = null;
            _pasteModeOwnerInstance = null;
        }
    }
}
