using UnityEngine;

namespace RuntimeUnityEditor.Core.REPL {
    internal struct Suggestion
    {
        public readonly string Prefix;
        public readonly string Addition;
        public string Full => Prefix + Addition;
        public Suggestion(string addition, string prefix, SuggestionKind kind)
        {
            Prefix = prefix;
            Kind = kind;
            Addition = addition;
        }

        public readonly SuggestionKind Kind;

        public Color GetTextColor()
        {
            return Kind == SuggestionKind.Namespace ? Color.gray : Color.white;
        }
    }
}