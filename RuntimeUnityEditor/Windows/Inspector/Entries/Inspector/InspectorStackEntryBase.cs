using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public abstract class InspectorStackEntryBase
    {
        public InspectorStackEntryBase(string name)
        {
            Name = name;
        }

        private string _searchString = string.Empty;
        public string SearchString
        {
            get => _searchString;
            // The string can't be null under unity 5.x or we crash
            set => _searchString = value ?? string.Empty;
        }

        public string Name { get; }

        public abstract bool EntryIsValid();

        public override string ToString()
        {
            return Name;
        }

        public Vector2 ScrollPosition;
    }
}
