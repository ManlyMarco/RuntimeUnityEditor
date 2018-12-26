namespace RuntimeUnityEditor.Inspector.Entries
{
    public abstract class InspectorStackEntryBase
    {
        public InspectorStackEntryBase(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public abstract bool EntryIsValid();
    }
}
