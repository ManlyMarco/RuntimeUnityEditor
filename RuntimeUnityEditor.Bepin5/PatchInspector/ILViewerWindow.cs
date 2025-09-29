using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RuntimeUnityEditor.Bepin5.PatchInspector
{
	internal class ILViewerWindow
	{
		public int WindowId;
		public Rect WindowRect;
		public MethodBase Method;
		public string OriginalIL;
		public List<PatchMethodInfo> PatchMethods;
		public Vector2 ScrollPosition;
		public Vector2 PatchListScrollPosition;
		public bool IsOpen;
		public ILViewMode CurrentView;
		public int SelectedPatchIndex;

		public ILViewerWindow(int windowId, MethodBase method, string originalIL, List<PatchMethodInfo> patchMethods)
		{
			WindowId = windowId;
			Method = method;
			OriginalIL = originalIL;
			PatchMethods = patchMethods;
			WindowRect = new Rect(100 + (windowId % 5) * 50, 100 + (windowId % 5) * 50, 900, 650);
			ScrollPosition = Vector2.zero;
			PatchListScrollPosition = Vector2.zero;
			IsOpen = true;
			CurrentView = ILViewMode.Original;
			SelectedPatchIndex = -1;
		}
	}
}