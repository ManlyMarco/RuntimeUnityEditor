using HarmonyLib;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using ContextMenu = RuntimeUnityEditor.Core.ContextMenu;

namespace RuntimeUnityEditor.Bepin5.PatchInspector
{
    internal sealed class ILViewerWindow : Window<ILViewerWindow>
    {
        public MethodBase Method => _method;

        private readonly MethodBase _method;
        private readonly string _originalIL;
        private readonly List<PatchMethodInfo> _patchMethods;

        private readonly GUIContent _headingGc;

        private Vector2 _patchesScrollPosition, _ilScrollPosition;
        private int _selectedPatchIndex = -2;

        public ILViewerWindow(int windowId, MethodBase method, string originalIL, List<PatchMethodInfo> patchMethods)
        {
            WindowId = windowId;
            _method = method ?? throw new ArgumentNullException(nameof(method));
            _originalIL = originalIL ?? throw new ArgumentNullException(nameof(originalIL));
            _patchMethods = patchMethods ?? throw new ArgumentNullException(nameof(patchMethods));
            Enabled = true;
            DisplayType = FeatureDisplayType.Hidden;
            DefaultScreenPosition = ScreenPartition.CenterUpper;
            Title = $"IL Code: {_method.DeclaringType?.Name}.{_method.Name}";

            _headingGc = new GUIContent(_method.FullDescription(), null, _method.GetFancyDescription());

            OnVisibleChanged(true);
        }

        protected override void Initialize(InitSettings initSettings) => throw new InvalidOperationException("This window should not be initialized");
        internal void DoOnGUI() => OnGUI();
        internal void DoVisibleChanged(bool visible) => VisibleChanged(visible);

        protected override void DrawContents()
        {
            if (GUILayout.Button(_headingGc, IMGUIUtils.UpperCenterLabelStyle, IMGUIUtils.EmptyLayoutOptions))
            {
                //if (IMGUIUtils.IsMouseRightClick())
                ContextMenu.Instance.Show(_method);
            }

            GUILayout.BeginHorizontal(IMGUIUtils.EmptyLayoutOptions);
            {
                _patchesScrollPosition = GUILayout.BeginScrollView(_patchesScrollPosition, GUI.skin.box, GUILayoutShim.ExpandHeight(true), GUILayout.Width(400));
                {
                    GUILayout.BeginVertical(GUI.skin.box, IMGUIUtils.EmptyLayoutOptions);
                    {
                        var isSelected = _selectedPatchIndex == -1;
                        if (isSelected) GUI.color = Color.cyan;

                        if (GUILayout.Button($" [Original method] {_method.Name}", GUI.skin.label, IMGUIUtils.LayoutOptionsExpandWidthTrue))
                        {
                            if (IMGUIUtils.IsMouseRightClick())
                            {
                                ContextMenu.Instance.Show(_method, null, "[MethodInfo] " + _method?.Name, null, null);
                            }
                            else
                            {
                                _selectedPatchIndex = -1;
                                _ilScrollPosition = Vector2.zero;
                            }
                        }

                        if (isSelected) GUI.color = Color.white;
                    }
                    GUILayout.EndVertical();

                    GUILayout.BeginHorizontal(IMGUIUtils.EmptyLayoutOptions);
                    {
                        GUILayout.Label("Patches:", IMGUIUtils.LayoutOptionsExpandWidthTrue);

                        if (GUILayout.Button("Refresh", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                            RefreshPatchList();
                    }
                    GUILayout.EndHorizontal();

                    if (_patchMethods.Count == 0)
                    {
                        GUILayout.Label("No patches found for this method.", IMGUIUtils.UpperCenterLabelStyle, IMGUIUtils.EmptyLayoutOptions);
                    }
                    else
                    {
                        for (var i = 0; i < _patchMethods.Count; i++)
                        {
                            var patch = _patchMethods[i];

                            GUILayout.BeginHorizontal(GUI.skin.box, IMGUIUtils.EmptyLayoutOptions);
                            {
                                var newEnabled = GUILayout.Toggle(patch.IsEnabled, "", GUILayout.Width(20));
                                if (newEnabled != patch.IsEnabled)
                                    PatchInspector.Instance.TogglePatch(_method, _patchMethods[i], newEnabled);

                                var isSelected = _selectedPatchIndex == i;
                                if (isSelected) GUI.color = Color.cyan;

                                var patchName = $"[{patch.PatchType}] {patch.PatchMethod.DeclaringType?.Name}.{patch.PatchMethod.Name}";
                                if (GUILayout.Button(patchName + $"\nPriority: {patch.Priority} | {patch.PatcherNamespace}", GUI.skin.label, IMGUIUtils.LayoutOptionsExpandWidthTrue))
                                {
                                    if (IMGUIUtils.IsMouseRightClick())
                                    {
                                        ContextMenu.Instance.Show(patch.PatchMethod, null, "[MethodInfo] " + patch.PatchMethod?.Name, null, null);
                                    }
                                    else
                                    {
                                        _selectedPatchIndex = i;
                                        _ilScrollPosition = Vector2.zero;
                                    }
                                }

                                if (isSelected) GUI.color = Color.white;
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                GUILayout.EndScrollView();

                GUILayout.Space(5);

                GUILayout.BeginVertical(GUI.skin.box, GUILayoutShim.ExpandHeight(true), GUILayoutShim.ExpandWidth(true));
                if (_selectedPatchIndex == -1)
                {
                    GUILayout.Label("IL Code for the Original Method (target being patched)", IMGUIUtils.EmptyLayoutOptions);
                    _ilScrollPosition = GUILayout.BeginScrollView(_ilScrollPosition, GUILayoutShim.ExpandHeight(true));
                    GUILayout.TextArea(_originalIL, IMGUIUtils.EmptyLayoutOptions);
                    GUILayout.EndScrollView();
                }
                else if (_selectedPatchIndex >= 0 && _selectedPatchIndex < _patchMethods.Count)
                {
                    var selectedPatch = _patchMethods[_selectedPatchIndex];

                    GUILayout.BeginHorizontal(IMGUIUtils.EmptyLayoutOptions);
                    {
                        GUILayout.Space(3);

                        var patchName = $"[{selectedPatch.PatchType}] {selectedPatch.PatchMethod.DeclaringType?.Name}.{selectedPatch.PatchMethod.Name}";
                        GUILayout.Label($"IL Code for: {patchName}", IMGUIUtils.LayoutOptionsExpandWidthTrue);

                        if (GUILayout.Button("Inspect", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                            Inspector.Instance.Push(new InstanceStackEntry(selectedPatch, patchName), true);

                        GUILayout.Space(3);
                    }
                    GUILayout.EndHorizontal();

                    _ilScrollPosition = GUILayout.BeginScrollView(_ilScrollPosition, GUILayoutShim.ExpandHeight(true));

                    GUILayout.TextArea(selectedPatch.ILCode, IMGUIUtils.EmptyLayoutOptions);

                    GUILayout.EndScrollView();
                }
                else
                {
                    GUILayout.Label("Select a method from the list on the left to view its IL code.", IMGUIUtils.MiddleCenterLabelStyle, IMGUIUtils.EmptyLayoutOptions);
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
        }

        private void RefreshPatchList()
        {
            try
            {
                var currentPatchInfo = Harmony.GetPatchInfo(_method);
                var currentPatches = new List<MethodInfo>();

                if (currentPatchInfo != null)
                {
                    if (currentPatchInfo.Prefixes != null)
                        currentPatches.AddRange(currentPatchInfo.Prefixes.Select(p => p.PatchMethod));
                    if (currentPatchInfo.Postfixes != null)
                        currentPatches.AddRange(currentPatchInfo.Postfixes.Select(p => p.PatchMethod));
                    if (currentPatchInfo.Transpilers != null)
                        currentPatches.AddRange(currentPatchInfo.Transpilers.Select(p => p.PatchMethod));
                    if (currentPatchInfo.Finalizers != null)
                        currentPatches.AddRange(currentPatchInfo.Finalizers.Select(p => p.PatchMethod));
                }

                foreach (var patch in _patchMethods)
                {
                    patch.IsEnabled = currentPatches.Contains(patch.PatchMethod as MethodInfo);
                }
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
            }
        }
    }
}