using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RuntimeUnityEditor.Core.Gizmos;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RuntimeUnityEditor.Core.ObjectTree
{
    public sealed class ObjectTreeViewer : WindowBase<ObjectTreeViewer>
    {
        internal Action<InspectorStackEntryBase[]> InspectorOpenCallback;
        internal Action<Transform> TreeSelectionChangedCallback;

        private readonly HashSet<GameObject> _openedObjects = new HashSet<GameObject>();
        private Transform _selectedTransform;

        private Vector2 _propertiesScrollPosition;
        private Vector2 _treeScrollPosition;
        private float _objectTreeHeight;
        private int _singleObjectTreeItemHeight;

        private bool _scrollTreeToSelected;
        private int _scrollTarget;

        private readonly GameObjectSearcher _gameObjectSearcher;
        private readonly Dictionary<Image, Texture2D> _imagePreviewCache = new Dictionary<Image, Texture2D>();
        private readonly GUILayoutOption _drawVector3FieldWidth = GUILayout.Width(38);
        private readonly GUILayoutOption _drawVector3FieldHeight = GUILayout.Height(19);
        private readonly GUILayoutOption _drawVector3SliderHeight = GUILayout.Height(10);
        private readonly GUILayoutOption _drawVector3SliderWidth = GUILayout.Width(33);

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

        public ObjectTreeViewer(MonoBehaviour pluginObject, GameObjectSearcher gameObjectSearcher)
        {
            if (pluginObject == null) throw new ArgumentNullException(nameof(pluginObject));
            if (gameObjectSearcher == null) throw new ArgumentNullException(nameof(gameObjectSearcher));

            Title = "Scene Browser - RuntimeUnityEditor v" + RuntimeUnityEditorCore.Version;
            _gameObjectSearcher = gameObjectSearcher;
            pluginObject.StartCoroutine(SetWireframeCo());
        }

        private bool _wireframe;
        private bool _actuallyInsideOnGui;

        private readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();
        private IEnumerator SetWireframeCo()
        {
            while (true)
            {
                yield return null;

                _actuallyInsideOnGui = true;

                yield return _waitForEndOfFrame;

                if (GL.wireframe != _wireframe)
                    GL.wireframe = _wireframe;

                _actuallyInsideOnGui = false;
            }
        }

        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                if (value && !base.Enabled)
                    ClearCaches();

                base.Enabled = value;
            }
        }

        public Transform SelectedTransform
        {
            get => _selectedTransform;
            set
            {
                if (_selectedTransform != value)
                {
                    _selectedTransform = value;
                    _searchTextComponents = _gameObjectSearcher.IsSearching() ? _searchText : "";
                    TreeSelectionChangedCallback?.Invoke(_selectedTransform);
                }
            }
        }

        public void ClearCaches()
        {
            _imagePreviewCache.Clear();
        }

        private void OnInspectorOpen(params InspectorStackEntryBase[] items)
        {
            InspectorOpenCallback.Invoke(items);
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

        private void DisplayObjectTreeHelper(GameObject go, int indent, ref int currentCount)
        {
            currentCount++;

            var needsHeightMeasure = _singleObjectTreeItemHeight == 0;

            var isVisible = currentCount * _singleObjectTreeItemHeight >= _treeScrollPosition.y &&
                            (currentCount - 1) * _singleObjectTreeItemHeight <= _treeScrollPosition.y + _objectTreeHeight;

            if (isVisible || needsHeightMeasure || _scrollTreeToSelected)
            {
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
                GUILayout.Space(_singleObjectTreeItemHeight);
            }

            if (_openedObjects.Contains(go))
            {
                for (var i = 0; i < go.transform.childCount; ++i)
                    DisplayObjectTreeHelper(go.transform.GetChild(i).gameObject, indent + 1, ref currentCount);
            }
        }

        protected override void DrawContents()
        {
            if (_wireframe && _actuallyInsideOnGui && Event.current.type == EventType.layout)
                GL.wireframe = false;

            GUILayout.BeginVertical();
            {
                DisplayObjectTree();

                DisplayControls();

                DisplayObjectProperties();
            }
            GUILayout.EndVertical();
        }

        private void DisplayControls()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    GUILayout.Label("Time", GUILayout.ExpandWidth(false));

                    if (GUILayout.Button(">", GUILayout.ExpandWidth(false)))
                        Time.timeScale = 1;
                    if (GUILayout.Button("||", GUILayout.ExpandWidth(false)))
                        Time.timeScale = 0;

                    if (float.TryParse(GUILayout.TextField(Time.timeScale.ToString("F2", CultureInfo.InvariantCulture), _drawVector3FieldWidth), NumberStyles.Any, CultureInfo.InvariantCulture, out var newVal))
                        Time.timeScale = newVal;
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    if (GUILayout.Button("Log", GUILayout.ExpandWidth(false)))
                        UnityFeatureHelper.OpenLog();

                    GUILayout.FlexibleSpace();

                    var origColor = GUI.backgroundColor;

                    if (RuntimeUnityEditorCore.Instance.Inspector.Enabled)
                        GUI.backgroundColor = Color.cyan;
                    if (GUILayout.Button("Insp"))
                        RuntimeUnityEditorCore.Instance.Inspector.Enabled = !RuntimeUnityEditorCore.Instance.Inspector.Enabled;
                    GUI.backgroundColor = origColor;


                    if (RuntimeUnityEditorCore.Instance.Repl != null)
                    {
                        if (RuntimeUnityEditorCore.Instance.ShowRepl)
                            GUI.backgroundColor = Color.cyan;
                        if (GUILayout.Button("REPL"))
                            RuntimeUnityEditorCore.Instance.ShowRepl = !RuntimeUnityEditorCore.Instance.ShowRepl;

                        GUI.backgroundColor = origColor;
                    }

                    if (RuntimeUnityEditorCore.Instance.ProfilerWindow.Enabled)
                        GUI.backgroundColor = Color.cyan;
                    if (GUILayout.Button("Profiler"))
                        RuntimeUnityEditorCore.Instance.ProfilerWindow.Enabled = !RuntimeUnityEditorCore.Instance.ProfilerWindow.Enabled;
                    GUI.backgroundColor = origColor;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                GUI.changed = false;
                var n = GUILayout.Toggle(Application.runInBackground, "Run in bg");
                if (GUI.changed) Application.runInBackground = n;

                RuntimeUnityEditorCore.Instance.EnableMouseInspect = GUILayout.Toggle(RuntimeUnityEditorCore.Instance.EnableMouseInspect, "Mouse inspect");

                _wireframe = GUILayout.Toggle(_wireframe, "Wireframe");
            }
            GUILayout.EndHorizontal();

            AssetBundleManagerHelper.DrawButtonIfAvailable();

            GizmoDrawer.DisplayControls();
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

                        if (!string.IsNullOrEmpty(_searchTextComponents) && !GameObjectSearcher.SearchInComponent(_searchTextComponents, component, false))
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

                if (GUILayout.Button(component.GetType().Name, GUI.skin.label))
                {
                    var transform = component.transform;
                    OnInspectorOpen(new InstanceStackEntry(transform, transform.name),
                        new InstanceStackEntry(component, component.GetType().FullName));
                }

                switch (component)
                {
                    case Image img:
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
                                    if (GUILayout.Button(tex, GUI.skin.box)) RuntimeUnityEditorCore.Instance.PreviewWindow.SetShownObject(tex, imgSprite.name);
                                }
                                else
                                {
                                    GUILayout.Label("Can't display texture");
                                }
                            }
                        }
                        GUILayout.FlexibleSpace();
                        break;
                    case Slider b:
                        {
                            for (var i = 0; i < b.onValueChanged.GetPersistentEventCount(); ++i)
                                GUILayout.Label(ToStringConverter.EventEntryToString(b.onValueChanged, i));

                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("?"))
                                ReflectionUtils.OutputEventDetails(b.onValueChanged);
                            break;
                        }
                    case Text text:
                        GUILayout.Label(
                            $"{text.text} {text.font} {text.fontStyle} {text.fontSize} {text.alignment} {text.resizeTextForBestFit} {text.color}");
                        GUILayout.FlexibleSpace();
                        break;
                    case RawImage r:
                        var rMainTexture = r.mainTexture;
                        if (!ReferenceEquals(rMainTexture, null))
                        {
                            GUILayout.Label(rMainTexture);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("S")) rMainTexture.SaveTextureToFileWithDialog();
                        }
                        else
                        {
                            GUILayout.Label("Can't display texture");
                            GUILayout.FlexibleSpace();
                        }
                        break;
                    case Renderer re:
                        var reMaterial = re.material;
                        GUILayout.Label(reMaterial != null ? reMaterial.shader.name : "[No material]");
                        GUILayout.FlexibleSpace();
                        if (reMaterial != null && reMaterial.mainTexture != null)
                        {
                            var rendTex = reMaterial.mainTexture;
                            GUILayout.Label(rendTex);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("S")) rendTex.SaveTextureToFileWithDialog();
                        }
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
                                ReflectionUtils.OutputEventDetails(b.onClick);
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
                                ReflectionUtils.OutputEventDetails(b.onValueChanged);
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

                if (!(component is Transform))
                {
                    /*if (GUILayout.Button("R"))
                    {
                        var t = component.GetType();
                        var g = component.gameObject;

                        IEnumerator RecreateCo()
                        {
                            Object.Destroy(component);
                            yield return null;
                            g.AddComponent(t);
                        }

                        Object.FindObjectOfType<CheatTools>().StartCoroutine(RecreateCo());
                    }*/

                    if (GUILayout.Button("X"))
                    {
                        Object.Destroy(component);
                    }
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
                    foreach (var rootGameObject in _gameObjectSearcher.GetSearchedOrAllObjects())
                        DisplayObjectTreeHelper(rootGameObject, 0, ref currentCount);
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
                    _searchTextComponents = _searchText;
                }

                if (Event.current.isKey && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "searchbox")
                {
                    _gameObjectSearcher.Search(_searchText, false);
                    _searchTextComponents = _searchText;
                    Event.current.Use();
                }

                if (GUILayout.Button("Deep scene"))
                {
                    _gameObjectSearcher.Search(_searchText, true);
                    _searchTextComponents = _searchText;
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
                            RuntimeUnityEditorCore.Logger.Log(LogLevel.Message | LogLevel.Warning, "No static type names contained the search string");
                        else if (stackEntries.Count == 1)
                            RuntimeUnityEditorCore.Instance.Inspector.Push(stackEntries.Single(), true);
                        else
                            RuntimeUnityEditorCore.Instance.Inspector.Push(new InstanceStackEntry(stackEntries, "Static type search"), true);
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        public void Update()
        {
            if (_scrollTreeToSelected && _scrollTarget > 0)
            {
                _scrollTreeToSelected = false;
                _treeScrollPosition.y = _scrollTarget;
                _scrollTarget = 0;
            }
        }
    }
}
