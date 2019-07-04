using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public abstract class InspectorStackEntryBase
    {
        public InspectorStackEntryBase(string name)
        {
            Name = name;
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
