using UnityEngine;

namespace RuntimeUnityEditor.Core.REPL {
    internal struct Suggestion
    {
        public readonly string Original;
        public readonly string Result;
        public Suggestion(string result, string original, SuggestionKind kind)
        {
            Original = original;
            Kind = kind;
            Result = result;
        }

        public readonly SuggestionKind Kind;

        public Color GetTextColor()
        {
            return Kind == SuggestionKind.Namespace ? Color.gray : Color.white;
        }
    }
}