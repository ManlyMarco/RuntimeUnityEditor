using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <summary>
    /// Base class for all inspector stack entries.
    /// </summary>
    public abstract class InspectorStackEntryBase
    {
        /// <summary>
        /// Constructor for the inspector stack entry.
        /// </summary>
        public InspectorStackEntryBase(string name)
        {
            Name = name;
        }

        private string _searchString = string.Empty;
        /// <summary>
        /// Search string for filtering the inspector stack entries.
        /// </summary>
        public string SearchString
        {
            get => _searchString;
            // The string can't be null under unity 5.x or we crash
            set => _searchString = value ?? string.Empty;
        }

        /// <summary>
        /// Name of the entry.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// If the entry can be displayed in the inspector.
        /// </summary>
        public abstract bool EntryIsValid();

        /// <inheritdoc />
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// The position of the scroll view in the inspector.
        /// </summary>
        public Vector2 ScrollPosition;

        /// <summary>
        /// Open context menu for this entry.
        /// </summary>
        public abstract void ShowContextMenu();
    }
}
