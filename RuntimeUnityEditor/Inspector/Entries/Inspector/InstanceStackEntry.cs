namespace RuntimeUnityEditor.Core.Inspector.Entries {
    public class InstanceStackEntry : InspectorStackEntryBase
    {
        public InstanceStackEntry(object instance, string name) : this(instance, name, null) { }
        public InstanceStackEntry(object instance, string name, ICacheEntry parent) : base(name)
        {
            Instance = instance;
            Parent = parent;
        }

        public object Instance { get; }
        public ICacheEntry Parent { get; }

        public override bool EntryIsValid()
        {
            return Instance != null;
        }
    }
}