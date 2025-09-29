using System.Reflection;

namespace RuntimeUnityEditor.Bepin5.PatchInspector
{
	internal struct PatchInfo
	{
		public string MethodName;
		public string TargetType;
		public string PatcherAssembly;
		public string PatcherNamespace;
		public string PatchType;
		public string FilePath;
		public MethodBase TargetMethod;
		public bool IsEnabled;
	}
}