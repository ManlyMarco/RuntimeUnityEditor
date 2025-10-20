using HarmonyLib;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RuntimeUnityEditor.Bepin5.PatchInspector
{
    internal sealed class ILViewerWindow : Window<ILViewerWindow>
    {
        public MethodBase Method => _method;

        private readonly MethodBase _method;
        private readonly string _originalIL;
        private readonly List<PatchMethodInfo> _patchMethods;

        private Vector2 _scrollPosition;
        private Vector2 _patchListScrollPosition;
        private ILViewMode _currentView = ILViewMode.Original;
        private int _selectedPatchIndex = -1;

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

            ResetWindowRect();
            //WindowRect = new Rect(100 + (windowId % 5) * 50, 100 + (windowId % 5) * 50, 900, 650);
        }

        protected override void Initialize(InitSettings initSettings) => throw new InvalidOperationException("This window should not be initialized");
        internal void DoOnGUI() => OnGUI();
        internal void DoVisibleChanged(bool visible) => VisibleChanged(visible);

        protected override void DrawContents()
        {
            GUILayout.BeginVertical();
            // todo cache + tooltip maybe buttons
            GUILayout.Label($"Method: {_method.DeclaringType?.FullName}.{_method.Name}");
            GUILayout.Label($"Parameters: {GetMethodParameters(_method)}");
            GUILayout.Label($"Return Type: {GetReturnType(_method)}");

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            bool opSelected = _currentView == ILViewMode.Original;
            if (opSelected) GUI.color = Color.yellow;
            if (GUILayout.Button("Original Method", GUILayout.Height(30)))
            {
                _currentView = ILViewMode.Original;
                _scrollPosition = Vector2.zero;
            }
            if (opSelected) GUI.color = Color.white;

            bool patchMethodsSelected = _currentView == ILViewMode.PatchMethods;
            if (patchMethodsSelected) GUI.color = Color.yellow;
            if (GUILayout.Button($"Patch Manager ({_patchMethods.Count})", GUILayout.Height(30)))
            {
                _currentView = ILViewMode.PatchMethods;
                _scrollPosition = Vector2.zero;
            }
            if (patchMethodsSelected) GUI.color = Color.white;

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            switch (_currentView)
            {
                case ILViewMode.Original:
                    DrawOriginalMethodView();
                    break;
                case ILViewMode.PatchMethods:
                    DrawPatchManagerView();
                    break;
            }

            GUILayout.EndVertical();
        }

        public void DrawOriginalMethodView()
        {
            GUILayout.Label("Original Method IL Code:");
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayoutShim.ExpandHeight(true));
            GUILayout.TextArea(_originalIL);
            GUILayout.EndScrollView();
        }

        private void DrawPatchManagerView()
        {
            if (_patchMethods.Count == 0)
            {
                GUILayout.Label("No patches found for this method.");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(400));
            GUILayout.Label("Patches:");

            if (GUILayout.Button("Refresh Patch List", GUILayout.Height(25)))
                RefreshPatchList();

            _patchListScrollPosition = GUILayout.BeginScrollView(_patchListScrollPosition, GUILayoutShim.ExpandHeight(true));

            for (var i = 0; i < _patchMethods.Count; i++)
            {
                var patch = _patchMethods[i];

                GUILayout.BeginVertical("box");
                GUILayout.BeginHorizontal();

                bool newEnabled = GUILayout.Toggle(patch.IsEnabled, "", GUILayout.Width(20));
                if (newEnabled != patch.IsEnabled)
                {
                    TogglePatch(i, newEnabled);
                    //SearchPatches(); // not needed?
                }

                GUILayout.BeginVertical();

                bool isSelected = _selectedPatchIndex == i;
                if (isSelected) GUI.color = Color.cyan;

                if (GUILayout.Button($"{patch.PatchType}: {patch.PatchMethod.DeclaringType?.Name}.{patch.PatchMethod.Name}", "label"))
                {
                    _selectedPatchIndex = i;
                    _scrollPosition = Vector2.zero;
                }

                if (isSelected) GUI.color = Color.white;

                GUILayout.Label($"Priority: {patch.Priority} | {patch.PatcherNamespace}");
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(2);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();

            if (_selectedPatchIndex >= 0 && _selectedPatchIndex < _patchMethods.Count)
            {
                var selectedPatch = _patchMethods[_selectedPatchIndex];
                GUILayout.Label($"IL Code for: {selectedPatch.PatchType} - {selectedPatch.PatchMethod.DeclaringType?.Name}.{selectedPatch.PatchMethod.Name}", GUI.skin.label);

                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayoutShim.ExpandHeight(true));

                GUILayout.TextArea(selectedPatch.ILCode);

                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Label("Select a patch from the list to view its IL code.", GUI.skin.label);
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }


        private void TogglePatch(int patchIndex, bool enable)
        {
            if (patchIndex < 0 || patchIndex >= _patchMethods.Count)
                return;

            var patch = _patchMethods[patchIndex];

            try
            {
                if (enable && !patch.IsEnabled)
                {
                    var harmony = new Harmony(patch.HarmonyId ?? "harmony.patch.inspector.temp");

                    switch (patch.PatchType)
                    {
                        case "Prefix":
                            harmony.Patch(_method, prefix: patch.HarmonyPatch);
                            break;
                        case "Postfix":
                            harmony.Patch(_method, postfix: patch.HarmonyPatch);
                            break;
                        case "Transpiler":
                            harmony.Patch(_method, transpiler: patch.HarmonyPatch);
                            break;
                        case "Finalizer":
                            harmony.Patch(_method, finalizer: patch.HarmonyPatch);
                            break;
                    }

                    patch.IsEnabled = true;
                }
                else if (!enable && patch.IsEnabled)
                {
                    var harmony = new Harmony(patch.HarmonyId ?? "harmony.patch.inspector.temp");
                    harmony.Unpatch(_method, patch.PatchMethod as MethodInfo);
                    patch.IsEnabled = false;
                }
            }
            catch (Exception e)
            {
                //patch.IsEnabled = !enable;
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, $"Failed to {(enable ? "enable" : "disable")} Harmony patch {patch.HarmonyId ?? "<NULL>"}:{patch.HarmonyPatch?.methodName}");
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
            }
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

        private static string GetMethodParameters(MethodBase method)
        {
            try
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 0)
                    return "()";

                var paramStrings = parameters.Select(p => $"{p.ParameterType.Name} {p.Name}");
                return $"({string.Join(", ", paramStrings.ToArray())})";
            }
            catch
            {
                return "(unknown)";
            }
        }

        private static string GetReturnType(MethodBase method)
        {
            try
            {
                if (method is MethodInfo methodInfo)
                    return methodInfo.ReturnType.Name;
                return "void";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}