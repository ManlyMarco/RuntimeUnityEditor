namespace RuntimeUnityEditor.Core.Inspector.Entries {
    /// <summary>
    /// Represents an entry in the inspector stack that holds an instance of an object.
    /// </summary>
    public class InstanceStackEntry : InspectorStackEntryBase
    {
        /// <inheritdoc />
        public InstanceStackEntry(object instance, string name) : this(instance, name, null) { }
        /// <inheritdoc />
        public InstanceStackEntry(object instance, string name, ICacheEntry parent) : base(name)
        {
            Instance = instance;
            Parent = parent;
        }

        /// <summary>
        /// The instance of the object represented by this entry.
        /// </summary>
        public object Instance { get; }

        /// <summary>
        /// The parent entry of this instance, if any.
        /// </summary>
        public ICacheEntry Parent { get; }

        /// <inheritdoc />
        public override bool EntryIsValid()
        {
            return Instance != null;
        }

        /// <inheritdoc />
        public override void ShowContextMenu()
        {
            if (Parent == null)
                ContextMenu.Instance.Show(Instance);
            else
                ContextMenu.Instance.Show(Instance, Parent);
        }
    }
}