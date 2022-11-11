using RuntimeUnityEditor.Core.Inspector.Entries;

namespace RuntimeUnityEditor.Core.Inspector
{
    internal class InspectorHelpObject
    {
        private InspectorHelpObject()
        { }

        public static InspectorStackEntryBase Create()
        {
            var obj = new InspectorHelpObject();
            return new InstanceStackEntry(obj, "Inspector Help");
        }

        public string Help1 = "This window displays contents of game classes in real time. " +
                              "You can go deeper by clicking on the buttons with member names on the left (here it's 'Help1'), " +
                              "and in some cases you can edit the data (try clicking on this text and typing)." +
                              "To apply changes you have to click outside of the field.";

        public string Help2 = "When you go deeper you can return to the original class by clicking on the history list above.";

        public string Help3 = "On the far left you can see the type that a member holds. In this case this is a string. Only some types can be edited." +
                              "You can copy the value even if it can't be edited.";

        public string Help4 = "You can run methods by clicking their names on the list. Try clicking on GetHashCode below to run it. " +
                              "Return value will appear. If a class is returned by the method click the method's name to open it (try S/Create method below).";
        public string Help5 = "WARNING - Running methods arbitraliy can have unforeseen consequences! You might even crash the game in some cases!";

        public string Help6 = "If something goes wrong while getting a member's value, you will see an EXCEPTION or ERROR message in the value field." +
                              "Usually you can click on the member's name again to view details of the error.";

        public string Help7 = "Above you can find various objects to display in the inspector. " +
                              "WARNING - If you search a heavily populated scene, you will get A LOT of results. Your FPS will drop, that's normal.";

        public string Help8 = "'IS ENUMERABLE' means that opening the member will give you anywhere from 0 to infinitely many values. They might even be generated as you view them. " +
                              "If the number of values is known, it will be displayed instead of this text.";

        public string Help9 = "If REPL is supported (the C# command prompt), you can pull and push object to and from inspector by using the 'geti()' and 'set(obj)' commands. Type 'help' in REPL for more info.";

        public static string HelpS = "If a member name has an S/ in front of it, it means that this is a static member. It will be the same in all instances of an object.";

        public string HelpTabs = "Right click on any member to open it in a tab. Right click a tab to close it.";
    }
}