using System;

namespace RuntimeUnityEditor.Core.Inspector.Entries {
    public class StaticStackEntry : InspectorStackEntryBase
    {
        public StaticStackEntry(Type staticType, string name) : base(name)
        {
            StaticType = staticType;
        }

        public Type StaticType { get; }

        public override bool EntryIsValid()
        {
            return StaticType != null;
        }
    }
}