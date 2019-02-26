using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RuntimeUnityEditor.REPL.MCS;
using RuntimeUnityEditor.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.REPL.Windows
{
    public sealed class ReplWindow
    {
        private static readonly char[] InputSplitChars = { ',', ';', '<', '>', '(', ')', '[', ']', '=', '|', '&' };

        private const int HistoryLimit = 50;
        private const int SuggestionsWidth = 200;

        private readonly ScriptEvaluator _evaluator;
        private readonly SuggestionsWindow _suggestionsWindow;

        private readonly List<string> _history = new List<string>();
        private int _historyPosition;

        private readonly StringBuilder _sb = new StringBuilder();

        private string _inputField = "";
        private string _prevInputField = "";
        private Vector2 _scrollPosition = Vector2.zero;

        private readonly int _windowId;
        private Rect _windowRect;

        public ReplWindow()
        {
            _windowId = GetHashCode();

            _sb.AppendLine("Welcome to C# REPL (read-evaluate-print loop)! Enter \"help\" to get a list of common methods.");

            _evaluator = new ScriptEvaluator(new StringWriter(_sb)) { InteractiveBaseClass = typeof(REPL) };

            _suggestionsWindow = new SuggestionsWindow();
            _suggestionsWindow.SuggestionAccept += AcceptSuggestion;
        }

        public void DisplayWindow()
        {
            EditorUtilities.DrawSolidWindowBackground(_windowRect);
            _windowRect = GUILayout.Window(_windowId, _windowRect, WindowFunc, "REPL");

            _suggestionsWindow.UpdateWindowSize(new Rect(_windowRect.x - SuggestionsWidth, _windowRect.y, SuggestionsWidth, _windowRect.height));
            _suggestionsWindow.DisplayWindow();
        }

        private void WindowFunc(int id)
        {
            GUILayout.BeginVertical();
            {
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
                {
                    GUILayout.TextArea(_sb.ToString(), GUILayout.ExpandHeight(true));
                }
                GUILayout.EndScrollView();

                GUILayout.BeginHorizontal();
                {
                    GUI.SetNextControlName("replInput");
                    _prevInputField = _inputField;
                    _inputField = GUILayout.TextField(_inputField);

                    if (GUILayout.Button("Run", GUILayout.ExpandWidth(false)))
                        AcceptInput();

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
            _inputField += suggestion;
            _suggestionsWindow.Suggestions = null;
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

        private void FetchSuggestions(string input, int cursorPos)
        {
            if (!string.IsNullOrEmpty(input))
            {
                var start = input.LastIndexOfAny(InputSplitChars, cursorPos - 1) + 1;
                var end = Mathf.Max(input.Length, input.IndexOfAny(InputSplitChars, cursorPos - 1));
                input = input.Substring(start, end - start);
            }

            try
            {
                _suggestionsWindow.Suggestions = _evaluator.GetCompletions(input, out string prefix);
                _suggestionsWindow.Prefix = prefix;
            }
            catch (Exception)
            {
                _suggestionsWindow.Suggestions = null;
                _suggestionsWindow.Prefix = null;
            }
        }

        private void CheckReplInput()
        {
            if (GUI.GetNameOfFocusedControl() != "replInput")
                return;

            TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            var cursorPos = te.cursorIndex;

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
                    _suggestionsWindow.Suggestions = null;
                    _suggestionsWindow.Prefix = null;
                }
                else if (Event.current.keyCode == KeyCode.DownArrow)
                {
                    FetchHistory(1);
                    Event.current.Use();
                    _suggestionsWindow.Suggestions = null;
                    _suggestionsWindow.Prefix = null;
                }
            }

            if (_inputField != _prevInputField)
                FetchSuggestions(_inputField, cursorPos);
        }

        private void AcceptInput()
        {
            if (_inputField.Contains("geti()"))
            {
                var val = REPL.geti();
                if (val == null)
                {
                    _sb.AppendLine($"> {_inputField}");
                    _sb.AppendLine("Error: No object opened in inspector or a static type is opened");
                    return;
                }

                _inputField = _inputField.Replace("geti()", $"geti<{val.GetType().FullName}>()");
            }

            _sb.AppendLine($"> {_inputField}");
            var result = Evaluate(_inputField);
            if (result != null && !Equals(result, VoidType.Value))
                _sb.AppendLine(result.ToString());

            _scrollPosition.y = float.MaxValue;

            _history.Add(_inputField);
            if (_history.Count > HistoryLimit)
                _history.RemoveRange(0, _history.Count - HistoryLimit);
            _historyPosition = 0;

            _inputField = string.Empty;
            _suggestionsWindow.Suggestions = null;
            _suggestionsWindow.Prefix = null;
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