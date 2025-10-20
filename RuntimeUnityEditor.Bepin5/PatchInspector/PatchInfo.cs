using System.Reflection;
using HarmonyLib;

namespace RuntimeUnityEditor.Bepin5.PatchInspector
{
	internal class PatchInfo
	{
		public Patch Patch;
		public string TargetMethodName;
		public string TargetType;
		public string PatcherAssembly;
		public string PatcherNamespace;
		public string PatchType;
		public string FilePath;
		public MethodBase TargetMethod;
		public bool IsEnabled;
	}
}