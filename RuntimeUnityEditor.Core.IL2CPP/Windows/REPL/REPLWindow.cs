﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime;
using RuntimeUnityEditor.Core.ChangeHistory;
using RuntimeUnityEditor.Core.REPL.MCS;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core.REPL
{
    /// <summary>
    /// C# REPL console window.
    /// </summary>
    public sealed class ReplWindow : Window<ReplWindow>
    {
        private string _autostartFilename;
        private static readonly char[] _inputSplitChars = { ',', ';', '<', '>', '(', ')', '[', ']', '=', '|', '&' };

        private const int HistoryLimit = 50;

        private ScriptEvaluator _evaluator;

        private readonly List<string> _history = new List<string>();
        private int _historyPosition;

        private readonly StringBuilder _sb = new StringBuilder();

        private string _inputField = "";
        private string _prevInput = "";
        private Vector2 _scrollPosition = Vector2.zero;

        private TextEditor _textEditor;
        private int _newCursorLocation = -1;

        private MemberInfo _cursorIndex;
        private MemberInfo _selectIndex;

        private HashSet<string> _namespaces;
        private HashSet<string> Namespaces
        {
            get
            {
                if (_namespaces == null)
                {
                    _namespaces = new HashSet<string>(
                        AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(Extensions.GetTypesSafe)
                            .Where(x => x.IsPublic && !string.IsNullOrEmpty(x.Namespace))
                            .Select(x => x.Namespace));
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, $"[REPL] Found {_namespaces.Count} public namespaces");
                }
                return _namespaces;
            }
        }

        private readonly List<Suggestion> _suggestions = new List<Suggestion>();

        private const string SnippletSeparator = "/****************************************/";
        private string _snippletFilename;
        private readonly List<string> _savedSnipplets = new List<string>();
        private bool _snippletsShown;


        /// <inheritdoc />
        protected override void Initialize(InitSettings initSettings)
        {
            if (!UnityFeatureHelper.SupportsRepl) throw new InvalidOperationException("mcs is not supported on this Unity version");

            var disable = false;
            initSettings.RegisterSetting("General", "Disable REPL function", false, "Completely turn off REPL even if it's supported. Useful if mcs is causing compatibility issues (e.g. in rare cases it can crash the game when used together with some versions of RuntimeDetours in some Unity versions).", x => disable = x);
            if (disable) throw new InvalidOperationException("REPL is disabled in config");

            var configPath = initSettings.ConfigPath;
            _autostartFilename = Path.Combine(configPath, "RuntimeUnityEditor.Autostart.cs");
            _snippletFilename = Path.Combine(configPath, "RuntimeUnityEditor.Snipplets.cs");
            Title = "C# REPL Console";

            _sb.AppendLine("Welcome to C# REPL (read-evaluate-print loop)! Enter \"help\" to get a list of common methods.");

            _evaluator = new ScriptEvaluator(new StringWriter(_sb)) { InteractiveBaseClass = typeof(REPL) };

            // todo save whole ui state in a generic way
            _onEnabledChanged = initSettings.RegisterSetting("General", "Show REPL console", true, "", x => Enabled = x);

            initSettings.PluginMonoBehaviour.StartCoroutine(DelayedReplSetup().WrapToIl2Cpp());

            DisplayName = "REPL console";
            MinimumSize = new Vector2(280, 130);
            Enabled = false;
            DefaultScreenPosition = ScreenPartition.CenterLower;
        }

        private IEnumerator DelayedReplSetup()
        {
            yield return null;
            try
            {
                RunEnvSetup();
            }
            catch (Exception ex)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "Failed to initialize REPL environment - " + ex.Message);
                try
                {
                    RuntimeUnityEditorCore.Instance.RemoveFeature(this);
                    _initialized = false;
                    _evaluator.Dispose();
                }
                catch (Exception e)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, e);
                }
            }
        }

        /// <summary>
        /// Set up the REPL environment. Sometimes needs to be delayed to some time after plugin instantiation or things fail.
        /// </summary>
        public void RunEnvSetup()
        {
            var envSetup = "using System;" +
                           "using UnityEngine;" +
                           "using System.Linq;" +
                           "using System.Collections;" +
                           "using System.Collections.Generic;";

            Evaluate(envSetup);
            RunAutostart(_autostartFilename);
        }

        private void RunAutostart(string autostartFilename)
        {
            if (File.Exists(autostartFilename))
            {
                var allLines = File.ReadAllLines(autostartFilename).Select(x => x.Trim('\t', ' ', '\r', '\n')).Where(x => !string.IsNullOrEmpty(x) && !x.StartsWith("//")).ToArray();
                if (allLines.Length > 0)
                {
                    var message = "Executing code from " + autostartFilename;
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, message);
                    AppendLogLine(message);
                    foreach (var line in allLines)
                        Evaluate(line);
                }
            }
        }

        private GUIStyle _completionsListingStyle;
        private bool _refocus;
        private int _refocusCursorIndex = -1;
        private int _refocusSelectIndex;

        /// <inheritdoc />
        protected override void DrawContents()
        {
            if (_completionsListingStyle == null)
            {
                _completionsListingStyle = new GUIStyle(GUI.skin.button)
                {
                    border = new RectOffset(),
                    margin = new RectOffset(),
                    padding = new RectOffset(),
                    hover = { background = Texture2D.whiteTexture, textColor = Color.black },
                    normal = { background = null },
                    focused = { background = Texture2D.whiteTexture, textColor = Color.black },
                    active = { background = Texture2D.whiteTexture, textColor = Color.black }
                };
            }

            GUILayout.BeginVertical();
            {
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.textArea);
                {
                    GUILayout.FlexibleSpace();

                    if (_snippletsShown)
                    {
                        if (_savedSnipplets.Count == 0)
                        {
                            GUILayout.Label("This is a list of saved snipplets of code that you can load later into the input field.\n\n" +
                                            "To save a snipplet, type someting in the input field and click the Save button.\n" +
                                            "To load a snipplet, make sure the input field is empty, click the Load button, then choose a snipplet.\n" +
                                            "To remove a snipplet you can edit the snipplet file by choosing the bottom option on the snipplet list.\n\n" +
                                            "Close this menu without loading anything by clicking the Cancel button below.", GUI.skin.box);
                        }
                        else
                        {
                            _completionsListingStyle.normal.textColor = Color.white;
                            foreach (var snipplet in _savedSnipplets)
                            {
                                if (GUILayout.Button(snipplet, GUI.skin.box, GUILayout.ExpandWidth(true)))
                                {
                                    _inputField = snipplet;
                                    _snippletsShown = false;
                                    break;
                                }
                            }

                            if (GUILayout.Button(">> Edit snipplet list in external editor <<", GUI.skin.box, GUILayout.ExpandWidth(true)))
                            {
                                AppendLogLine("Opening snipplet file at " + _snippletFilename);

                                if (!File.Exists(_snippletFilename))
                                    File.WriteAllText(_snippletFilename, "");

                                try { Process.Start(_snippletFilename); }
                                catch (Exception e) { AppendLogLine(e.Message); }
                            }
                        }
                    }
                    else if (_suggestions.Count > 0)
                    {
                        foreach (var suggestion in _suggestions)
                        {
                            _completionsListingStyle.normal.textColor = suggestion.GetTextColor();
                            if (GUILayout.Button(suggestion.Full, _completionsListingStyle, GUILayout.ExpandWidth(true)))
                            {
                                AcceptSuggestion(suggestion.Addition);
                                break;
                            }
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
                    _inputField = GUILayout.TextArea(_inputField);

                    if (_refocus)
                    {
                        _refocusCursorIndex = (int)ReflectionUtils.GetValue(_cursorIndex, _textEditor);
                        _refocusSelectIndex = (int)ReflectionUtils.GetValue(_selectIndex, _textEditor);
                        GUI.FocusControl("replInput");
                        _refocus = false;
                    }
                    else if (_refocusCursorIndex >= 0)
                    {
                        ReflectionUtils.SetValue(_cursorIndex, _textEditor, _refocusCursorIndex);
                        ReflectionUtils.SetValue(_selectIndex, _textEditor, _refocusSelectIndex);

                        _refocusCursorIndex = -1;
                    }

                    if (GUILayout.Button("Run", GUILayout.ExpandWidth(false)))
                        AcceptInput();

                    if (GUILayout.Button(_snippletsShown ? "Cancel" : (_inputField.Length == 0 ? "Load" : "Save"), GUILayout.ExpandWidth(false)))
                    {
                        if (_snippletsShown)
                        {
                            // Cancel/close
                            _snippletsShown = false;
                        }
                        else if (_inputField.Length == 0)
                        {
                            // Load
                            _snippletsShown = true;

                            var items = File.Exists(_snippletFilename)
                                ? File.ReadAllText(_snippletFilename)
                                    .Split(new[] { SnippletSeparator }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(x => x.Replace("\t", "    ").Trim(' ', '\r', '\n'))
                                    .Where(x => x.Length > 0)
                                : new string[0];
                            _savedSnipplets.Clear();
                            _savedSnipplets.AddRange(items);
                        }
                        else
                        {
                            // Save
                            var contents = File.Exists(_snippletFilename) ? $"{_inputField}{Environment.NewLine}{SnippletSeparator}{Environment.NewLine}{File.ReadAllText(_snippletFilename)}" : _inputField;
                            File.WriteAllText(_snippletFilename, contents);
                            AppendLogLine("Saved current command to snipplets. Clear the input box and click Load to load it.");
                        }
                    }

                    if (GUILayout.Button("Autostart", GUILayout.ExpandWidth(false)))
                    {
                        AppendLogLine("Opening autostart file at " + _autostartFilename);

                        if (!File.Exists(_autostartFilename))
                            File.WriteAllText(_autostartFilename, "// This C# code will be executed by the REPL near the end of plugin initialization. Only single-line statements are supported. Use echo(string) to write to REPL log and message(string) to write to global log.\n\n");

                        try { Process.Start(_autostartFilename); }
                        catch (Exception e) { AppendLogLine(e.Message); }

                        ScrollToBottom();
                    }

                    if (GUILayout.Button("History", GUILayout.ExpandWidth(false)))
                    {
                        AppendLogLine("");
                        AppendLogLine("# History of executed commands:");
                        foreach (var h in _history)
                            AppendLogLine(h);

                        ScrollToBottom();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            CheckReplInput();
        }

        private void AcceptSuggestion(string suggestion)
        {
            int cursorIndex = (int)ReflectionUtils.GetValue(_cursorIndex, _textEditor);
            _inputField = _inputField.Insert(cursorIndex, suggestion);
            _newCursorLocation = (int)ReflectionUtils.GetValue(_cursorIndex, _textEditor) + suggestion.Length;
            ClearSuggestions();

            _refocus = true;
            _refocusCursorIndex = cursorIndex + suggestion.Length;
        }

        /// <summary>
        /// Evaluate string as C# code and run it in the REPL environment. Write resulting log into the console.
        /// </summary>
        public object Evaluate(string str)
        {
            object ret = VoidType.Value;
            _evaluator.Compile(str, out var compiled);
            try
            {
                compiled?.Invoke(ref ret);
            }
            catch (Exception e)
            {
                AppendLogLine(e.ToString());
            }

            return ret;
        }

        private void FetchHistory(int move)
        {
            if (_history.Count == 0) return;

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

                // A fix for ? characters causing an infinite loop in GetCompletions
                if (input.IndexOfAny(new[] { '?', '{', '}', '[', ']' }) < 0)
                {
                    // Discard errors when searching for completions
                    var logLen = _sb.Length;
                    var completions = _evaluator.GetCompletions(input, out string prefix);
                    _sb.Length = logLen;
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
                }

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

            return Namespaces.Where(x => x.StartsWith(trimmedInput) && x.Length > trimmedInput.Length)
                .Select(x => new Suggestion(x.Substring(trimmedInput.Length), x.Substring(0, trimmedInput.Length), SuggestionKind.Namespace));
        }

        private void CheckReplInput()
        {
            if (GUI.GetNameOfFocusedControl() != "replInput")
                return;

            _textEditor = GUIUtility.GetStateObject(Il2CppType.Of<TextEditor>(), GUIUtility.keyboardControl).Cast<TextEditor>();

            // Reflection for compatibility with Unity 4.x
            var tEditor = typeof(TextEditor);

            _cursorIndex = tEditor.GetProperty("cursorIndex", BindingFlags.Instance | BindingFlags.Public);
            _selectIndex = tEditor.GetProperty("selectIndex", BindingFlags.Instance | BindingFlags.Public);

            if (_cursorIndex == null && _selectIndex == null)
            {
                _cursorIndex = tEditor.GetField("pos", BindingFlags.Instance | BindingFlags.Public);
                _selectIndex = tEditor.GetField("selectPos", BindingFlags.Instance | BindingFlags.Public);
            }

            if (_newCursorLocation >= 0)
            {
                ReflectionUtils.SetValue(_cursorIndex, _textEditor, _newCursorLocation);
                ReflectionUtils.SetValue(_selectIndex, _textEditor, _newCursorLocation);

                _newCursorLocation = -1;
            }

            var input = _inputField;

            var currentEvent = Event.current;
            if (currentEvent.isKey)
            {
                if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
                {
                    if (!currentEvent.shift)
                    {
                        // Fix pressing enter adding a newline in textarea
                        var index = (int)ReflectionUtils.GetValue(_cursorIndex, _textEditor);

                        if (index - 1 >= 0)
                            _inputField = _inputField.Remove(index - 1, 1);

                        AcceptInput();
                        currentEvent.Use();
                    }
                }
                else if (input == null || !input.Contains('\n')) // todo change to always be alt + up/dn and have arrows for suggestions?
                {
                    if (currentEvent.keyCode == KeyCode.UpArrow)
                    {
                        FetchHistory(-1);
                        currentEvent.Use();
                        ClearSuggestions();
                    }
                    else if (currentEvent.keyCode == KeyCode.DownArrow)
                    {
                        FetchHistory(1);
                        currentEvent.Use();
                        ClearSuggestions();
                    }
                }
            }

            if (!string.IsNullOrEmpty(input))
            {
                try
                {
                    // Separate input into parts, grab only the part with cursor in it
                    var cursorIndex = _refocusCursorIndex >= 0 ? _refocusCursorIndex : (int)ReflectionUtils.GetValue(_cursorIndex, _textEditor);
                    var start = cursorIndex <= 0 ? 0 : input.LastIndexOfAny(_inputSplitChars, cursorIndex - 1) + 1;
                    var end = cursorIndex <= 0 ? input.Length : input.IndexOfAny(_inputSplitChars, cursorIndex - 1);
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
            _inputField = _inputField.Trim();

            if (_inputField == "") return;

            _history.Add(_inputField);
            if (_history.Count > HistoryLimit)
                _history.RemoveRange(0, _history.Count - HistoryLimit);
            _historyPosition = 0;

            Change.Report("(REPL)::" + _inputField);

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

            AppendLogLine($"> {_inputField}");
            var result = Evaluate(_inputField);
            if (result != null && !Equals(result, VoidType.Value))
                AppendLogLine(result.ToString());

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

        internal void AppendLogLine(string message)
        {
            _sb.AppendLine(message);
        }

        /// <summary>
        /// Clear the log.
        /// </summary>
        public void Clear()
        {
            _sb.Length = 0;
        }

        /// <inheritdoc />
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                if (base.Enabled != value)
                {
                    base.Enabled = value;
                    _onEnabledChanged?.Invoke(value);
                }
            }
        }
        private Action<bool> _onEnabledChanged;

        /// <summary>
        /// Use to send an object into the REPL environment. User is given code to load the object in the input field.
        /// </summary>
        public void IngestObject(object obj)
        {
            if (obj == null)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "obj is null in: " + new StackTrace());
                return;
            }

            REPL.InteropTempVar = obj;
            _prevInput = _inputField = $"var {GetUniqueVarName("q")} = ({obj.GetType().GetSourceCodeRepresentation()}){nameof(REPL.InteropTempVar)}";
            ClearSuggestions();
        }

        private string GetUniqueVarName(string baseName)
        {
            var lastVarName = _evaluator.fields.Keys.Where(x => Regex.IsMatch(x, @"^q\d*$", RegexOptions.Singleline)).OrderBy(x => x.Length).ThenBy(x => x).LastOrDefault();
            if (lastVarName != null)
            {
                if (lastVarName.Length > 1)
                {
                    var i = int.Parse(lastVarName.Substring(1));
                    baseName += i + 1;
                }
                else
                {
                    baseName += "1";
                }
            }

            return baseName;
        }
    }
}