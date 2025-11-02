using BepInEx;
using HarmonyLib;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ContextMenu = RuntimeUnityEditor.Core.ContextMenu;

namespace RuntimeUnityEditor.Bepin5.PatchInspector
{
    /// <summary>
    /// Window for inspecting and managing Harmony patches applied to methods. 
    /// </summary>
    public class PatchInspector : Window<PatchInspector>
    {
        private readonly List<PatchInfo> _foundPatches = new List<PatchInfo>();
        private readonly List<ILViewerWindow> _ilViewerWindows = new List<ILViewerWindow>();
        private readonly Dictionary<string, List<PatchMethodInfo>> _opPatchStates = new Dictionary<string, List<PatchMethodInfo>>();

        private int _nextWindowId = 13000;
        private Vector2 _scrollPos;
        private string _searchInput = string.Empty;

        private string SearchInput
        {
            get => _searchInput;
            set => _searchInput = value ?? string.Empty;
        }

        /// <inheritdoc />
        protected override void Initialize(InitSettings initSettings)
        {
            Enabled = false;
            DefaultScreenPosition = ScreenPartition.LeftUpper;
            DisplayName = "Patch Inspector";
            Title = "Harmony Patch Inspector";

            ContextMenu.MenuContents.Add(new ContextMenuEntry((GUIContent)null, (o, info) => info != null, null));
            ContextMenu.MenuContents.Add(new ContextMenuEntry("Manage harmony patches", (o, info) => info is MethodBase, (o, info, name) => OpenILViewer((MethodBase)info)));
            ContextMenu.MenuContents.Add(new ContextMenuEntry("Manage harmony patches (get)", (o, info) => info is PropertyInfo p && p.CanRead, (o, info, name) => OpenILViewer(((PropertyInfo)info).GetGetMethod(true))));
            ContextMenu.MenuContents.Add(new ContextMenuEntry("Manage harmony patches (set)", (o, info) => info is PropertyInfo p && p.CanWrite, (o, info, name) => OpenILViewer(((PropertyInfo)info).GetSetMethod(true))));
        }

        /// <inheritdoc />
        protected override void VisibleChanged(bool visible)
        {
            if (visible)
                SearchPatches();

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
                    WindowManager.AdditionalWindows.Remove(window);
                    i--;
                }
            }

            base.OnGUI();
        }

        /// <inheritdoc />
        protected override void DrawContents()
        {
            GUILayout.BeginHorizontal(GUI.skin.box,IMGUIUtils.EmptyLayoutOptions);
            {
                GUILayout.Label("Search: ", IMGUIUtils.LayoutOptionsExpandWidthFalse);

                string newSearchInput = GUILayout.TextField(SearchInput, IMGUIUtils.LayoutOptionsExpandWidthTrue);
                if (newSearchInput != SearchInput)
                {
                    SearchInput = newSearchInput;
                    SearchPatches();
                    return;
                }

                if (GUILayout.Button("Clear", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                {
                    SearchInput = string.Empty;
                    _foundPatches.Clear();
                }
            }
            GUILayout.EndHorizontal();

            if (_foundPatches.Count > 0)
            {
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUI.skin.box);
                GUILayout.Label($"Found {_foundPatches.Count} patches", IMGUIUtils.UpperCenterLabelStyle, IMGUIUtils.EmptyLayoutOptions);

                for (var i = 0; i < _foundPatches.Count; i++)
                {
                    var patch = _foundPatches[i];

                    Color prevColor = GUI.color;
                    GUI.color = patch.IsEnabled ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);

                    GUILayout.BeginVertical(GUI.skin.box, IMGUIUtils.EmptyLayoutOptions);
                    {
                        GUILayout.BeginHorizontal(IMGUIUtils.EmptyLayoutOptions);
                        {
                            bool newEnabled = GUILayout.Toggle(patch.IsEnabled, "", IMGUIUtils.LayoutOptionsExpandWidthFalse);
                            if (newEnabled != patch.IsEnabled)
                                TogglePatchDirect(patch, newEnabled);

                            if (GUILayout.Button(
                                    new GUIContent($"Target: {patch.TargetType}.{patch.TargetMethodName}", null, "Click to view IL and a full list of patches applied to this method. Right click for more options"),
                                    GUI.skin.label, IMGUIUtils.LayoutOptionsExpandWidthTrue))
                            {
                                if (IMGUIUtils.IsMouseRightClick())
                                    ContextMenu.Instance.Show(patch.TargetMethod, null, $"MethodInfo: {patch.TargetType}.{patch.TargetMethodName}", null, null);
                                else
                                    OpenILViewer(patch.TargetMethod);
                            }
                        }
                        GUILayout.EndHorizontal();

                        var patchName = $"Patch: [{patch.PatchType}] {patch.Patch?.PatchMethod?.Name ?? "Not applied"}";

                        if (patch.Patch == null) GUI.enabled = false;
                        if (GUILayout.Button(new GUIContent(patchName, null, "Click to see more information about the patch method. Right click for more options."), GUI.skin.label, IMGUIUtils.EmptyLayoutOptions))
                        {
                            if (IMGUIUtils.IsMouseRightClick())
                                ContextMenu.Instance.Show(patch.Patch.PatchMethod, null, $"MethodInfo: {patch.Patch.PatchMethod?.DeclaringType?.Name}.{patch.Patch.PatchMethod?.Name}", null, null);
                            else
                                Inspector.Instance.Push(new InstanceStackEntry(patch.Patch, patchName), true);
                        }

                        GUI.enabled = true;

                        if (GUILayout.Button(new GUIContent($"Patcher: {patch.PatcherNamespace}", null, "Click to search for types in this namespace."), GUI.skin.label, IMGUIUtils.EmptyLayoutOptions))
                            Inspector.Instance.Push(new InstanceStackEntry(AccessTools.AllTypes().Where(x => x.Namespace == patch.PatcherNamespace).ToArray(), "Types in " + patch.PatcherNamespace), true);

                        if (GUILayout.Button(
                                new GUIContent($"Assembly: {patch.PatcherAssembly}", null,
                                               $"File: {patch.FilePath}\n\nClick to open explorer focused on this file. Right click for to inspect the Assembly instance."), GUI.skin.label, IMGUIUtils.EmptyLayoutOptions))
                        {
                            if (IMGUIUtils.IsMouseRightClick())
                                ContextMenu.Instance.Show(AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == patch.PatcherAssembly), null, "Assembly: " + patch.PatcherAssembly,
                                                          null, null);
                            else
                            {
                                try
                                {
                                    if (!System.IO.File.Exists(patch.FilePath))
                                        throw new Exception("File does not exist on disk: " + (patch.FilePath ?? "NULL"));

                                    // Start explorer focused on the dll
                                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{patch.FilePath}\"");
                                }
                                catch (Exception e)
                                {
                                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, "Failed to open explorer: " + e.Message);
                                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
                                }
                            }
                        }
                    }
                    GUILayout.EndVertical();
                    GUI.color = prevColor;
                }

                GUILayout.EndScrollView();
            }
            else if (!string.IsNullOrEmpty(SearchInput))
            {
                GUILayout.Space(10);
                GUILayout.Label("No patches found.", IMGUIUtils.UpperCenterLabelStyle, IMGUIUtils.EmptyLayoutOptions);
                GUILayout.FlexibleSpace();
            }
            else
            {
                GUILayout.Space(10);
                GUILayout.Label("Use the search box to search for currently applied Harmony patches by method, class, or namespace.\n\nExamples: 'OnClick', 'method:OnClick class:AddButtonCtrl', 'namespace:SimpleGame'", IMGUIUtils.UpperCenterLabelStyle, IMGUIUtils.EmptyLayoutOptions);
                GUILayout.FlexibleSpace();
            }
        }

        private void SearchPatches()
        {
            _foundPatches.Clear();

            string searchTerm = SearchInput.Trim();

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

                _foundPatches.Sort((p1, p2) =>
                {
                    var c1 = string.Compare(p1.TargetType, p2.TargetType, StringComparison.Ordinal);
                    if (c1 != 0)
                        return c1;

                    return string.Compare(p1.TargetMethodName, p2.TargetMethodName, StringComparison.Ordinal);
                });
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
                    Patch = patch,
                    TargetMethodName = targetMethod.Name,
                    TargetType = targetMethod.DeclaringType?.FullName ?? "Unknown",
                    PatcherAssembly = assembly?.GetName().Name ?? "Unknown",
                    PatchType = patchType,
                    FilePath = GetAssemblyFilePath(assembly),
                    PatcherNamespace = patchMethod.DeclaringType?.Namespace ?? "Unknown",
                    TargetMethod = targetMethod,
                    IsEnabled = true,
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
                                TargetMethodName = targetMethod.Name,
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

            var existing = _ilViewerWindows.Find(w => w.Method == method);
            if (existing != null)
            {
                GUI.BringWindowToFront(existing.WindowId);
                return;
            }

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
                WindowManager.AdditionalWindows.Add(window);
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

        internal void TogglePatch(MethodBase targetMethod, PatchMethodInfo patch, bool enable)
        {
            try
            {
                string methodKey = GetMethodSignature(targetMethod);

                if (!_opPatchStates.ContainsKey(methodKey))
                {
                    var patchMethods = new List<PatchMethodInfo>();
                    var harmonyPatchInfo = Harmony.GetPatchInfo(targetMethod);

                    if (harmonyPatchInfo != null)
                    {
                        AddPatchHInfo(harmonyPatchInfo, patchMethods);
                    }

                    _opPatchStates[methodKey] = patchMethods;
                }

                if (enable && !patch.IsEnabled)
                {
                    var harmony = new Harmony(patch.HarmonyId ?? "harmony.patch.inspector.temp");

                    switch (patch.PatchType)
                    {
                        case "Prefix":
                            harmony.Patch(targetMethod, prefix: patch.HarmonyPatch);
                            break;
                        case "Postfix":
                            harmony.Patch(targetMethod, postfix: patch.HarmonyPatch);
                            break;
                        case "Transpiler":
                            harmony.Patch(targetMethod, transpiler: patch.HarmonyPatch);
                            break;
                        case "Finalizer":
                            harmony.Patch(targetMethod, finalizer: patch.HarmonyPatch);
                            break;
                    }

                    patch.IsEnabled = true;
                }
                else if (!enable && patch.IsEnabled)
                {
                    var harmony = new Harmony(patch.HarmonyId ?? "harmony.patch.inspector.temp");
                    harmony.Unpatch(targetMethod, patch.PatchMethod as MethodInfo);
                    patch.IsEnabled = false;
                }
            }
            catch (Exception e)
            {
                //patch.IsEnabled = !enable;
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, $"Failed to {(enable ? "enable" : "disable")} Harmony patch {patch.HarmonyId ?? "<NULL>"}:{patch.HarmonyPatch?.methodName}");
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
            }

            var existing = _foundPatches.Find(fp => fp.TargetMethod == targetMethod && fp.PatchType == patch.PatchType && fp.PatcherNamespace == patch.PatcherNamespace);
            if (existing != null)
            {
                existing.IsEnabled = patch.IsEnabled;
            }
            else
            {
                SearchPatches();
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