using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using RuntimeUnityEditor.Inspector.Entries;
using RuntimeUnityEditor.Utils;
using UnityEngine;
using Logger = BepInEx.Logger;

namespace RuntimeUnityEditor.Inspector
{
    public class Inspector
    {
        private const int InspectorRecordHeight = 25;
        private readonly Action<Transform> _treelistShowCallback;
        private readonly GUILayoutOption[] _inspectorTypeWidth = { GUILayout.Width(170), GUILayout.MaxWidth(170) };
        private readonly GUILayoutOption[] _inspectorNameWidth = { GUILayout.Width(240), GUILayout.MaxWidth(240) };
        private readonly GUILayoutOption _inspectorRecordHeight = GUILayout.Height(InspectorRecordHeight);
        private readonly GUILayoutOption _dnSpyButtonOptions = GUILayout.Width(19);

        private GUIStyle _alignedButtonStyle;

        private Rect _inspectorWindowRect;
        private Vector2 _inspectorScrollPos;
        private Vector2 _inspectorStackScrollPos;

        private int _currentVisibleCount;
        private object _currentlyEditingTag;
        private string _currentlyEditingText;
        private bool _userHasHitReturn;

        private readonly Dictionary<Type, bool> _canCovertCache = new Dictionary<Type, bool>();
        private readonly List<ICacheEntry> _fieldCache = new List<ICacheEntry>();
        private readonly Stack<InspectorStackEntry> _inspectorStack = new Stack<InspectorStackEntry>();

        private InspectorStackEntry _nextToPush;

        public Inspector(Action<Transform> treelistShowCallback)
        {
            _treelistShowCallback = treelistShowCallback ?? throw new ArgumentNullException(nameof(treelistShowCallback));
        }

        private void CacheFields(object objectToOpen)
        {
            _inspectorScrollPos = Vector2.zero;
            _fieldCache.Clear();

            if (objectToOpen == null) return;

            var type = objectToOpen.GetType();

            IEnumerable<ICacheEntry> CacheMethods(object instance, MethodInfo[] typesToCheck)
            {
                var cacheItems = typesToCheck
                    .Where(x => !x.IsConstructor && !x.IsSpecialName && x.GetParameters().Length == 0)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Where(x => x.Name != "MemberwiseClone" && x.Name != "obj_address") // Instant game crash
                    .Select(m =>
                    {
                        if (m.ContainsGenericParameters)
                            try
                            {
                                return m.MakeGenericMethod(type);
                            }
                            catch (Exception)
                            {
                                return null;
                            }
                        return m;
                    }).Where(x => x != null)
                    .Select(m => new MethodCacheEntry(instance, m)).Cast<ICacheEntry>();
                return cacheItems;
            }

            try
            {
                CallbackCacheEntey<Action> CreateTransfromCallback(Transform tr)
                {
                    return new CallbackCacheEntey<Action>("Open in Scene Object Browser",
                        "Navigate to this object in the Scene Object Browser",
                        () =>
                        {
                            _treelistShowCallback(tr);
                            return null;
                        });
                }
                ReadonlyCacheEntry CreateTransfromChildEntry(Transform tr)
                {
                    return new ReadonlyCacheEntry("Child objects", tr.Cast<Transform>().ToArray());
                }

                // If we somehow enter a string, this allows user to see what the string actually says
                if (type == typeof(string))
                {
                    _fieldCache.Add(new ReadonlyCacheEntry("this", objectToOpen));
                }
                else if (objectToOpen is Transform tr)
                {
                    _fieldCache.Add(CreateTransfromCallback(tr));
                    _fieldCache.Add(CreateTransfromChildEntry(tr));
                }
                else if (objectToOpen is GameObject ob && ob.transform != null)
                {
                    _fieldCache.Add(CreateTransfromCallback(ob.transform));
                    _fieldCache.Add(CreateTransfromChildEntry(ob.transform));
                }
                else if (objectToOpen is IEnumerable enumerable)
                {
                    _fieldCache.AddRange(enumerable.Cast<object>()
                        .Select((x, y) => x is ICacheEntry ? x : new ListCacheEntry(x, y))
                        .Cast<ICacheEntry>());
                }

                // Instance members
                _fieldCache.AddRange(type
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                               BindingFlags.FlattenHierarchy)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(f => new FieldCacheEntry(objectToOpen, f)).Cast<ICacheEntry>());
                _fieldCache.AddRange(type
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                   BindingFlags.FlattenHierarchy)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(p => new PropertyCacheEntry(objectToOpen, p)).Cast<ICacheEntry>());

                // Static members
                _fieldCache.AddRange(type
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                               BindingFlags.FlattenHierarchy)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(f => new FieldCacheEntry(null, f)).Cast<ICacheEntry>());
                _fieldCache.AddRange(type
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                   BindingFlags.FlattenHierarchy)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(p => new PropertyCacheEntry(null, p)).Cast<ICacheEntry>());

                // Methods
                _fieldCache.AddRange(CacheMethods(objectToOpen,
                    type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                    BindingFlags.FlattenHierarchy)));
                _fieldCache.AddRange(CacheMethods(null,
                    type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                    BindingFlags.FlattenHierarchy)));
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, "[Inspector] CacheFields crash: " + ex);
            }
        }

        private bool CanCovert(string value, Type type)
        {
            if (_canCovertCache.ContainsKey(type))
                return _canCovertCache[type];

            try
            {
                var _ = Convert.ChangeType(value, type);
                _canCovertCache[type] = true;
                return true;
            }
            catch
            {
                _canCovertCache[type] = false;
                return false;
            }
        }

        private void DrawEditableValue(ICacheEntry field, object value, params GUILayoutOption[] layoutParams)
        {
            var isBeingEdited = _currentlyEditingTag == field;
            var text = isBeingEdited ? _currentlyEditingText : EditorUtilities.ExtractText(value);
            var result = GUILayout.TextField(text, layoutParams);

            if (!Equals(text, result) || isBeingEdited)
                if (_userHasHitReturn)
                {
                    _currentlyEditingTag = null;
                    _userHasHitReturn = false;
                    try
                    {
                        var converted = Convert.ChangeType(result, field.Type());
                        if (!Equals(converted, value))
                            field.SetValue(converted);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Error, "[Inspector] Failed to set value - " + ex.Message);
                    }
                }
                else
                {
                    _currentlyEditingText = result;
                    _currentlyEditingTag = field;
                }
        }

        private void DrawValue(object value, params GUILayoutOption[] layoutParams)
        {
            GUILayout.TextArea(EditorUtilities.ExtractText(value), GUI.skin.label, layoutParams);
        }

        private void DrawVariableName(ICacheEntry field)
        {
            GUILayout.TextArea(field.Name(), GUI.skin.label, _inspectorNameWidth);
        }

        private void DrawVariableNameEnterButton(ICacheEntry field)
        {
            if (GUILayout.Button(field.Name(), _alignedButtonStyle, _inspectorNameWidth))
            {
                var val = field.EnterValue();
                if (val != null)
                    _nextToPush = new InspectorStackEntry(val, field.Name());
            }
        }

        public void InspectorClear()
        {
            _inspectorStack.Clear();
            CacheFields(null);
        }

        private void InspectorPop()
        {
            _inspectorStack.Pop();
            CacheFields(_inspectorStack.Peek().Instance);
        }

        public void InspectorPush(InspectorStackEntry o)
        {
            _inspectorStack.Push(o);
            CacheFields(o.Instance);
        }

        private void InspectorWindow(int id)
        {
            try
            {
                GUILayout.BeginVertical();
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box);
                        {
                            GUILayout.Label("Find:");
                            foreach (var obj in new[]
                            {
                                new KeyValuePair<object, string>(
                                    EditorUtilities.GetInstanceClassScanner().OrderBy(x => x.Name()), "Instances"),
                                new KeyValuePair<object, string>(EditorUtilities.GetComponentScanner().OrderBy(x => x.Name()),
                                    "Components"),
                                new KeyValuePair<object, string>(
                                    EditorUtilities.GetMonoBehaviourScanner().OrderBy(x => x.Name()), "MonoBehaviours"),
                                new KeyValuePair<object, string>(EditorUtilities.GetTransformScanner().OrderBy(x => x.Name()),
                                    "Transforms")
                                //                            new KeyValuePair<object, string>(GetTypeScanner(_inspectorStack.Peek().GetType()).OrderBy(x=>x.Name()), _inspectorStack.Peek().GetType().ToString()+"s"),
                            })
                            {
                                if (obj.Key == null) continue;
                                if (GUILayout.Button(obj.Value))
                                {
                                    InspectorClear();
                                    InspectorPush(new InspectorStackEntry(obj.Key, obj.Value));
                                }
                            }
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.Space(13);

                        GUILayout.BeginHorizontal(GUI.skin.box);
                        {
                            if (GUILayout.Button("Help"))
                                InspectorPush(InspectorHelpObj.Create());
                            if (GUILayout.Button("Close"))
                                InspectorClear();
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndHorizontal();

                    _inspectorStackScrollPos = GUILayout.BeginScrollView(_inspectorStackScrollPos, true, false,
                        GUI.skin.horizontalScrollbar, GUIStyle.none, GUIStyle.none, GUILayout.Height(46));
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.ExpandWidth(false),
                            GUILayout.ExpandHeight(false));
                        foreach (var item in _inspectorStack.Reverse().ToArray())
                            if (GUILayout.Button(item.Name, GUILayout.ExpandWidth(false)))
                            {
                                while (_inspectorStack.Peek() != item)
                                    InspectorPop();

                                return;
                            }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Space(1);
                            GUILayout.Label("Value/return type", GUI.skin.box, _inspectorTypeWidth);
                            GUILayout.Space(2);
                            GUILayout.Label("Member name", GUI.skin.box, _inspectorNameWidth);
                            GUILayout.Space(1);
                            GUILayout.Label("Value", GUI.skin.box, GUILayout.ExpandWidth(true));
                        }
                        GUILayout.EndHorizontal();

                        DrawContentScrollView();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "[Inspector] GUI crash: " + ex);
                InspectorClear();
            }

            GUI.DragWindow();
        }

        private void DrawContentScrollView()
        {
            _inspectorScrollPos = GUILayout.BeginScrollView(_inspectorScrollPos);
            {
                GUILayout.BeginVertical();
                {
                    var firstIndex = (int)(_inspectorScrollPos.y / InspectorRecordHeight);

                    GUILayout.Space(firstIndex * InspectorRecordHeight);

                    _currentVisibleCount = (int)(_inspectorWindowRect.height / InspectorRecordHeight) - 4;
                    for (var index = firstIndex; index < Mathf.Min(_fieldCache.Count, firstIndex + _currentVisibleCount); index++)
                    {
                        var entry = _fieldCache[index];
                        try
                        {
                            DrawSingleContentEntry(entry);
                        }
                        catch (ArgumentException)
                        {
                            // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                        }
                    }
                    try
                    {
                        GUILayout.Space(Mathf.Max(_inspectorWindowRect.height / 2, (_fieldCache.Count - firstIndex - _currentVisibleCount) * InspectorRecordHeight));
                    }
                    catch
                    {
                        // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        private void DrawSingleContentEntry(ICacheEntry entry)
        {
            GUILayout.BeginHorizontal((_inspectorRecordHeight));
            {
                GUILayout.Label(entry.TypeName(), (_inspectorTypeWidth));

                var value = entry.GetValue();

                if (entry.CanEnterValue() || value is Exception)
                    DrawVariableNameEnterButton(entry);
                else
                    DrawVariableName(entry);

                if (entry.CanSetValue() &&
                    CanCovert(EditorUtilities.ExtractText(value), entry.Type()))
                    DrawEditableValue(entry, value, GUILayout.ExpandWidth(true));
                else
                    DrawValue(value, GUILayout.ExpandWidth(true));

                if (DnSpyHelper.IsAvailable && GUILayout.Button("^", _dnSpyButtonOptions))
                    DnSpyHelper.OpenTypeInDnSpy(entry);
            }
            GUILayout.EndHorizontal();
        }

        public void DisplayInspector()
        {
            if (_alignedButtonStyle == null)
            {
                _alignedButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true
                };
            }

            if (Event.current.keyCode == KeyCode.Return) _userHasHitReturn = true;

            while (_inspectorStack.Count > 0 && _inspectorStack.Peek().Instance == null)
            {
                var se = _inspectorStack.Pop();
                Logger.Log(LogLevel.Message, $"[Inspector] Inspected object \"{se.Name}\" has been destroyed");
            }

            if (_inspectorStack.Count != 0)
            {
                EditorUtilities.DrawSolidWindowBackground(_inspectorWindowRect);
                _inspectorWindowRect = GUILayout.Window(592, _inspectorWindowRect, InspectorWindow, "Inspector");
            }
        }

        public void UpdateWindowSize(Rect screenRect)
        {
            var width = (int) Mathf.Min(850, screenRect.width);

            var height = screenRect.height;//(int)(screenRect.height / 3) * 2;

            _inspectorWindowRect = new Rect(screenRect.xMin + screenRect.width / 2 - Mathf.RoundToInt((float)width / 2), screenRect.yMin, width, height);
        }

        public void InspectorUpdate()
        {
            if (_nextToPush != null)
            {
                InspectorPush(_nextToPush);

                _nextToPush = null;
            }
        }
    }
}