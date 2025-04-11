using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RuntimeUnityEditor.Core.ChangeHistory;
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
    /// <summary>
    /// Shows a tree of GameObjects/Transforms currently loaded. Also lists components attached to GOs and simple edit controls. Similar to Unity Editor interface.
    /// </summary>
    public sealed class ObjectTreeViewer : Window<ObjectTreeViewer>
    {
        private readonly HashSet<GameObject> _openedObjects = new HashSet<GameObject>();
        private Transform _selectedTransform;

        private Vector2 _propertiesScrollPosition;
        private Vector2 _treeScrollPosition;
        private float _objectTreeHeight;
        private float _singleObjectTreeItemHeight;
        private float _singleObjectTreeItemMargin;

        private bool _scrollTreeToSelected;
        private float _scrollTarget = -1f;

        private bool _searchNames = true;
        private InitSettings.Setting<bool> _searchComponents;
        private InitSettings.Setting<bool> _searchProperties;

        private RootGameObjectSearcher _gameObjectSearcher;
        private readonly Dictionary<Image, Texture2D> _imagePreviewCache = new Dictionary<Image, Texture2D>();
        private static readonly GUILayoutOption _drawVector3FieldWidth = GUILayout.Width(38);
        private static readonly GUILayoutOption _drawVector3FieldHeight = GUILayout.Height(19);
        private static readonly GUILayoutOption _drawVector3SliderHeight = GUILayout.Height(10);
        private static readonly GUILayoutOption _drawVector3SliderWidth = GUILayout.Width(33);

        /// <summary>
        /// Invoked whenever a new Transform is selected in the tree.
        /// </summary>
        public event Action<Transform> TreeSelectionChanged;

        /// <summary>
        /// Select the transform in the tree list, expand the tree to make sure it's visible, and scroll the view to make sure user can see it.
        /// </summary>
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

        /// <inheritdoc />
        protected override void Initialize(InitSettings initSettings)
        {
            Title = "Object Browser";
            _gameObjectSearcher = new RootGameObjectSearcher();
            DefaultScreenPosition = ScreenPartition.Right;

            _searchComponents = initSettings.RegisterSetting(Title, "Search component names", true, "Include names of components on a GameObject when searching with the search box.");
            _searchProperties = initSettings.RegisterSetting(Title, "Search component properties", false, "Include values of properties of components on a GameObject when searching with the search box. Very slow and may have side effects!");
        }

        /// <summary>
        /// Transform currently selected in the tree.
        /// </summary>
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

        /// <summary>
        /// Clear any display caches that might hold no longer existing data, e.g. destroyed sprites or textures.
        /// </summary>
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

        /// <inheritdoc />
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
            if (!go) return;

            currentCount++;

            var needsHeightMeasure = _singleObjectTreeItemHeight == 0f;

            var isVisible = currentCount * _singleObjectTreeItemHeight + _singleObjectTreeItemMargin >= _treeScrollPosition.y &&
                            (currentCount - 1) * _singleObjectTreeItemHeight + _singleObjectTreeItemMargin <= _treeScrollPosition.y + _objectTreeHeight;

            if (SelectedTransform == go.transform && _scrollTreeToSelected)
            {
                _scrollTarget = currentCount == 1 ? 0 : _singleObjectTreeItemHeight * (currentCount - 1);
            }

            if (isVisible || needsHeightMeasure)
            {
                if (notVisibleCount > 0)
                {
                    GUILayout.Space(_singleObjectTreeItemHeight * notVisibleCount + _singleObjectTreeItemMargin);
                    notVisibleCount = 0;
                }

                var c = GUI.color;
                if (SelectedTransform == go.transform)
                {
                    GUI.color = Color.cyan;
                }
                else if (go.GetSceneName(out var sceneName) && sceneName == null && !go.activeInHierarchy)
                {
                    GUI.color = new Color(0.6f, 0.6f, 0.4f, 1);
                }
                else if (!go.activeInHierarchy)
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
                            if (GUILayout.Toggle(_openedObjects.Contains(go), "", IMGUIUtils.LayoutOptionsExpandWidthFalse))
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
                                ContextMenu.Instance.Show(go);
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

                if (Event.current.type == EventType.Repaint)
                {
                    if (needsHeightMeasure)
                    {
                        _singleObjectTreeItemMargin = GUI.skin.label.margin.top;
                        _singleObjectTreeItemHeight = GUILayoutUtility.GetLastRect().height + _singleObjectTreeItemMargin;
                    }
                }
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

        /// <inheritdoc />
        protected override void DrawContents()
        {
            GUILayout.BeginVertical();
            {
                DisplayObjectTree();

                DisplayObjectProperties();
            }
            GUILayout.EndVertical();

            _sceneDropdown.DrawDropdownIfOpen();
        }

        private void DisplayObjectProperties()
        {
            _propertiesScrollPosition = GUILayout.BeginScrollView(_propertiesScrollPosition, GUI.skin.box);
            {
                if (!SelectedTransform)
                {
                    GUILayout.Label("No object selected");
                }
                else
                {
                    DrawTransformControls();

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Search components ", IMGUIUtils.LayoutOptionsExpandWidthFalse);

                        _searchTextComponents = GUILayout.TextField(_searchTextComponents, IMGUIUtils.LayoutOptionsExpandWidthTrue);

                        if (GUILayout.Button("Clear", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                            _searchTextComponents = string.Empty;
                    }
                    GUILayout.EndHorizontal();

                    foreach (var component in SelectedTransform.AbstractGetAllComponents())
                    {
                        if (!component)
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
                var transform = SelectedTransform;
                var fullTransfromPath = transform.GetFullTransfromPath();

                GUILayout.TextArea(fullTransfromPath, GUI.skin.label);

                GUILayout.BeginHorizontal();
                {
                    var selectedGameObject = transform.gameObject;
                    GUILayout.Label($"Layer {selectedGameObject.layer} ({LayerMask.LayerToName(selectedGameObject.layer)})");

                    GUILayout.Space(8);

                    GUILayout.Toggle(selectedGameObject.isStatic, "isStatic");

                    GUI.changed = false;
                    var newVal = GUILayout.Toggle(selectedGameObject.activeSelf, "Active", IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    if (GUI.changed)
                        Change.Action($"{{0}}.SetActive({newVal});", selectedGameObject, o => o.SetActiveWithSceneChangeWarning(newVal), o => o.SetActiveWithSceneChangeWarning(!newVal));

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Inspect"))
                        OnInspectorOpen(new InstanceStackEntry(selectedGameObject, selectedGameObject.name));

                    if (GUILayout.Button("X"))
                        Change.Action("Object.Destroy({0})", selectedGameObject, Object.Destroy);
                }
                GUILayout.EndHorizontal();

                var oldWidth = GUI.skin.horizontalSlider.fixedWidth;
                GUI.skin.horizontalSliderThumb.fixedWidth = 12;

                DrawVector3(nameof(Transform.position), transform, (t, v) => t.position = v, t => t.position, -5, 5);
                DrawVector3(nameof(Transform.localPosition), transform, (t, v) => t.localPosition = v, t => t.localPosition, -5, 5);
                DrawVector3(nameof(Transform.lossyScale), transform, (t, v) => t.SetLossyScale(v), t => t.lossyScale, 0.00001f, 5);
                DrawVector3(nameof(Transform.localScale), transform, (t, v) => t.localScale = v, t => t.localScale, 0.00001f, 5);
                DrawVector3(nameof(Transform.eulerAngles), transform, (t, v) => t.eulerAngles = v, t => t.eulerAngles, 0, 360);
                DrawVector3(nameof(Transform.localEulerAngles), transform, (t, v) => t.localEulerAngles = v, t => t.localEulerAngles, 0, 360);

                GUI.skin.horizontalSlider.fixedWidth = oldWidth;
            }
            GUILayout.EndVertical();
        }

        private static void DrawVector3(string memberName, Transform transform, Action<Transform, Vector3> set, Func<Transform, Vector3> get, float minVal, float maxVal)
        {
            var v3 = get(transform);
            var v3New = v3;

            GUI.changed = false;
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(memberName, GUILayout.ExpandWidth(true), _drawVector3FieldHeight);
                v3New.x = GUILayout.HorizontalSlider(v3.x, minVal, maxVal, _drawVector3SliderWidth, _drawVector3SliderHeight);
                float.TryParse(GUILayout.TextField(v3New.x.ToString("F2", CultureInfo.InvariantCulture), _drawVector3FieldWidth, _drawVector3FieldHeight), NumberStyles.Any, CultureInfo.InvariantCulture, out v3New.x);
                v3New.y = GUILayout.HorizontalSlider(v3.y, minVal, maxVal, _drawVector3SliderWidth, _drawVector3SliderHeight);
                float.TryParse(GUILayout.TextField(v3New.y.ToString("F2", CultureInfo.InvariantCulture), _drawVector3FieldWidth, _drawVector3FieldHeight), NumberStyles.Any, CultureInfo.InvariantCulture, out v3New.y);
                v3New.z = GUILayout.HorizontalSlider(v3.z, minVal, maxVal, _drawVector3SliderWidth, _drawVector3SliderHeight);
                float.TryParse(GUILayout.TextField(v3New.z.ToString("F2", CultureInfo.InvariantCulture), _drawVector3FieldWidth, _drawVector3FieldHeight), NumberStyles.Any, CultureInfo.InvariantCulture, out v3New.z);
            }
            GUILayout.EndHorizontal();

            if (GUI.changed && v3 != v3New) Change.WithUndo($"{{0}}.{memberName} = {{1}}", transform, v3New, set, oldValue: v3);
        }

        private static void DrawVector2(string memberName, Transform transform, Action<Transform, Vector2> set, Func<Transform, Vector2> get, float minVal, float maxVal)
        {
            var vector2 = get(transform);
            var vector2New = vector2;

            GUI.changed = false;
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(memberName, GUILayout.ExpandWidth(true), _drawVector3FieldHeight);
                vector2New.x = GUILayout.HorizontalSlider(vector2.x, minVal, maxVal, _drawVector3SliderWidth, _drawVector3SliderHeight);
                float.TryParse(GUILayout.TextField(vector2New.x.ToString("F2", CultureInfo.InvariantCulture), _drawVector3FieldWidth, _drawVector3FieldHeight), NumberStyles.Any, CultureInfo.InvariantCulture, out vector2New.x);
                vector2New.y = GUILayout.HorizontalSlider(vector2.y, minVal, maxVal, _drawVector3SliderWidth, _drawVector3SliderHeight);
                float.TryParse(GUILayout.TextField(vector2New.y.ToString("F2", CultureInfo.InvariantCulture), _drawVector3FieldWidth, _drawVector3FieldHeight), NumberStyles.Any, CultureInfo.InvariantCulture, out vector2New.y);
            }
            GUILayout.EndHorizontal();

            if (GUI.changed && vector2 != vector2New) Change.WithUndo($"{{0}}.{memberName} = {{1}}", transform, vector2New, set, oldValue: vector2);
        }

        private void DrawSingleComponent(Component component)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                if (component is Behaviour bh)
                {
                    GUI.changed = false;
                    var enabledNew = GUILayout.Toggle(bh.enabled, "", IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    if (GUI.changed)
                        Change.MemberAssignment(bh, enabledNew, b => b.enabled);
                }

                var type = component.GetType();
                if (GUILayout.Button(new GUIContent(type.Name, null, $"{component}\n\nFull type: {type.GetFancyDescription()}\n\nLeft click to open in Inspector\nRight click to for more options"), GUI.skin.label))
                {
                    if (IMGUIUtils.IsMouseRightClick())
                    {
                        ContextMenu.Instance.Show(component);
                    }
                    else
                    {
                        var transform = component.transform;
                        OnInspectorOpen(new InstanceStackEntry(transform, transform.name),
                                        new InstanceStackEntry(component, type.GetSourceCodeRepresentation()));
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

                            try
                            {
                                var calls = eventObj.GetPrivateExplicit<UnityEventBase>("m_Calls").GetPrivate("m_RuntimeCalls").CastToEnumerable();
                                foreach (var call in calls)
                                    GUILayout.Label(ToStringConverter.ObjectToString(call.GetPrivate("Delegate")));
                            }
                            catch (NullReferenceException) { }
#if IL2CPP
                            catch (HarmonyLib.MemberNotFoundException) { /* IL2CPP stripped it probably */ }
#endif
                            catch (Exception e) { RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e); }

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

                            try
                            {
                                var calls = b.onValueChanged.GetPrivateExplicit<UnityEventBase>("m_Calls").GetPrivate("m_RuntimeCalls").CastToEnumerable();
                                foreach (var call in calls)
                                    GUILayout.Label(ToStringConverter.ObjectToString(call.GetPrivate("Delegate")));
                            }
                            catch (NullReferenceException) { }
#if IL2CPP
                            catch (HarmonyLib.MemberNotFoundException) { /* IL2CPP stripped it probably */ }
#endif
                            catch (Exception e) { RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e); }

                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("?"))
                                ObjectViewWindow.Instance.SetShownObject(ReflectionUtils.GetEventDetails(b.onValueChanged), $"{b} / {b.onValueChanged} - Event details");
                            break;
                        }
                    case RectTransform rt:
                        GUILayout.BeginVertical();
                        {
                            DrawVector2(nameof(RectTransform.anchorMin), rt, (t, vector2) => ((RectTransform)t).anchorMin = vector2, t => ((RectTransform)t).anchorMin, 0, 1);
                            DrawVector2(nameof(RectTransform.anchorMax), rt, (t, vector2) => ((RectTransform)t).anchorMax = vector2, t => ((RectTransform)t).anchorMax, 0, 1);
                            DrawVector2(nameof(RectTransform.offsetMin), rt, (t, vector2) => ((RectTransform)t).offsetMin = vector2, t => ((RectTransform)t).offsetMin, -1000, 1000);
                            DrawVector2(nameof(RectTransform.offsetMax), rt, (t, vector2) => ((RectTransform)t).offsetMax = vector2, t => ((RectTransform)t).offsetMax, -1000, 1000);
                            DrawVector2(nameof(RectTransform.sizeDelta), rt, (t, vector2) => ((RectTransform)t).sizeDelta = vector2, t => ((RectTransform)t).sizeDelta, -1000, 1000);
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
                    if (_scrollTreeToSelected)
                        _scrollTarget = Mathf.Min(_scrollTarget, _singleObjectTreeItemHeight * currentCount + _singleObjectTreeItemMargin - _objectTreeHeight);
                }
                GUILayout.EndScrollView();

                DisplaySceneControls();
            }
            GUILayout.EndVertical();
        }

        private void UpdateSearch()
        {
            _gameObjectSearcher.Search(_searchText, _searchNames, _searchComponents.Value, _searchProperties.Value, false);
            _treeScrollPosition = Vector2.zero;
        }

        private readonly GUIContent _searchLabelContent = new GUIContent("Search:");
        private readonly GUIContent _searchClearButtonContent = new GUIContent("Clear");
        private readonly GUIContent _searchNamesContent = new GUIContent("Names", null, "Include names of GameObjects when searching with the search box.");
        private readonly GUIContent _searchComponentsContent = new GUIContent("Components", null, "Include names of components on a GameObject when searching with the search box.");
        private readonly GUIContent _searchPropertiesContent = new GUIContent("Properties", null, "Include values of properties and fields of components on a GameObject when searching with the search box. Very slow and may have side effects!");
        private readonly GUIContent _searchStaticsButtonContent = new GUIContent("Statics", null, "Search for static classes by their name. Results are opened in inspector.");
        private static readonly string[] _spinner = { "|", "/", "-", "\\" };
        private void DisplayTreeSearchBox()
        {
            //todo
            // add info markings to list items on right
            //   scene, prefab, etc

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(_searchLabelContent, IMGUIUtils.LayoutOptionsExpandWidthFalse);

                    GUI.changed = false;

                    GUI.SetNextControlName("searchbox");
                    _searchText = GUILayout.TextField(_searchText, IMGUIUtils.LayoutOptionsExpandWidthTrue);

                    // Show indicator that search results are still being updated
                    if (_gameObjectSearcher.BusyIndexing)
                        GUILayout.Label(_spinner[Mathf.FloorToInt(((Time.time * 2) % 1) / 0.25f)], GUILayout.Width(5));

                    if (GUI.changed)
                        UpdateSearch();
                    if (GUILayout.Button(_searchClearButtonContent, IMGUIUtils.LayoutOptionsExpandWidthFalse))
                    {
                        _searchText = string.Empty;
                        UpdateSearch();
                        SelectAndShowObject(SelectedTransform);
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUI.changed = false;
                    _searchNames = GUILayout.Toggle(_searchNames, _searchNamesContent);
                    _searchComponents.Value = GUILayout.Toggle(_searchComponents.Value, _searchComponentsContent);
                    _searchProperties.Value = GUILayout.Toggle(_searchProperties.Value, _searchPropertiesContent);
                    if (GUI.changed)
                        UpdateSearch();

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(_searchStaticsButtonContent, IMGUIUtils.LayoutOptionsExpandWidthFalse))
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

                            var stackEntries = matchedTypes.Select(t => new StaticStackEntry(t, t.GetSourceCodeRepresentation())).ToList();

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
            GUILayout.EndVertical();
        }

        #region Scene stuff

        private readonly ImguiComboBox _sceneDropdown = new ImguiComboBox();
        private static readonly GUIContent _sceneDropdownAllScenesContent = new GUIContent("All scenes", null, "Show GameObjects from all loaded scenes.\n\nSelect a scene from the dropdown to only show GameObjects that belong to that scene (they will be destroyed if the scene is unloaded).");
        private GUIContent _sceneDropdownCurrentContent = _sceneDropdownAllScenesContent;
        private readonly GUIContent _sceneUnloadButtonContent = new GUIContent("Unload", null, "Attempt to unload currently selected scene. It may fail if there is only one scene loaded.");
        private readonly GUIContent _sceneLabelContent = new GUIContent("Scene: ", null, "Multiple scenes can be loaded at the same time, in which case objects that belong to them all exist at the same time." +
                                                                                   "\nThe dropdown lists all currently loaded scenes. Select one of them to only show objects that belong to it." +
                                                                                   "\n\nWhen a scene is unloaded, all of its objects are marked to be destroyed (in some cases they are not cleaned up until Resources.UnloadUnusedResources).");
        private void DisplaySceneControls()
        {
            if (!UnityFeatureHelper.SupportsScenes) return;

            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                GUILayout.Label(_sceneLabelContent, IMGUIUtils.LayoutOptionsExpandWidthFalse);

                var loadedScenes = Enumerable.Range(0, UnityFeatureHelper.sceneCount).Select(UnityFeatureHelper.GetSceneAt);

                _sceneDropdown.Show(_sceneDropdownCurrentContent,
                                    () => Enumerable.Repeat(_sceneDropdownAllScenesContent, 1).Concat(loadedScenes.Select((x, i) => GetSceneContent(i, x))).ToArray(),
                                    i =>
                                    {
                                        try
                                        {
                                            var sceneIndex = i - 1;
                                            _sceneDropdownCurrentContent = sceneIndex < 0 ? _sceneDropdownAllScenesContent : GetSceneContent(sceneIndex, UnityFeatureHelper.GetSceneAt(sceneIndex));
                                            _gameObjectSearcher.SceneIndexFilter = sceneIndex;

                                            UpdateSearch();
                                        }
                                        catch (Exception e)
                                        {
                                            _sceneDropdownCurrentContent = _sceneDropdownAllScenesContent;
                                            _gameObjectSearcher.SceneIndexFilter = -1;
                                            RuntimeUnityEditorCore.Logger.Log(LogLevel.Message | LogLevel.Warning, "Failed to select scene from dropdown, it may have been unloaded");
                                            RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
                                        }
                                    });

                var guiEnabled = GUI.enabled;
                if (_gameObjectSearcher.SceneIndexFilter < 0 || _gameObjectSearcher.SceneIndexFilter >= UnityFeatureHelper.sceneCount) GUI.enabled = false;

                if (GUILayout.Button(_sceneUnloadButtonContent, IMGUIUtils.LayoutOptionsExpandWidthFalse))
                {
                    var scene = UnityFeatureHelper.GetSceneAt(_gameObjectSearcher.SceneIndexFilter);
                    if (scene.isLoaded)
                    {
                        UnityFeatureHelper.UnloadScene(scene.name);
                        _gameObjectSearcher.SceneIndexFilter = -1;
                        _sceneDropdownCurrentContent = _sceneDropdownAllScenesContent;
                    }
                }
                GUI.enabled = guiEnabled;

                //todo add scene manager
                //if (GUILayout.Button("Manage", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                //{
                //    
                //}
            }
            GUILayout.EndHorizontal();
            return;

            GUIContent GetSceneContent(int i, UnityFeatureHelper.SceneWrapper x) => new GUIContent($"#{i} {x.name}", null, x.ToString());
        }

        #endregion

        /// <summary>
        /// Switch the tree into reference search mode.
        /// </summary>
        public void FindReferencesInScene(object obj)
        {
            if (_gameObjectSearcher.SearchReferences(obj))
                _searchText = "Search for object references...";
        }

        /// <inheritdoc />
        protected override void Update()
        {
            if (_scrollTreeToSelected && _scrollTarget >= 0)
            {
                _scrollTreeToSelected = false;
                _treeScrollPosition.y = _scrollTarget;
                _scrollTarget = -1f;
            }

            _gameObjectSearcher.Refresh(false, null);
        }

        /// <inheritdoc />
        protected override void VisibleChanged(bool visible)
        {
            base.VisibleChanged(visible);

            ClearCaches();

            if (visible)
                _gameObjectSearcher.Refresh(true, null);
        }
    }
}
