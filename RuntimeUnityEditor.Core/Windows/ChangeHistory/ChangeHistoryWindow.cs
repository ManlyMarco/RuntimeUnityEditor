﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core.ChangeHistory
{
    /// <summary>
    /// UI window that displays a list of changes made to the game by RUE (or more specifically by everything that used the <see cref="T:RuntimeUnityEditor.Core.ChangeHistory.Change" /> API).
    /// </summary>
    public class ChangeHistoryWindow : Window<ChangeHistoryWindow>
    {
        private static readonly GUIContent _inspectContent = new GUIContent("Insp.", null, "Inspect target object (the object that contains the changed member, not the changed member itself)");
        private static readonly GUIContent _undoContent = new GUIContent("Undo", null, "Attempt to undo this action. The more changes were made to the affected object since this change, the less reliable this will be.");

        private Vector2 _scrollPos;
        private InitSettings.Setting<bool> _showTimestamps;

        /// <inheritdoc />
        protected override void Initialize(InitSettings initSettings)
        {
            DisplayName = "History";
            Title = "Change History";
            DefaultScreenPosition = ScreenPartition.LeftLower;

            _showTimestamps = initSettings.RegisterSetting("Change History", "Show timestamps in Change History window", true, string.Empty);
        }

        /// <inheritdoc />
        protected override void DrawContents()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                if (GUILayout.Button("Clear"))
                    Change.Changes.Clear();

                GUILayout.Space(5);

                if (GUILayout.Button("Copy all to clipboard"))
                {
                    try
                    {
                        UnityFeatureHelper.systemCopyBuffer = string.Join("\n", Change.Changes.Select(c => c.GetDisplayString()).ToArray());
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, $"Copied {Change.Changes.Count} changes to clipboard");
                    }
                    catch (Exception e)
                    {
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Message | LogLevel.Error, "Failed to copy to clipboard: " + e.Message);
                    }
                }

                if (GUILayout.Button("...as pseudo-code"))
                {
                    try
                    {
                        UnityFeatureHelper.systemCopyBuffer = string.Join("\n", Change.Changes.Select(ConvertChangeToPseudoCodeString).ToArray());
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, $"Copied {Change.Changes.Count} changes to clipboard (converted to pseudo-code)");
                    }
                    catch (Exception e)
                    {
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Message | LogLevel.Error, "Failed to copy to clipboard: " + e.Message);
                    }
                }

                GUILayout.FlexibleSpace();

                GUI.changed = false;
                _showTimestamps.Value = GUILayout.Toggle(_showTimestamps.Value, "Show timestamps");
            }
            GUILayout.EndHorizontal();

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true);
            {
                foreach (var change in Change.Changes)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        if (_showTimestamps.Value)
                        {
                            GUI.color = new Color(0.67f, 0.67f, 0.67f);
                            GUILayout.Label(change.ChangeTime.ToString("HH:mm:ss"), GUILayoutShim.MinWidth(50));
                            GUI.color = Color.white;
                        }

                        GUILayout.TextField(change.GetDisplayString(), GUI.skin.label, IMGUIUtils.LayoutOptionsExpandWidthTrue);

                        if (change.CanUndo && GUILayout.Button(_undoContent, IMGUIUtils.LayoutOptionsExpandWidthFalse))
                        {
                            try
                            {
                                change.Undo();
                            }
                            catch (Exception e)
                            {
                                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning | LogLevel.Message, "Failed to Undo: " + e.Message);
                            }
                        }

                        if (!change.Target.IsNullOrDestroyed() && GUILayout.Button(_inspectContent, IMGUIUtils.LayoutOptionsExpandWidthFalse))
                            Inspector.Inspector.Instance.Push(new InstanceStackEntry(change.Target, change.GetDisplayString()), true);
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
        }

        private static string ConvertChangeToPseudoCodeString(IChange change)
        {
            var displayString = change.GetDisplayString();
            var cleaned = Regex.Replace(displayString, @"^\([\w/ ]*?\)::", "");
            cleaned = Regex.Replace(cleaned, @"\(([\w/ ]*?)\)::GameObject", "GameObject.Find(\"$1\")");
            return cleaned.Length == 0 ? displayString : cleaned;
        }
    }
}