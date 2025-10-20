using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Bepin5.PatchInspector
{
    /// <summary>
    /// Window for inspecting and managing Harmony patches applied to methods. 
    /// </summary>
    public class PatchInspector : Window<PatchInspector>
    {
        private string _searchInput = String.Empty;
        private Vector2 _scrollPos;
        private List<PatchInfo> _foundPatches = new List<PatchInfo>();
        private bool _showFilePaths = true;

        private int _nextWindowId = 13000;
        private readonly List<ILViewerWindow> _ilViewerWindows = new List<ILViewerWindow>();
        private readonly Dictionary<string, List<PatchMethodInfo>> _opPatchStates = new Dictionary<string, List<PatchMethodInfo>>();

        /// <inheritdoc />
        protected override void Initialize(InitSettings initSettings)
        {
            Enabled = false;
            DefaultScreenPosition = ScreenPartition.LeftUpper;
            DisplayName = "Patch Inspector";
            Title = "Patch Inspector";
        }

        /// <inheritdoc />
        protected override void VisibleChanged(bool visible)
        {
            if (visible)
            {
                //_searchInput = string.Empty;
                //_foundPatches.Clear();
                SearchPatches();
            }

            foreach (var window in _ilViewerWindows)
            {
                if (window.Enabled)
                    window.DoVisibleChanged(visible);
            }

            base.VisibleChanged(visible);
        }

        /// <inheritdoc />
        protected override void OnGUI()
        {
            for (var i = 0; i < _ilViewerWindows.Count; i++)
            {
                var window = _ilViewerWindows[i];
                if (window.Enabled)
                {
                    window.DoOnGUI();
                }
                else
                {
                    _ilViewerWindows.RemoveAt(i);
                    i--;
                }
            }

            base.OnGUI();
        }

        /// <inheritdoc />
        protected override void DrawContents()
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Search for patches by method, class, or namespace:", GUI.skin.label);
            GUILayout.Label("Examples: 'OnClick', 'method:OnClick class:AddButtonCtrl', 'namespace:SimpleGame'");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(60));

            string newSearchInput = GUILayout.TextField(_searchInput);
            if (newSearchInput != _searchInput)
            {
                _searchInput = newSearchInput;
                SearchPatches();
                return;
            }

            // Replaced by upper OnValueChanged
            /*if (GUILayout.Button("Search", GUILayout.Width(80)))
			{
				SearchPatches();
			}*/

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                _searchInput = string.Empty;
                _foundPatches.Clear();
            }
            GUILayout.EndHorizontal();

            _showFilePaths = GUILayout.Toggle(_showFilePaths, "Show file paths");

            GUILayout.Space(10);

            if (_foundPatches.Count > 0)
            {
                GUILayout.Label($"Found {_foundPatches.Count} patches:");
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayoutShim.ExpandHeight(true));

                foreach (var patch in _foundPatches)
                {
                    GUILayout.BeginVertical();
                    Color bgColor = patch.IsEnabled ? Color.white : new Color(1f, 0.39f, 0.39f, 0.3f);

                    GUILayout.BeginVertical("box");
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical();

                    GUI.color = bgColor;
                    GUILayout.Label($"Method: {patch.TargetType}.{patch.MethodName}");
                    GUILayout.Label($"Patch Type: {patch.PatchType}");
                    GUILayout.Label($"Patcher: {patch.PatcherNamespace}");
                    GUILayout.Label($"Assembly: {patch.PatcherAssembly}");

                    if (_showFilePaths && !string.IsNullOrEmpty(patch.FilePath))
                    {
                        GUILayout.Label($"File: {patch.FilePath}");
                    }

                    GUI.color = Color.white;
                    GUILayout.EndVertical();

                    GUILayout.BeginVertical(GUILayout.Width(80));

                    bool newEnabled = GUILayout.Toggle(patch.IsEnabled, "Enabled");
                    if (newEnabled != patch.IsEnabled)
                    {
                        TogglePatchDirect(patch, newEnabled);
                        SearchPatches();
                        return;
                    }

                    if (GUILayout.Button("View IL", GUILayout.Height(25)))
                    {
                        OpenILViewer(patch.TargetMethod);
                    }

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.EndVertical();
                    GUILayout.Space(5);
                }

                GUILayout.EndScrollView();
            }
            else if (!string.IsNullOrEmpty(_searchInput))
            {
                GUILayout.Label("No patches found.");
            }
            else
            {
                GUILayout.Label("Enter a method name, namespace, or type to search for patches.");
            }

            GUILayout.Space(10);
            GUILayout.EndVertical();
        }

        private void SearchPatches()
        {
            _foundPatches.Clear();

            string searchTerm = _searchInput?.Trim();

            if (string.IsNullOrEmpty(searchTerm))
                return;

            try
            {
                var searchCriteria = ParseSearchInput(searchTerm);
                // Not entirely sure why I had this in the old project? PatchInspector does not own any Patches...
                // var harmony = Harmony.CreateAndPatchAll(typeof(PatchInspector));
                var patchedMethods = Harmony.GetAllPatchedMethods();

                foreach (var method in patchedMethods)
                {
                    var patches = Harmony.GetPatchInfo(method);
                    if (patches == null) continue;

                    if (!MatchesSearchCriteria(method, searchCriteria))
                        continue;

                    AddPatchesToList(patches.Prefixes.ToArray(), method, "Prefix");
                    AddPatchesToList(patches.Postfixes.ToArray(), method, "Postfix");
                    AddPatchesToList(patches.Transpilers.ToArray(), method, "Transpiler");
                    AddPatchesToList(patches.Finalizers.ToArray(), method, "Finalizer");
                }

                _foundPatches = _foundPatches.OrderBy(info => info.TargetType).ThenBy(info => info.MethodName).ToList();
            }
            catch (Exception ex)
            {
                // todo cleaner logging
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, ex);
            }
        }

        private static SearchCriteria ParseSearchInput(string input)
        {
            var criteria = new SearchCriteria();

            if (input.Contains(":"))
            {
                criteria.IsStructured = true;
                var parts = input.Split(' ');

                foreach (string part in parts)
                {
                    if (part.StartsWith("method:", StringComparison.OrdinalIgnoreCase))
                        criteria.Method = part.Substring(7);
                    else if (part.StartsWith("class:", StringComparison.OrdinalIgnoreCase))
                        criteria.Class = part.Substring(6);
                    else if (part.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                        criteria.Class = part.Substring(5);
                    else if (part.StartsWith("namespace:", StringComparison.OrdinalIgnoreCase))
                        criteria.Namespace = part.Substring(10);
                    else if (!part.Contains(":"))
                        criteria.Text = part;
                }
            }
            else
            {
                criteria.Text = input;
                criteria.IsStructured = false;
            }

            return criteria;
        }

        private static bool MatchesSearchCriteria(MethodBase method, SearchCriteria criteria)
        {
            if (criteria.IsStructured)
            {
                bool matches = true;

                if (!string.IsNullOrEmpty(criteria.Method))
                    matches &= method.Name.IndexOf(criteria.Method, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!string.IsNullOrEmpty(criteria.Class))
                    matches &= method.DeclaringType?.Name.IndexOf(criteria.Class, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                method.DeclaringType?.FullName?.IndexOf(criteria.Class, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!string.IsNullOrEmpty(criteria.Namespace))
                    matches &= method.DeclaringType?.Namespace?.IndexOf(criteria.Namespace, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!string.IsNullOrEmpty(criteria.Text))
                {
                    string searchTerm = criteria.Text.ToLower();
                    string fullMethodName = $"{method.DeclaringType?.FullName}.{method.Name}".ToLower();
                    string methodWithParams = GetMethodSignature(method).ToLower();

                    matches &= method.Name.ToLower().Contains(searchTerm) ||
                                method.DeclaringType?.Name.ToLower().Contains(searchTerm) == true ||
                                method.DeclaringType?.Namespace?.ToLower().Contains(searchTerm) == true ||
                                method.DeclaringType?.FullName?.ToLower().Contains(searchTerm) == true ||
                                fullMethodName.Contains(searchTerm) ||
                                methodWithParams.Contains(searchTerm);
                }

                return matches;
            }
            else
            {
                string searchTerm = criteria.Text.ToLower();
                string fullMethodName = $"{method.DeclaringType?.FullName}.{method.Name}".ToLower();
                string methodWithParams = GetMethodSignature(method).ToLower();

                return method.Name.ToLower().Contains(searchTerm) ||
                        method.DeclaringType?.Name.ToLower().Contains(searchTerm) == true ||
                        method.DeclaringType?.Namespace?.ToLower().Contains(searchTerm) == true ||
                        method.DeclaringType?.FullName?.ToLower().Contains(searchTerm) == true ||
                        fullMethodName.Contains(searchTerm) ||
                        methodWithParams.Contains(searchTerm);
            }
        }

        private void AddPatchesToList(Patch[] patches, MethodBase targetMethod, string patchType)
        {
            if (patches == null) return;
            foreach (var patch in patches)
            {
                var patchMethod = patch.PatchMethod;
                var assembly = patchMethod.DeclaringType?.Assembly;

                var patchInfo = new PatchInfo
                {
                    MethodName = targetMethod.Name,
                    TargetType = targetMethod.DeclaringType?.FullName ?? "Unknown",
                    PatcherAssembly = assembly?.GetName().Name ?? "Unknown",
                    PatchType = patchType,
                    FilePath = GetAssemblyFilePath(assembly),
                    PatcherNamespace = patchMethod.DeclaringType?.Namespace ?? "Unknown",
                    TargetMethod = targetMethod,
                    IsEnabled = true
                };

                _foundPatches.Add(patchInfo);
            }

            string methodKey = GetMethodSignature(targetMethod);
            if (_opPatchStates.TryGetValue(methodKey, out var storedPatches))
            {
                foreach (var storedPatch in storedPatches)
                {
                    if (!storedPatch.IsEnabled && storedPatch.PatchType == patchType)
                    {
                        bool alreadyAdded = _foundPatches.Any(fp => fp.TargetMethod == targetMethod && fp.PatchType == patchType && fp.PatcherNamespace == storedPatch.PatcherNamespace);

                        if (!alreadyAdded)
                        {
                            var assembly = storedPatch.PatchMethod.DeclaringType?.Assembly;
                            var patchInfo = new PatchInfo
                            {
                                MethodName = targetMethod.Name,
                                TargetType = targetMethod.DeclaringType?.FullName ?? "Unknown",
                                PatcherAssembly = assembly?.GetName().Name ?? "Unknown",
                                PatchType = patchType,
                                FilePath = GetAssemblyFilePath(assembly),
                                PatcherNamespace = storedPatch.PatcherNamespace,
                                TargetMethod = targetMethod,
                                IsEnabled = false
                            };

                            _foundPatches.Add(patchInfo);
                        }
                    }
                }
            }
        }

        private static string GetAssemblyFilePath(Assembly assembly)
        {
            try
            {
                if (assembly != null)
                {
                    return assembly.Location;
                }
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
            }

            return "Dynamic Assembly";
        }

        private void OpenILViewer(MethodBase method)
        {
            if (method == null) return;

            if (_ilViewerWindows.Any(w => w.Method == method))
                return;

            try
            {
                string opIL = IL.DisassembleMethod(method);

                string methodKey = GetMethodSignature(method);
                if (_opPatchStates.TryGetValue(methodKey, out var patchMethods))
                {
                    RefreshPatchListInternal(method, patchMethods);
                }
                else
                {
                    patchMethods = new List<PatchMethodInfo>();
                    var patchInfo = Harmony.GetPatchInfo(method);

                    if (patchInfo != null)
                    {
                        if (patchInfo.Prefixes != null)
                        {
                            foreach (var patch in patchInfo.Prefixes)
                            {
                                patchMethods.Add(new PatchMethodInfo
                                {
                                    PatchType = "Prefix",
                                    PatchMethod = patch.PatchMethod,
                                    PatcherNamespace = patch.PatchMethod.DeclaringType?.Namespace ?? "Unknown",
                                    ILCode = IL.DisassembleMethod(patch.PatchMethod),
                                    Priority = patch.priority,
                                    IsEnabled = true,
                                    HarmonyPatch = new HarmonyMethod(patch.PatchMethod),
                                    HarmonyId = GetHarmonyIdFromPatch(patch.PatchMethod)
                                });
                            }
                        }

                        if (patchInfo.Postfixes != null)
                        {
                            foreach (var patch in patchInfo.Postfixes)
                            {
                                patchMethods.Add(new PatchMethodInfo
                                {
                                    PatchType = "Postfix",
                                    PatchMethod = patch.PatchMethod,
                                    PatcherNamespace = patch.PatchMethod.DeclaringType?.Namespace ?? "Unknown",
                                    ILCode = IL.DisassembleMethod(patch.PatchMethod),
                                    Priority = patch.priority,
                                    IsEnabled = true,
                                    HarmonyPatch = new HarmonyMethod(patch.PatchMethod),
                                    HarmonyId = GetHarmonyIdFromPatch(patch.PatchMethod)
                                });
                            }
                        }


                        if (patchInfo.Transpilers != null)
                        {
                            foreach (var patch in patchInfo.Transpilers)
                            {
                                patchMethods.Add(new PatchMethodInfo
                                {
                                    PatchType = "Transpiler",
                                    PatchMethod = patch.PatchMethod,
                                    PatcherNamespace = patch.PatchMethod.DeclaringType?.Namespace ?? "Unknown",
                                    ILCode = IL.DisassembleMethod(patch.PatchMethod),
                                    Priority = patch.priority,
                                    IsEnabled = true,
                                    HarmonyPatch = new HarmonyMethod(patch.PatchMethod),
                                    HarmonyId = GetHarmonyIdFromPatch(patch.PatchMethod)
                                });
                            }
                        }


                        if (patchInfo.Finalizers != null)
                        {
                            foreach (var patch in patchInfo.Finalizers)
                            {
                                patchMethods.Add(new PatchMethodInfo
                                {
                                    PatchType = "Finalizer",
                                    PatchMethod = patch.PatchMethod,
                                    PatcherNamespace = patch.PatchMethod.DeclaringType?.Namespace ?? "Unknown",
                                    ILCode = IL.DisassembleMethod(patch.PatchMethod),
                                    Priority = patch.priority,
                                    IsEnabled = true,
                                    HarmonyPatch = new HarmonyMethod(patch.PatchMethod),
                                    HarmonyId = GetHarmonyIdFromPatch(patch.PatchMethod)
                                });
                            }
                        }
                    }

                    _opPatchStates[methodKey] = patchMethods;
                }

                var window = new ILViewerWindow(_nextWindowId++, method, opIL, patchMethods);
                _ilViewerWindows.Add(window);
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
            }
        }

        private static void RefreshPatchListInternal(MethodBase method, List<PatchMethodInfo> storedPatches)
        {
            try
            {
                var currentPatchInfo = Harmony.GetPatchInfo(method);
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

                foreach (var patch in storedPatches)
                {
                    patch.IsEnabled = currentPatches.Contains(patch.PatchMethod as MethodInfo);
                }
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
            }
        }


        private static void AddPatchHInfo(Patches patchInfo, List<PatchMethodInfo> patchMethods)
        {
            if (patchInfo.Prefixes != null)
            {
                foreach (var patch in patchInfo.Prefixes)
                {
                    patchMethods.Add(new PatchMethodInfo
                    {
                        PatchType = "Prefix",
                        PatchMethod = patch.PatchMethod,
                        PatcherNamespace = patch.PatchMethod.DeclaringType?.Namespace ?? "Unknown",
                        Priority = patch.priority,
                        IsEnabled = true,
                        HarmonyPatch = new HarmonyMethod(patch.PatchMethod),
                        HarmonyId = GetHarmonyIdFromPatch(patch.PatchMethod)
                    });
                }
            }

            if (patchInfo.Postfixes != null)
            {
                foreach (var patch in patchInfo.Postfixes)
                {
                    patchMethods.Add(new PatchMethodInfo
                    {
                        PatchType = "Postfix",
                        PatchMethod = patch.PatchMethod,
                        PatcherNamespace = patch.PatchMethod.DeclaringType?.Namespace ?? "Unknown",
                        Priority = patch.priority,
                        IsEnabled = true,
                        HarmonyPatch = new HarmonyMethod(patch.PatchMethod),
                        HarmonyId = GetHarmonyIdFromPatch(patch.PatchMethod)
                    });
                }
            }
            if (patchInfo.Transpilers != null)
            {
                foreach (var patch in patchInfo.Transpilers)
                {
                    patchMethods.Add(new PatchMethodInfo
                    {
                        PatchType = "Transpiler",
                        PatchMethod = patch.PatchMethod,
                        PatcherNamespace = patch.PatchMethod.DeclaringType?.Namespace ?? "Unknown",
                        Priority = patch.priority,
                        IsEnabled = true,
                        HarmonyPatch = new HarmonyMethod(patch.PatchMethod),
                        HarmonyId = GetHarmonyIdFromPatch(patch.PatchMethod)
                    });
                }
            }
            if (patchInfo.Finalizers != null)
            {
                foreach (var patch in patchInfo.Finalizers)
                {
                    patchMethods.Add(new PatchMethodInfo
                    {
                        PatchType = "Finalizer",
                        PatchMethod = patch.PatchMethod,
                        PatcherNamespace = patch.PatchMethod.DeclaringType?.Namespace ?? "Unknown",
                        Priority = patch.priority,
                        IsEnabled = true,
                        HarmonyPatch = new HarmonyMethod(patch.PatchMethod),
                        HarmonyId = GetHarmonyIdFromPatch(patch.PatchMethod)
                    });
                }
            }
        }

        private void TogglePatchDirect(PatchInfo patch, bool enable)
        {
            try
            {
                string methodKey = GetMethodSignature(patch.TargetMethod);

                if (!_opPatchStates.ContainsKey(methodKey))
                {
                    var patchMethods = new List<PatchMethodInfo>();
                    var harmonyPatchInfo = Harmony.GetPatchInfo(patch.TargetMethod);

                    if (harmonyPatchInfo != null)
                    {
                        AddPatchHInfo(harmonyPatchInfo, patchMethods);
                    }

                    _opPatchStates[methodKey] = patchMethods;
                }

                var patchMethodInfo = _opPatchStates[methodKey].FirstOrDefault(e => e.PatchType == patch.PatchType && e.PatcherNamespace == patch.PatcherNamespace);

                if (patchMethodInfo != null)
                {
                    var harmonyId = patchMethodInfo.HarmonyId ?? "harmony.patch.inspector.temp";
                    var harmony = new Harmony(harmonyId);

                    if (enable && !patchMethodInfo.IsEnabled)
                    {
                        switch (patchMethodInfo.PatchType)
                        {
                            case "Prefix":
                                harmony.Patch(patch.TargetMethod, prefix: patchMethodInfo.HarmonyPatch);
                                break;
                            case "Postfix":
                                harmony.Patch(patch.TargetMethod, postfix: patchMethodInfo.HarmonyPatch);
                                break;
                            case "Transpiler":
                                harmony.Patch(patch.TargetMethod, transpiler: patchMethodInfo.HarmonyPatch);
                                break;
                            case "Finalizer":
                                harmony.Patch(patch.TargetMethod, finalizer: patchMethodInfo.HarmonyPatch);
                                break;
                        }

                        patchMethodInfo.IsEnabled = true;
                        patch.IsEnabled = true;
                    }
                    else if (!enable && patchMethodInfo.IsEnabled)
                    {
                        harmony.Unpatch(patch.TargetMethod, patchMethodInfo.PatchMethod as MethodInfo);
                        patchMethodInfo.IsEnabled = false;
                        patch.IsEnabled = false;
                    }
                }
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
            }
        }

        private static string GetHarmonyIdFromPatch(MethodBase patchMethod)
        {
            try
            {
                var assembly = patchMethod.DeclaringType?.Assembly;
                if (assembly != null)
                {
                    if (assembly.GetCustomAttributes(typeof(BepInPlugin), false).FirstOrDefault() is BepInPlugin bepinPluginAttr)
                    {
                        return bepinPluginAttr.GUID;
                    }

                    return assembly.GetName().Name;
                }

                return patchMethod.DeclaringType?.FullName ?? "unknown.harmony.id";
            }
            catch (Exception)
            {
                return "unknown.harmony.id";
            }
        }

        private static string GetMethodSignature(MethodBase method)
        {
            try
            {
                var parameters = method.GetParameters();
                var paramTypes = parameters.Select(p => p.ParameterType.Name).ToArray();
                var paramString = parameters.Length > 0 ? $"({string.Join(", ", paramTypes)})" : "()";
                return $"{method.DeclaringType?.FullName}.{method.Name}{paramString}";
            }
            catch
            {
                return $"{method.DeclaringType?.FullName}.{method.Name}";
            }
        }
    }
}