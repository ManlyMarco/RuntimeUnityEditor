using System;
using System.Collections.Generic;
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

        protected override void Initialize(InitSettings initSettings)
        {
            Title = "Clipboard";
            MinimumSize = new Vector2(250, 100);
        }

        protected override Rect GetDefaultWindowRect(Rect screenRect)
        {
            return MakeDefaultWindowRect(screenRect, TextAlignment.Left);
        }

        protected override void DrawContents()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true);

            if (Contents.Count == 0)
            {
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

                            if (GUILayout.Button(index.ToString(), GUI.skin.label, GUILayout.Width(widthIndex), GUILayout.ExpandWidth(false)) && IMGUIUtils.IsMouseRightClick())
                                ContextMenu.Instance.Show(content, null);

                            var type = content?.GetType();

                            if (GUILayout.Button(type?.Name ?? "NULL", GUI.skin.label, GUILayout.Width(widthName), GUILayout.ExpandWidth(false)) && IMGUIUtils.IsMouseRightClick())
                                ContextMenu.Instance.Show(content, null);

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
    }
}
