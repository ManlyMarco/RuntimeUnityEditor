using UnityEngine;

namespace RuntimeUnityEditor.REPL.Windows
{
    public sealed class SuggestionsWindow
    {
        public delegate void AcceptSuggestionDelegate(string suggestion);

        private GUIStyle _completionsListingStyle;

        private Vector2 _scrollPosition = Vector2.zero;
        private Rect _screenRect;
        private readonly int _windowId;

        public SuggestionsWindow()
        {
            _windowId = GetHashCode();
        }

        public string Prefix { get; set; }

        public string[] Suggestions { get; set; }

        private bool IsHidden()
        {
            return Suggestions == null || Suggestions.Length == 0;
        }

        public event AcceptSuggestionDelegate SuggestionAccept;

        public void DisplayWindow()
        {
            if (IsHidden()) return;

            if (_completionsListingStyle == null)
            {
                _completionsListingStyle = new GUIStyle(GUI.skin.button)
                {
                    border = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(0, 0, 0, 0),
                    hover = { background = Texture2D.whiteTexture, textColor = Color.black },
                    normal = { background = null },
                    focused = { background = Texture2D.whiteTexture, textColor = Color.black },
                    active = { background = Texture2D.whiteTexture, textColor = Color.black }
                };
            }

            GUILayout.Window(_windowId, _screenRect, WindowFunc, "");
        }

        private void WindowFunc(int id)
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            {
                GUILayout.BeginVertical();
                {
                    foreach (string suggestion in Suggestions)
                    {
                        if (!GUILayout.Button($"{Prefix}{suggestion}", _completionsListingStyle, GUILayout.ExpandWidth(true)))
                            continue;
                        SuggestionAccept?.Invoke(suggestion);
                        break;
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        public void UpdateWindowSize(Rect windowRect)
        {
            _screenRect = windowRect;
        }
    }
}