using System;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <summary>
    /// Represents an entry in the inspector stack that holds a static type.
    /// </summary>
    public class StaticStackEntry : InspectorStackEntryBase
    {
        /// <inheritdoc />
        public StaticStackEntry(Type staticType, string name) : base(name)
        {
            StaticType = staticType;
        }

        /// <summary>
        /// The static type represented by this entry.
        /// </summary>
        public Type StaticType { get; }

        /// <inheritdoc />
        public override bool EntryIsValid()
        {
            return StaticType != null;
        }

        /// <inheritdoc />
        public override void ShowContextMenu()
        {
            ContextMenu.Instance.Show(StaticType);
        }
    }
}