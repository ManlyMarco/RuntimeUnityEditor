namespace RuntimeUnityEditor.Core.Inspector.Entries {
    public class InstanceStackEntry : InspectorStackEntryBase
    {
        public InstanceStackEntry(object instance, string name) : base(name)
        {
            Instance = instance;
        }

        public object Instance { get; }

        public override bool EntryIsValid()
        {
            return Instance != null;
        }
    }
}