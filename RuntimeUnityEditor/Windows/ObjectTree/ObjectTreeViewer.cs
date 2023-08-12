﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectView;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RuntimeUnityEditor.Core.ObjectTree
{
    public sealed class ObjectTreeViewer : Window<ObjectTreeViewer>
    {
        private readonly HashSet<GameObject> _openedObjects = new HashSet<GameObject>();
        private Transform _selectedTransform;

        private Vector2 _propertiesScrollPosition;
        private Vector2 _treeScrollPosition;
        private float _objectTreeHeight;
        private int _singleObjectTreeItemHeight;

        private bool _scrollTreeToSelected;
        private int _scrollTarget;

        private RootGameObjectSearcher _gameObjectSearcher;
        private readonly Dictionary<Image, Texture2D> _imagePreviewCache = new Dictionary<Image, Texture2D>();
        private readonly GUILayoutOption _drawVector3FieldWidth = GUILayout.Width(38);
        private readonly GUILayoutOption _drawVector3FieldHeight = GUILayout.Height(19);
        private readonly GUILayoutOption _drawVector3SliderHeight = GUILayout.Height(10);
        private readonly GUILayoutOption _drawVector3SliderWidth = GUILayout.Width(33);

        public event Action<Transform> TreeSelectionChanged;

        public void SelectAndShowObject(Transform target)
        {
            SelectedTransform = target;

            if (target == null) return;

            target = target.parent;
            while (target != null)
            {
                _openedObjects.Add(target.gameObject);
                target = target.parent;
            }

            _scrollTreeToSelected = true;
            Enabled = true;
        }

        protected override void Initialize(InitSettings initSettings)
        {
            Title = "Object Browser";
            _gameObjectSearcher = new RootGameObjectSearcher();
        }

        public Transform SelectedTransform
        {
            get => _selectedTransform;
            set
            {
                if (_selectedTransform != value)
                {
                    _selectedTransform = value;
                    //_searchTextComponents = _gameObjectSearcher.IsSearching() ? _searchText : "";
                    TreeSelectionChanged?.Invoke(_selectedTransform);
                }
            }
        }

        public void ClearCaches()
        {
            _imagePreviewCache.Clear();
        }

        private static void OnInspectorOpen(params InspectorStackEntryBase[] items)
        {
            for (var i = 0; i < items.Length; i++)
            {
                var stackEntry = items[i];
                Inspector.Inspector.Instance.Push(stackEntry, i == 0);
            }
        }

        public override Rect WindowRect
        {
            get => base.WindowRect;
            set
            {
                base.WindowRect = value;
                _objectTreeHeight = value.height / 3;
            }
        }

        private void DisplayObjectTreeHelper(GameObject go, int indent, ref int currentCount, ref int notVisibleCount)
        {
            currentCount++;

            var needsHeightMeasure = _singleObjectTreeItemHeight == 0;

            var isVisible = currentCount * _singleObjectTreeItemHeight >= _treeScrollPosition.y &&
                            (currentCount - 1) * _singleObjectTreeItemHeight <= _treeScrollPosition.y + _objectTreeHeight;

            if (isVisible || needsHeightMeasure || _scrollTreeToSelected)
            {
                if (notVisibleCount > 0)
                {
                    GUILayout.Space(_singleObjectTreeItemHeight * notVisibleCount);
                    notVisibleCount = 0;
                }

                var c = GUI.color;
                if (SelectedTransform == go.transform)
                {
                    GUI.color = Color.cyan;
                    if (_scrollTreeToSelected && Event.current.type == EventType.Repaint)
                        _scrollTarget = (int)(GUILayoutUtility.GetLastRect().y - 250);
                }
                else if (!go.activeSelf)
                {
                    GUI.color = new Color(1, 1, 1, 0.6f);
                }

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Space(indent * 20f);

                    GUILayout.BeginHorizontal();
                    {
                        if (go.transform.childCount != 0)
                        {
                            if (GUILayout.Toggle(_openedObjects.Contains(go), "", GUILayout.ExpandWidth(false)))
                                _openedObjects.Add(go);
                            else
                                _openedObjects.Remove(go);
                        }
                        else
                        {
                            GUILayout.Space(20f);
                        }

                        if (GUILayout.Button(go.name, GUI.skin.label, GUILayout.ExpandWidth(true), GUILayout.MinWidth(200)))
                        {
                            if (IMGUIUtils.IsMouseRightClick())
                            {
                                ContextMenu.Instance.Show(go, null);
                            }
                            else
                            {
                                if (SelectedTransform == go.transform)
                                {
                                    // Toggle on/off
                                    if (!_openedObjects.Add(go))
                                        _openedObjects.Remove(go);
                                }
                                else
                                {
                                    SelectedTransform = go.transform;
                                }
                            }
                        }

                        GUI.color = c;
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndHorizontal();

                if (needsHeightMeasure && Event.current.type == EventType.repaint)
                    _singleObjectTreeItemHeight = Mathf.CeilToInt(GUILayoutUtility.GetLastRect().height);
            }
            else
            {
                notVisibleCount++;
            }

            if (_openedObjects.Contains(go))
            {
                for (var i = 0; i < go.transform.childCount; ++i)
                    DisplayObjectTreeHelper(go.transform.GetChild(i).gameObject, indent + 1, ref currentCount, ref notVisibleCount);
            }
        }

        protected override void DrawContents()
        {
            GUILayout.BeginVertical();
            {
                DisplayObjectTree();

                DisplayObjectProperties();
            }
            GUILayout.EndVertical();
        }

        private void DisplayObjectProperties()
        {
            _propertiesScrollPosition = GUILayout.BeginScrollView(_propertiesScrollPosition, GUI.skin.box);
            {
                if (SelectedTransform == null)
                {
                    GUILayout.Label("No object selected");
                }
                else
                {
                    DrawTransformControls();

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Search components ", GUILayout.ExpandWidth(false));

                        _searchTextComponents = GUILayout.TextField(_searchTextComponents, GUILayout.ExpandWidth(true));

                        if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                            _searchTextComponents = string.Empty;
                    }
                    GUILayout.EndHorizontal();

                    foreach (var component in SelectedTransform.GetComponents<Component>())
                    {
                        if (component == null)
                            continue;

                        if (!string.IsNullOrEmpty(_searchTextComponents) && !RootGameObjectSearcher.SearchInComponent(_searchTextComponents, component, false))
                            continue;

                        DrawSingleComponent(component);
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawTransformControls()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                var fullTransfromPath = SelectedTransform.GetFullTransfromPath();

                GUILayout.TextArea(fullTransfromPath, GUI.skin.label);

                GUILayout.BeginHorizontal();
                {
                    var selectedGameObject = SelectedTransform.gameObject;
                    GUILayout.Label($"Layer {selectedGameObject.layer} ({LayerMask.LayerToName(selectedGameObject.layer)})");

                    GUILayout.Space(8);

                    GUILayout.Toggle(selectedGameObject.isStatic, "isStatic");

                    selectedGameObject.SetActive(GUILayout.Toggle(selectedGameObject.activeSelf, "Active", GUILayout.ExpandWidth(false)));

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Inspect"))
                        OnInspectorOpen(new InstanceStackEntry(selectedGameObject, selectedGameObject.name));

                    if (GUILayout.Button("X"))
                        Object.Destroy(selectedGameObject);
                }
                GUILayout.EndHorizontal();

                DrawVector3(nameof(Transform.position), vector3 => SelectedTransform.position = vector3, () => SelectedTransform.position, -5, 5);
                DrawVector3(nameof(Transform.localPosition), vector3 => SelectedTransform.localPosition = vector3, () => SelectedTransform.localPosition, -5, 5);
                DrawVector3(nameof(Transform.lossyScale), vector3 => SelectedTransform.SetLossyScale(vector3), () => SelectedTransform.lossyScale, 0.00001f, 5);
                DrawVector3(nameof(Transform.localScale), vector3 => SelectedTransform.localScale = vector3, () => SelectedTransform.localScale, 0.00001f, 5);
                DrawVector3(nameof(Transform.eulerAngles), vector3 => SelectedTransform.eulerAngles = vector3, () => SelectedTransform.eulerAngles, 0, 360);
                DrawVector3(nameof(Transform.localEulerAngles), vector3 => SelectedTransform.localEulerAngles = vector3, () => SelectedTransform.localEulerAngles, 0, 360);
            }
            GUILayout.EndVertical();
        }

        private void DrawVector3(string name, Action<Vector3> set, Func<Vector3> get, float minVal, float maxVal)
        {
            var v3 = get();
            var v3New = v3;

            GUI.changed = false;
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(name, GUILayout.ExpandWidth(true), _drawVector3FieldHeight);
                v3New.x = GUILayout.HorizontalSlider(v3.x, minVal, maxVal, _drawVector3SliderWidth, _drawVector3SliderHeight);
                float.TryParse(GUILayout.TextField(v3New.x.ToString("F2", CultureInfo.InvariantCulture), _drawVector3FieldWidth, _drawVector3FieldHeight), NumberStyles.Any, CultureInfo.InvariantCulture, out v3New.x);
                v3New.y = GUILayout.HorizontalSlider(v3.y, minVal, maxVal, _drawVector3SliderWidth, _drawVector3SliderHeight);
                float.TryParse(GUILayout.TextField(v3New.y.ToString("F2", CultureInfo.InvariantCulture), _drawVector3FieldWidth, _drawVector3FieldHeight), NumberStyles.Any, CultureInfo.InvariantCulture, out v3New.y);
                v3New.z = GUILayout.HorizontalSlider(v3.z, minVal, maxVal, _drawVector3SliderWidth, _drawVector3SliderHeight);
                float.TryParse(GUILayout.TextField(v3New.z.ToString("F2", CultureInfo.InvariantCulture), _drawVector3FieldWidth, _drawVector3FieldHeight), NumberStyles.Any, CultureInfo.InvariantCulture, out v3New.z);
            }
            GUILayout.EndHorizontal();

            if (GUI.changed && v3 != v3New) set(v3New);
        }

        private void DrawVector2(string name, Action<Vector2> set, Func<Vector2> get, float minVal, float maxVal)
        {
            var vector2 = get();
            var vector2New = vector2;

            GUI.changed = false;
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(name, GUILayout.ExpandWidth(true), _drawVector3FieldHeight);
                vector2New.x = GUILayout.HorizontalSlider(vector2.x, minVal, maxVal, _drawVector3SliderWidth, _drawVector3SliderHeight);
                float.TryParse(GUILayout.TextField(vector2New.x.ToString("F2", CultureInfo.InvariantCulture), _drawVector3FieldWidth, _drawVector3FieldHeight), NumberStyles.Any, CultureInfo.InvariantCulture, out vector2New.x);
                vector2New.y = GUILayout.HorizontalSlider(vector2.y, minVal, maxVal, _drawVector3SliderWidth, _drawVector3SliderHeight);
                float.TryParse(GUILayout.TextField(vector2New.y.ToString("F2", CultureInfo.InvariantCulture), _drawVector3FieldWidth, _drawVector3FieldHeight), NumberStyles.Any, CultureInfo.InvariantCulture, out vector2New.y);
            }
            GUILayout.EndHorizontal();

            if (GUI.changed && vector2 != vector2New) set(vector2New);
        }

        private void DrawSingleComponent(Component component)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                if (component is Behaviour bh)
                    bh.enabled = GUILayout.Toggle(bh.enabled, "", GUILayout.ExpandWidth(false));

                var type = component.GetType();
                if (GUILayout.Button(new GUIContent(type.Name, $"{component}\n\nFull type: {type.GetFancyDescription()}\n\nLeft click to open in Inspector\nRight click to for more options"), GUI.skin.label))
                {
                    if (IMGUIUtils.IsMouseRightClick())
                    {
                        ContextMenu.Instance.Show(component, null);
                    }
                    else
                    {
                        var transform = component.transform;
                        OnInspectorOpen(new InstanceStackEntry(transform, transform.name),
                                        new InstanceStackEntry(component, type.FullName));
                    }
                }

                switch (component)
                {
                    case Image img:
                        var texShown = false;
                        var imgSprite = img.sprite;
                        if (imgSprite != null)
                        {
                            GUILayout.Label(imgSprite.name);
                            var texture = imgSprite.texture;
                            if (texture != null)
                            {
                                if (!_imagePreviewCache.TryGetValue(img, out var tex))
                                {
                                    try
                                    {
                                        tex = imgSprite.GetVisibleTexture();
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex);
                                        tex = null;
                                    }

                                    _imagePreviewCache.Add(img, tex);
                                }

                                if (tex != null)
                                {
                                    if (GUILayout.Button(tex, GUI.skin.box))
                                        ObjectViewWindow.Instance.SetShownObject(tex, imgSprite.name);
                                    texShown = true;
                                }
                            }
                        }

                        if (!texShown && img.mainTexture != null)
                        {
                            if (GUILayout.Button(img.mainTexture, GUI.skin.box))
                                ObjectViewWindow.Instance.SetShownObject(img.mainTexture, img.ToString());
                            texShown = true;
                        }

                        if (!texShown)
                            GUILayout.Label("Can't display texture");

                        GUILayout.FlexibleSpace();
                        break;
                    case Slider b:
                        {
                            for (var i = 0; i < b.onValueChanged.GetPersistentEventCount(); ++i)
                                GUILayout.Label(ToStringConverter.EventEntryToString(b.onValueChanged, i));

                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("?"))
                                ObjectViewWindow.Instance.SetShownObject(ReflectionUtils.GetEventDetails(b.onValueChanged), $"{b} / {b.onValueChanged} - Event details");
                            break;
                        }
                    case Text text:
                        GUILayout.Label($"{text.text} {text.font} {text.fontStyle} {text.fontSize} {text.alignment} {text.resizeTextForBestFit} {text.color}");
                        GUILayout.FlexibleSpace();
                        break;
                    case RawImage r:
                        var rMainTexture = r.mainTexture;
                        if (rMainTexture != null)
                        {
                            if (GUILayout.Button(rMainTexture, GUI.skin.box))
                                ObjectViewWindow.Instance.SetShownObject(rMainTexture, r.ToString());
                        }
                        else
                        {
                            GUILayout.Label("Can't display texture");
                        }

                        GUILayout.FlexibleSpace();
                        break;
                    case Renderer re:
                        var reMaterial = re.sharedMaterial ?? re.material;
                        GUILayout.Label(reMaterial != null ? reMaterial.shader.name : "[No material]");
                        if (reMaterial != null && reMaterial.mainTexture != null)
                        {
                            if (GUILayout.Button(reMaterial.mainTexture, GUI.skin.box))
                                ObjectViewWindow.Instance.SetShownObject(reMaterial.mainTexture, re.ToString());
                        }
                        GUILayout.FlexibleSpace();
                        break;
                    case Button b:
                        {
                            var eventObj = b.onClick;
                            for (var i = 0; i < eventObj.GetPersistentEventCount(); ++i)
                                GUILayout.Label(ToStringConverter.EventEntryToString(eventObj, i));

                            var calls = (IList)eventObj.GetPrivateExplicit<UnityEventBase>("m_Calls").GetPrivate("m_RuntimeCalls");
                            foreach (var call in calls)
                                GUILayout.Label(ToStringConverter.ObjectToString(call.GetPrivate("Delegate")));

                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("?"))
                                ObjectViewWindow.Instance.SetShownObject(ReflectionUtils.GetEventDetails(b.onClick), $"{b} / {b.onClick} - Event details");
                            break;
                        }
                    case Toggle b:
                        {
                            var eventObj = b.onValueChanged;
                            for (var i = 0; i < eventObj.GetPersistentEventCount(); ++i)
                                GUILayout.Label(ToStringConverter.EventEntryToString(b.onValueChanged, i));

                            var calls = (IList)b.onValueChanged.GetPrivateExplicit<UnityEventBase>("m_Calls").GetPrivate("m_RuntimeCalls");
                            foreach (var call in calls)
                                GUILayout.Label(ToStringConverter.ObjectToString(call.GetPrivate("Delegate")));

                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("?"))
                                ObjectViewWindow.Instance.SetShownObject(ReflectionUtils.GetEventDetails(b.onValueChanged), $"{b} / {b.onValueChanged} - Event details");
                            break;
                        }
                    case RectTransform rt:
                        GUILayout.BeginVertical();
                        {
                            DrawVector2(nameof(RectTransform.anchorMin), vector2 => rt.anchorMin = vector2, () => rt.anchorMin, 0, 1);
                            DrawVector2(nameof(RectTransform.anchorMax), vector2 => rt.anchorMax = vector2, () => rt.anchorMax, 0, 1);
                            DrawVector2(nameof(RectTransform.offsetMin), vector2 => rt.offsetMin = vector2, () => rt.offsetMin, -1000, 1000);
                            DrawVector2(nameof(RectTransform.offsetMax), vector2 => rt.offsetMax = vector2, () => rt.offsetMax, -1000, 1000);
                            DrawVector2(nameof(RectTransform.sizeDelta), vector2 => rt.sizeDelta = vector2, () => rt.sizeDelta, -1000, 1000);
                            GUILayout.Label("rect " + rt.rect);
                        }
                        GUILayout.EndVertical();
                        break;
                    default:
                        GUILayout.FlexibleSpace();
                        break;
                }
            }
            GUILayout.EndHorizontal();
        }

        private string _searchText = string.Empty;
        private string _searchTextComponents = string.Empty;
        private void DisplayObjectTree()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                DisplayTreeSearchBox();

                _treeScrollPosition = GUILayout.BeginScrollView(_treeScrollPosition,
                    GUILayout.Height(_objectTreeHeight), GUILayout.ExpandWidth(true));
                {
                    var currentCount = 0;
                    var notVisibleCount = 0;
                    foreach (var rootGameObject in _gameObjectSearcher.GetSearchedOrAllObjects())
                        DisplayObjectTreeHelper(rootGameObject, 0, ref currentCount, ref notVisibleCount);

                    if (notVisibleCount > 0)
                        GUILayout.Space(_singleObjectTreeItemHeight * notVisibleCount);
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }

        private void DisplayTreeSearchBox()
        {
            GUILayout.BeginHorizontal();
            {
                GUI.SetNextControlName("searchbox");
                _searchText = GUILayout.TextField(_searchText, GUILayout.ExpandWidth(true));

                if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                {
                    _searchText = string.Empty;
                    _gameObjectSearcher.Search(_searchText, false);
                    SelectAndShowObject(SelectedTransform);
                }

                GUILayout.Space(3);

                if (SelectedTransform == null) GUI.enabled = false;
                if (GUILayout.Button("Dump obj", GUILayout.ExpandWidth(false)))
                    SceneDumper.DumpObjects(SelectedTransform?.gameObject);
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Search scene"))
                {
                    _gameObjectSearcher.Search(_searchText, false);
                    //_searchTextComponents = _searchText;
                }

                if (Event.current.isKey && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "searchbox")
                {
                    _gameObjectSearcher.Search(_searchText, false);
                    //_searchTextComponents = _searchText;
                    Event.current.Use();
                }

                if (GUILayout.Button("Deep scene"))
                {
                    _gameObjectSearcher.Search(_searchText, true);
                    //_searchTextComponents = _searchText;
                }

                if (GUILayout.Button("Search static"))
                {
                    if (string.IsNullOrEmpty(_searchText))
                    {
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Message | LogLevel.Warning, "Can't search for empty string");
                    }
                    else
                    {
                        var matchedTypes = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(Extensions.GetTypesSafe)
                            .Where(x => x.GetSourceCodeRepresentation().Contains(_searchText, StringComparison.OrdinalIgnoreCase));

                        var stackEntries = matchedTypes.Select(t => new StaticStackEntry(t, t.FullName)).ToList();

                        if (stackEntries.Count == 0)
                        {
                            RuntimeUnityEditorCore.Logger.Log(LogLevel.Message | LogLevel.Warning, "No static type names contained the search string");
                        }
                        else
                        {
                            Inspector.Inspector.Instance.Push(new InstanceStackEntry(stackEntries, "Static type search"), true);
                            if (stackEntries.Count == 1)
                                Inspector.Inspector.Instance.Push(stackEntries.Single(), false);
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        public void FindReferencesInScene(object obj)
        {
            if (_gameObjectSearcher.SearchReferences(obj))
                _searchText = "Search for object references...";
        }

        protected override void Update()
        {
            if (_scrollTreeToSelected && _scrollTarget > 0)
            {
                _scrollTreeToSelected = false;
                _treeScrollPosition.y = _scrollTarget;
                _scrollTarget = 0;
            }

            _gameObjectSearcher.Refresh(false, null);
        }

        protected override Rect GetDefaultWindowRect(Rect screenRect)
        {
            return MakeDefaultWindowRect(screenRect, TextAlignment.Right);
        }

        protected override void VisibleChanged(bool visible)
        {
            base.VisibleChanged(visible);

            ClearCaches();

            if (visible)
                _gameObjectSearcher.Refresh(true, null);
        }
    }
}
