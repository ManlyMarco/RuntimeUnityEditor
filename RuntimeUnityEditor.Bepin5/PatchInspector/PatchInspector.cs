using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx;
using HarmonyLib;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Bepin5.PatchInspector
{
	public class PatchInspector : Window<PatchInspector>
	{
		private string searchInput = String.Empty;
		private Vector2 scrollPos;
		private List<PatchInfo> foundPatches = new List<PatchInfo>();
		private bool showFilePaths = true;

		private int nextWindowId = 13000;
		private Dictionary<string, ILViewerWindow> ilViewerWindows = new Dictionary<string, ILViewerWindow>();
		private Dictionary<string, List<PatchMethodInfo>> opPatchStates = new Dictionary<string, List<PatchMethodInfo>>();
		protected override void Initialize(InitSettings initSettings)
		{
			Enabled = false;
			DisplayName = "Patch Inspector";
			Title = "Patch Inspector";
			WindowRect = new Rect(50, 50, 700, 600);
		}

		protected override void OnVisibleChanged(bool visible)
		{
			if (visible)
			{
				searchInput = string.Empty;
				foundPatches.Clear();
			}
			base.OnVisibleChanged(visible);
		}

		protected override void OnGUI()
		{
			foreach (var window in ilViewerWindows)
			{
				if (window.Value.IsOpen)
				{
					window.Value.WindowRect = GUI.Window(window.Value.WindowId, window.Value.WindowRect, (GUI.WindowFunction)(id => DrawILViewerWindow(id, window.Value)),
						$"IL Code: {window.Value.Method.DeclaringType?.Name}.{window.Value.Method.Name}");
					if (window.Value.WindowRect.Contains(Event.current.mousePosition))
					{
						Input.ResetInputAxes();
					}
				}
				else
				{
					ilViewerWindows.Remove(window.Key);
					return;
				}
			}
			base.OnGUI();
		}

		protected override void DrawContents()
		{
			GUILayout.BeginVertical();

			GUILayout.Label("Search for patches by method, class, or namespace:", GUI.skin.label);
			GUILayout.Label("Examples: 'OnClick', 'method:OnClick class:AddButtonCtrl', 'namespace:SimpleGame'");

			GUILayout.BeginHorizontal();
			GUILayout.Label("Search:", GUILayout.Width(60));

			string newSearchInput = GUILayout.TextField(searchInput);
			if (newSearchInput != searchInput)
			{
				searchInput = newSearchInput;
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
				searchInput = string.Empty;
				foundPatches.Clear();
			}
			GUILayout.EndHorizontal();

			showFilePaths = GUILayout.Toggle(showFilePaths, "Show file paths");

			GUILayout.Space(10);

			if (foundPatches.Count > 0)
			{
				GUILayout.Label($"Found {foundPatches.Count} patches:");
				scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));

				foreach (var patch in foundPatches)
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

					if (showFilePaths && !string.IsNullOrEmpty(patch.FilePath))
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
			else if (!string.IsNullOrEmpty(searchInput))
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
			foundPatches.Clear();

			string searchTerm = searchInput.Trim();

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

				foundPatches = foundPatches.OrderBy(info => info.TargetType).ThenBy(info => info.MethodName).ToList();
			}
			catch
			{
			}
		}

		private SearchCriteria ParseSearchInput(string input)
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

		private bool MatchesSearchCriteria(MethodBase method, SearchCriteria criteria)
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

				foundPatches.Add(patchInfo);
			}

			string methodKey = GetMethodSignature(targetMethod);
			if (opPatchStates.ContainsKey(methodKey))
			{
				var storedPatches = opPatchStates[methodKey];
				foreach (var storedPatch in storedPatches)
				{
					if (!storedPatch.IsEnabled && storedPatch.PatchType == patchType)
					{
						bool alreadyAdded = foundPatches.Any(fp => fp.TargetMethod == targetMethod && fp.PatchType == patchType && fp.PatcherNamespace == storedPatch.PatcherNamespace);

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

							foundPatches.Add(patchInfo);
						}
					}
				}
			}
		}

		private string GetAssemblyFilePath(Assembly assembly)
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

			string methodKey = GetMethodSignature(method);

			if (ilViewerWindows.ContainsKey(methodKey) && ilViewerWindows[methodKey].IsOpen)
				return;

			try
			{
				string opIL = IL.DisassembleMethod(method);
				List<PatchMethodInfo> patchMethods;

				if (opPatchStates.ContainsKey(methodKey))
				{
					patchMethods = opPatchStates[methodKey];
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

					opPatchStates[methodKey] = patchMethods;
				}

				var window = new ILViewerWindow(nextWindowId++, method, opIL, patchMethods);
				ilViewerWindows[methodKey] = window;
			}
			catch (Exception e)
			{
				RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
			}
		}

		private void RefreshPatchListInternal(MethodBase method, List<PatchMethodInfo> storedPatches)
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

		private void DrawILViewerWindow(int windowId, ILViewerWindow window)
		{
			GUILayout.BeginVertical();
			GUILayout.Label($"Method: {window.Method.DeclaringType?.FullName}.{window.Method.Name}");
			GUILayout.Label($"Parameters: {GetMethodParameters(window.Method)}");
			GUILayout.Label($"Return Type: {GetReturnType(window.Method)}");

			GUILayout.Space(10);

			GUILayout.BeginHorizontal();

			bool opSelected = window.CurrentView == ILViewMode.Original;
			if (opSelected) GUI.color = Color.yellow;
			if (GUILayout.Button("Original Method", GUILayout.Height(30)))
			{
				window.CurrentView = ILViewMode.Original;
				window.ScrollPosition = Vector2.zero;
			}
			if (opSelected) GUI.color = Color.white;

			bool patchMethodsSelected = window.CurrentView == ILViewMode.PatchMethods;
			if (patchMethodsSelected) GUI.color = Color.yellow;
			if (GUILayout.Button($"Patch Manager ({window.PatchMethods.Count})", GUILayout.Height(30)))
			{
				window.CurrentView = ILViewMode.PatchMethods;
				window.ScrollPosition = Vector2.zero;
			}
			if (patchMethodsSelected) GUI.color = Color.white;

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Close", GUILayout.Width(60), GUILayout.Height(30)))
				window.IsOpen = false;

			GUILayout.EndHorizontal();

			GUILayout.Space(5);

			switch (window.CurrentView)
			{
				case ILViewMode.Original:
					DrawOriginalMethodView(window);
					break;
				case ILViewMode.PatchMethods:
					DrawPatchManagerView(window);
					break;
			}

			GUILayout.EndVertical();
			window.WindowRect = IMGUIUtils.DragResizeEat(windowId, window.WindowRect);
		}

		private void DrawOriginalMethodView(ILViewerWindow window)
		{
			GUILayout.Label("Original Method IL Code:");
			window.ScrollPosition = GUILayout.BeginScrollView(window.ScrollPosition, GUILayout.ExpandHeight(true));
			GUILayout.TextArea(window.OriginalIL);
			GUILayout.EndScrollView();
		}

		private void DrawPatchManagerView(ILViewerWindow window)
		{
			if (window.PatchMethods.Count == 0)
			{
				GUILayout.Label("No patches found for this method.");
				return;
			}

			GUILayout.BeginHorizontal();
			GUILayout.BeginVertical(GUILayout.Width(400));
			GUILayout.Label("Patches:");

			if (GUILayout.Button("Refresh Patch List", GUILayout.Height(25)))
				RefreshPatchList(window);

			window.PatchListScrollPosition = GUILayout.BeginScrollView(window.PatchListScrollPosition, GUILayout.ExpandHeight(true));

			for (var i = 0; i < window.PatchMethods.Count; i++)
			{
				var patch = window.PatchMethods[i];

				GUILayout.BeginVertical("box");
				GUILayout.BeginHorizontal();

				bool newEnabled = GUILayout.Toggle(patch.IsEnabled, "", GUILayout.Width(20));
				if (newEnabled != patch.IsEnabled)
				{
					TogglePatch(window, i, newEnabled);
					SearchPatches(); // not needed?
				}

				GUILayout.BeginVertical();

				bool isSelected = window.SelectedPatchIndex == i;
				if (isSelected) GUI.color = Color.cyan;

				if (GUILayout.Button($"{patch.PatchType}: {patch.PatchMethod.DeclaringType?.Name}.{patch.PatchMethod.Name}", "label"))
				{
					window.SelectedPatchIndex = i;
					window.ScrollPosition = Vector2.zero;
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

			if (window.SelectedPatchIndex >= 0 && window.SelectedPatchIndex < window.PatchMethods.Count)
			{
				var selectedPatch = window.PatchMethods[window.SelectedPatchIndex];
				GUILayout.Label($"IL Code for: {selectedPatch.PatchType} - {selectedPatch.PatchMethod.DeclaringType?.Name}.{selectedPatch.PatchMethod.Name}", GUI.skin.label);

				window.ScrollPosition = GUILayout.BeginScrollView(window.ScrollPosition, GUILayout.ExpandHeight(true));

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

		private void AddPatchHInfo(Patches patchInfo, List<PatchMethodInfo> patchMethods)
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

				if (!opPatchStates.ContainsKey(methodKey))
				{
					var patchMethods = new List<PatchMethodInfo>();
					var harmonyPatchInfo = Harmony.GetPatchInfo(patch.TargetMethod);

					if (harmonyPatchInfo != null)
					{
						AddPatchHInfo(harmonyPatchInfo, patchMethods);
					}

					opPatchStates[methodKey] = patchMethods;
				}

				var patchMethodInfo = opPatchStates[methodKey].FirstOrDefault(e => e.PatchType == patch.PatchType && e.PatcherNamespace == patch.PatcherNamespace);

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

		private void TogglePatch(ILViewerWindow window, int patchIndex, bool enable)
		{
			if (patchIndex < 0 || patchIndex >= window.PatchMethods.Count)
				return;

			var patch = window.PatchMethods[patchIndex];

			try
			{
				if (enable && !patch.IsEnabled)
				{
					var harmony = new Harmony(patch.HarmonyId ?? "harmony.patch.inspector.temp");

					switch (patch.PatchType)
					{
						case "Prefix":
							harmony.Patch(window.Method, prefix: patch.HarmonyPatch);
							break;
						case "Postfix":
							harmony.Patch(window.Method, postfix: patch.HarmonyPatch);
							break;
						case "Transpiler":
							harmony.Patch(window.Method, transpiler: patch.HarmonyPatch);
							break;
						case "Finalizer":
							harmony.Patch(window.Method, finalizer: patch.HarmonyPatch);
							break;
					}

					patch.IsEnabled = true;
				}
				else if (!enable && patch.IsEnabled)
				{
					var harmony = new Harmony(patch.HarmonyId ?? "harmony.patch.inspector.temp");
					harmony.Unpatch(window.Method, patch.PatchMethod as MethodInfo);
					patch.IsEnabled = false;
				}
			}
			catch (Exception e)
			{
				patch.IsEnabled = !enable;
				RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
			}
		}

		private void RefreshPatchList(ILViewerWindow window)
		{
			try
			{
				var currentPatchInfo = Harmony.GetPatchInfo(window.Method);
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

				foreach (var patch in window.PatchMethods)
				{
					patch.IsEnabled = currentPatches.Contains(patch.PatchMethod as MethodInfo);
				}
			}
			catch (Exception e)
			{
				RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
			}
		}

		private string GetHarmonyIdFromPatch(MethodBase patchMethod)
		{
			try
			{
				var assembly = patchMethod.DeclaringType?.Assembly;
				if (assembly != null)
				{
					var bepinPluginAttr = assembly.GetCustomAttributes(typeof(BepInPlugin), false).FirstOrDefault() as BepInPlugin;
					if (bepinPluginAttr != null)
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
		
		private string GetMethodSignature(MethodBase method)
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

		private string GetMethodParameters(MethodBase method)
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

		private string GetReturnType(MethodBase method)
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