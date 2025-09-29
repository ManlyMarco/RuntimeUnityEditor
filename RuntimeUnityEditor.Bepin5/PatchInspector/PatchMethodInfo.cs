using System.Reflection;
using HarmonyLib;

namespace RuntimeUnityEditor.Bepin5.PatchInspector
{
	internal class PatchMethodInfo
	{
		public string PatchType;
		public MethodBase PatchMethod;
		public string PatcherNamespace;
		public string ILCode;
		public int Priority;
		public bool IsEnabled;
		public HarmonyMethod HarmonyPatch;
		public string HarmonyId;
	}
}