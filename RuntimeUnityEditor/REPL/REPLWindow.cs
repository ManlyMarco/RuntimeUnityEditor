using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RuntimeUnityEditor.Core.REPL.MCS;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.REPL
{
    public sealed class ReplWindow
    {
        private static readonly char[] InputSplitChars = { ',', ';', '<', '>', '(', ')', '[', ']', '=', '|', '&' };

        private const int HistoryLimit = 50;

        private readonly ScriptEvaluator _evaluator;

        private readonly List<string> _history = new List<string>();
        private int _historyPosition;

        private readonly StringBuilder _sb = new StringBuilder();

        private string _inputField = "";
        private string _prevInput = "";
        private Vector2 _scrollPosition = Vector2.zero;

        private readonly int _windowId;
        private Rect _windowRect;
        private TextEditor _textEditor;
        private int _newCursorLocation = -1;

        private readonly HashSet<string> _namespaces;
        private readonly List<Suggestion> _suggestions = new List<Suggestion>();

        public ReplWindow()
        {
            _windowId = GetHashCode();

            _sb.AppendLine("Welcome to C# REPL (read-evaluate-print loop)! Enter \"help\" to get a list of common methods.");

            _evaluator = new ScriptEvaluator(new StringWriter(_sb)) { InteractiveBaseClass = typeof(REPL) };

            var envSetup = new string[]
            {
                "using System;",
                "using UnityEngine;",
                "using System.Linq;",
                "using System.Collections;",
                "using System.Collections.Generic;",
            };

            foreach (var define in envSetup)
                Evaluate(define);

            _namespaces = new HashSet<string>(
                AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x =>
                    {
                        try { return x.GetTypes(); }
                        catch { return Enumerable.Empty<Type>(); }
                    })
                .Where(x => x.IsPublic && !string.IsNullOrEmpty(x.Namespace))
                .Select(x => x.Namespace));
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, $"[REPL] Found {_namespaces.Count} public namespaces");
        }

        public void DisplayWindow()
        {
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

            EditorUtilities.DrawSolidWindowBackground(_windowRect);
            _windowRect = GUILayout.Window(_windowId, _windowRect, WindowFunc, "C# REPL Console");
        }

        private GUIStyle _completionsListingStyle;
        private bool _refocus;
        private int _refocusCursorIndex = -1;
        private int _refocusSelectIndex;

        private void WindowFunc(int id)
        {
            GUILayout.BeginVertical();
            {
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.textArea);
                {
                    GUILayout.FlexibleSpace();

                    if (_suggestions.Count > 0)
                    {
                        foreach (var suggestion in _suggestions)
                        {
                            _completionsListingStyle.normal.textColor = suggestion.GetTextColor();
                            if (!GUILayout.Button(suggestion.Full, _completionsListingStyle, GUILayout.ExpandWidth(true)))
                                continue;
                            AcceptSuggestion(suggestion.Addition);
                            break;
                        }
                    }
                    else
                    {
                        GUILayout.TextArea(_sb.ToString(), GUI.skin.label);
                    }
                }
                GUILayout.EndScrollView();

                GUILayout.BeginHorizontal();
                {
                    GUI.SetNextControlName("replInput");
                    _inputField = GUILayout.TextField(_inputField);

                    if (_refocus)
                    {
                        _refocusCursorIndex = _textEditor.cursorIndex;
                        _refocusSelectIndex = _textEditor.selectIndex;
                        GUI.FocusControl("replInput");
                        _refocus = false;
                    }
                    else if (_refocusCursorIndex >= 0)
                    {
                        _textEditor.cursorIndex = _refocusCursorIndex;
                        _textEditor.selectIndex = _refocusSelectIndex;
                        _refocusCursorIndex = -1;
                    }

                    if (GUILayout.Button("Run", GUILayout.ExpandWidth(false)))
                        AcceptInput();

                    if (GUILayout.Button("History", GUILayout.ExpandWidth(false)))
                    {
                        _sb.AppendLine();
                        _sb.AppendLine("# History of entered commands:");
                        foreach (var h in _history)
                            _sb.AppendLine(h);

                        ScrollToBottom();
                    }

                    if (GUILayout.Button("Clear log", GUILayout.ExpandWidth(false)))
                        _sb.Length = 0;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            CheckReplInput();

            GUI.DragWindow();
        }

        private void AcceptSuggestion(string suggestion)
        {
            _inputField = _inputField.Insert(_textEditor.cursorIndex, suggestion);
            _newCursorLocation = _textEditor.cursorIndex + suggestion.Length;
            ClearSuggestions();
        }

        private object Evaluate(string str)
        {
            object ret = VoidType.Value;
            _evaluator.Compile(str, out var compiled);
            try
            {
                compiled?.Invoke(ref ret);
            }
            catch (Exception e)
            {
                _sb.AppendLine(e.ToString());
            }

            return ret;
        }

        private void FetchHistory(int move)
        {
            _historyPosition += move;
            _historyPosition %= _history.Count;
            if (_historyPosition < 0)
                _historyPosition = _history.Count - 1;

            _inputField = _history[_historyPosition];
        }

        private void FetchSuggestions(string input)
        {
            try
            {
                _suggestions.Clear();

                var completions = _evaluator.GetCompletions(input, out string prefix);
                if (completions != null)
                {
                    if (prefix == null)
                        prefix = input;

                    _suggestions.AddRange(completions
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Select(x => new Suggestion(x, prefix, SuggestionKind.Unknown))
                        //.Where(x => !_namespaces.Contains(x.Full))
                        );
                }

                _suggestions.AddRange(GetNamespaceSuggestions(input).OrderBy(x => x.Full));

                _refocus = true;
            }
            catch (Exception ex)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, "[REPL] " + ex);
                ClearSuggestions();
            }
        }

        private IEnumerable<Suggestion> GetNamespaceSuggestions(string input)
        {
            var trimmedInput = input.Trim();
            if (trimmedInput.StartsWith("using"))
                trimmedInput = trimmedInput.Remove(0, 5).Trim();

            return _namespaces.Where(x => x.StartsWith(trimmedInput) && x.Length > trimmedInput.Length)
                .Select(x => new Suggestion(x.Substring(trimmedInput.Length), x.Substring(0, trimmedInput.Length), SuggestionKind.Namespace));
        }

        private void CheckReplInput()
        {
            if (GUI.GetNameOfFocusedControl() != "replInput")
                return;

            _textEditor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            if (_newCursorLocation >= 0)
            {
                _textEditor.cursorIndex = _newCursorLocation;
                _textEditor.selectIndex = _newCursorLocation;
                _newCursorLocation = -1;
            }

            if (Event.current.isKey && Event.current.keyCode == KeyCode.Return)
            {
                AcceptInput();
                Event.current.Use();
            }

            if (Event.current.isKey)
            {
                if (Event.current.keyCode == KeyCode.UpArrow)
                {
                    FetchHistory(-1);
                    Event.current.Use();
                    ClearSuggestions();
                }
                else if (Event.current.keyCode == KeyCode.DownArrow)
                {
                    FetchHistory(1);
                    Event.current.Use();
                    ClearSuggestions();
                }
            }

            var input = _inputField;
            if (!string.IsNullOrEmpty(input))
            {
                try
                {
                    // Separate input into parts, grab only the part with cursor in it
                    var cursorIndex = _refocusCursorIndex >= 0 ? _refocusCursorIndex : _textEditor.cursorIndex;
                    var start = cursorIndex <= 0 ? 0 : input.LastIndexOfAny(InputSplitChars, cursorIndex - 1) + 1;
                    var end = cursorIndex <= 0 ? input.Length : input.IndexOfAny(InputSplitChars, cursorIndex - 1);
                    if (end < 0 || end < start) end = input.Length;
                    input = input.Substring(start, end - start);
                }
                catch (ArgumentException) { }

                if (input != _prevInput)
                {
                    if (!string.IsNullOrEmpty(input))
                        FetchSuggestions(input);
                }
            }
            else
            {
                ClearSuggestions();
            }

            _prevInput = input;
        }

        private void ClearSuggestions()
        {
            if (_suggestions.Any())
            {
                _suggestions.Clear();
                _refocus = true;
            }
        }

        private void AcceptInput()
        {
            _history.Add(_inputField);
            if (_history.Count > HistoryLimit)
                _history.RemoveRange(0, _history.Count - HistoryLimit);
            _historyPosition = 0;

            if (_inputField.Contains("geti()"))
            {
                try
                {
                    var val = REPL.geti();
                    if (val != null)
                        _inputField = _inputField.Replace("geti()", $"geti<{val.GetType().GetSourceCodeRepresentation()}>()");
                }
                catch (SystemException) { }
            }

            _sb.AppendLine($"> {_inputField}");
            var result = Evaluate(_inputField);
            if (result != null && !Equals(result, VoidType.Value))
                _sb.AppendLine(result.ToString());

            ScrollToBottom();

            _inputField = string.Empty;
            ClearSuggestions();
        }

        private void ScrollToBottom()
        {
            _scrollPosition.y = float.MaxValue;
        }

        private class VoidType
        {
            public static readonly VoidType Value = new VoidType();
            private VoidType() { }
        }

        public void UpdateWindowSize(Rect windowRect)
        {
            _windowRect = windowRect;
        }
    }
}