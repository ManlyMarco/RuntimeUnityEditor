using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using RuntimeUnityEditor.Core.Clipboard;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector
{
    internal static class VariableFieldDrawer
    {
        public static Dictionary<Type, Action<ICacheEntry, object>> SettingDrawHandlers { get; }

        private static readonly Dictionary<ICacheEntry, ComboBox> _comboBoxCache = new Dictionary<ICacheEntry, ComboBox>();
        private static readonly Dictionary<ICacheEntry, ColorCacheEntry> _colorCache = new Dictionary<ICacheEntry, ColorCacheEntry>();

        private static object _currentlyEditingTag;
        private static string _currentlyEditingText;
        private static bool _userHasHitReturn;

        static VariableFieldDrawer()
        {
            SettingDrawHandlers = new Dictionary<Type, Action<ICacheEntry, object>>
            {
                {typeof(bool), DrawBoolField},
                {typeof(Color), DrawColor },
                {typeof(Vector2), DrawVector2 },
                {typeof(Vector3), DrawVector3 },
                {typeof(Vector4), DrawVector4 },
                {typeof(Quaternion), DrawQuaternion },
                {typeof(Sprite), DrawSprite },
                {typeof(UnityEngine.UI.Image), DrawImage },
            };
        }

        public static void DrawSettingValue(ICacheEntry setting, object value)
        {
            if (Event.current.isKey && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)) _userHasHitReturn = true;

            if (setting is MethodCacheEntry mce)
            {
                DrawInvokeField(mce);
            }
            else
            {
                var canSetValue = setting.CanSetValue();
                if (!canSetValue) GUI.color = Color.gray;

                var t = setting.Type();
                if (t.IsEnum)
                {
                    if (t.GetCustomAttributes(typeof(FlagsAttribute), false).Any())
                        DrawFlagsField(setting, Enum.GetValues(t), value);
                    else
                        DrawComboboxField(setting, Enum.GetValues(t), value);
                }
                else
                {
                    if (setting.CanEnterValue())
                    {
                        if (value is Texture)
                        {
                            DrawAnyTexture(value);
                            goto wasDrawn;
                        }
                        else if (SettingDrawHandlers.TryGetValue(setting.Type(), out var drawMethod))
                        {
                            drawMethod(setting, value);
                            goto wasDrawn;
                        }
                    }

                    if (canSetValue && ToStringConverter.CanEditValue(setting, value))
                        DrawGenericEditableValue(setting, value, GUILayout.ExpandWidth(true));
                    else
                        DrawUnknownField(value);

                    wasDrawn:;
                }

                GUI.color = Color.white;
            }
        }
        
        private static void DrawUnknownField(object value)
        {
            GUILayout.TextArea(ToStringConverter.ObjectToString(value), GUI.skin.label, GUILayout.ExpandWidth(true));
        }

        public static void ClearCache()
        {
            _comboBoxCache.Clear();

            foreach (var tex in _colorCache)
                UnityEngine.Object.Destroy(tex.Value.Tex);
            _colorCache.Clear();
        }

        public static bool DrawCurrentDropdown()
        {
            if (ComboBox.CurrentDropdownDrawer != null)
            {
                ComboBox.CurrentDropdownDrawer.Invoke();
                ComboBox.CurrentDropdownDrawer = null;
                return true;
            }
            return false;
        }

        private static void DrawBoolField(ICacheEntry setting, object o)
        {
            var boolVal = (bool)setting.GetValue();
            var result = GUILayout.Toggle(boolVal, boolVal ? "True" : "False", GUILayout.ExpandWidth(true));
            if (result != boolVal)
                setting.SetValue(result);
        }

        private static void DrawFlagsField(ICacheEntry setting, IList enumValues, object fieldValue)
        {
            var currentValue = Convert.ToInt64(fieldValue);
            var allValues = enumValues.Cast<Enum>().Select(x => new { name = x.ToString(), val = Convert.ToInt64(x) }).ToArray();

            // Vertically stack Horizontal groups of the options to deal with the options taking more width than is available in the window
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            {
                for (var index = 0; index < allValues.Length;)
                {
                    GUILayout.BeginHorizontal();
                    {
                        var currentWidth = 0;
                        for (; index < allValues.Length; index++)
                        {
                            var value = allValues[index];

                            // Skip the 0 / none enum value, just uncheck everything to get 0
                            if (value.val != 0)
                            {
                                // Make sure this horizontal group doesn't extend over window width, if it does then start a new horiz group below
                                var textDimension = (int)GUI.skin.toggle.CalcSize(new GUIContent(value.name)).x;
                                currentWidth += textDimension;
                                if (currentWidth > 370)
                                    break;

                                GUI.changed = false;
                                var newVal = GUILayout.Toggle((currentValue & value.val) == value.val, value.name,
                                    GUILayout.ExpandWidth(false));
                                if (GUI.changed)
                                {
                                    var newValue = newVal ? currentValue | value.val : currentValue & ~value.val;
                                    setting.SetValue(Enum.ToObject(setting.Type(), newValue));
                                }
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                GUI.changed = false;
            }
            GUILayout.EndVertical();
            // Make sure the reset button is properly spaced
            GUILayout.FlexibleSpace();
        }

        private static void DrawComboboxField(ICacheEntry setting, IList list, object value)
        {
            var buttonText = new GUIContent(value.ToString());
            var dispRect = GUILayoutUtility.GetRect(buttonText, GUI.skin.button, GUILayout.ExpandWidth(true));

            if (!_comboBoxCache.TryGetValue(setting, out var box))
            {
                box = new ComboBox(dispRect, buttonText, list.Cast<object>().Select(x => new GUIContent(x.ToString())).ToArray(), InterfaceMaker.CustomSkin.button, Inspector.MaxWindowY); //todo don't rely on Inspector.MaxWindowY
                _comboBoxCache[setting] = box;
            }
            else
            {
                box.Rect = dispRect;
                box.ButtonContent = buttonText;
            }

            box.Show(id =>
            {
                if (id >= 0 && id < list.Count)
                    setting.SetValue(list[id]);
            });
        }

        private static void DrawGenericEditableValue(ICacheEntry field, object value, params GUILayoutOption[] layoutParams)
        {
            var isBeingEdited = _currentlyEditingTag == field;
            var text = isBeingEdited ? _currentlyEditingText : ToStringConverter.GetEditValue(field, value);
            var result = GUILayout.TextField(text, layoutParams);

            if (!Equals(text, result) || isBeingEdited)
            {
                if (_userHasHitReturn)
                {
                    _currentlyEditingTag = null;
                    _userHasHitReturn = false;

                    ToStringConverter.SetEditValue(field, value, result);
                }
                else
                {
                    _currentlyEditingText = result;
                    _currentlyEditingTag = field;
                }
            }
        }

        private static void DrawVector2(ICacheEntry obj, object value)
        {
            var setting = (Vector2)value;
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            if (setting != copy) obj.SetValue(setting);
        }

        private static void DrawVector3(ICacheEntry obj, object value)
        {
            var setting = (Vector3)value;
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            if (setting != copy) obj.SetValue(setting);
        }

        private static void DrawVector4(ICacheEntry obj, object value)
        {
            var setting = (Vector4)value;
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            setting.w = DrawSingleVectorSlider(setting.w, "W");
            if (setting != copy) obj.SetValue(setting);
        }

        private static void DrawQuaternion(ICacheEntry obj, object value)
        {
            var setting = (Quaternion)value;
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            setting.w = DrawSingleVectorSlider(setting.w, "W");
            if (setting != copy) obj.SetValue(setting);
        }

        private static float DrawSingleVectorSlider(float setting, string label)
        {
            GUILayout.Label(label, GUILayout.ExpandWidth(false));
            float.TryParse(GUILayout.TextField(setting.ToString("F", CultureInfo.InvariantCulture), GUILayout.ExpandWidth(true)), NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
            return x;
        }

        private static void DrawColor(ICacheEntry obj, object value)
        {
            var setting = (Color)value;

            if (!_colorCache.TryGetValue(obj, out var cacheEntry))
            {
                cacheEntry = new ColorCacheEntry { Tex = new Texture2D(14, 14, TextureFormat.ARGB32, false), Last = setting };
                cacheEntry.Tex.FillTexture(setting);
                _colorCache[obj] = cacheEntry;
            }

            GUILayout.Label("R", GUILayout.ExpandWidth(false));
            setting.r = GUILayout.HorizontalSlider(setting.r, 0f, 1f, GUILayout.ExpandWidth(true));
            GUILayout.Label("G", GUILayout.ExpandWidth(false));
            setting.g = GUILayout.HorizontalSlider(setting.g, 0f, 1f, GUILayout.ExpandWidth(true));
            GUILayout.Label("B", GUILayout.ExpandWidth(false));
            setting.b = GUILayout.HorizontalSlider(setting.b, 0f, 1f, GUILayout.ExpandWidth(true));
            GUILayout.Label("A", GUILayout.ExpandWidth(false));
            setting.a = GUILayout.HorizontalSlider(setting.a, 0f, 1f, GUILayout.ExpandWidth(true));

            GUILayout.Space(4);

            GUI.changed = false;
            var isBeingEdited = _currentlyEditingTag == obj;
            var text = isBeingEdited ? _currentlyEditingText : TomlTypeConverter.ConvertToString(setting, typeof(Color));
            text = GUILayout.TextField(text, GUILayout.Width(75));
            if (GUI.changed && !text.Equals(TomlTypeConverter.ConvertToString(setting, typeof(Color))) || isBeingEdited)
            {
                if (_userHasHitReturn)
                {
                    _currentlyEditingTag = null;
                    _userHasHitReturn = false;

                    try { obj.SetValue(TomlTypeConverter.ConvertToValue<Color>(text)); }
                    catch { }
                }
                else
                {
                    _currentlyEditingText = text;
                    _currentlyEditingTag = obj;
                }
            }

            if (setting != cacheEntry.Last)
            {
                obj.SetValue(setting);
                cacheEntry.Tex.FillTexture(setting);
                cacheEntry.Last = setting;
            }

            GUILayout.Label(cacheEntry.Tex, GUILayout.ExpandWidth(false));
        }

        private sealed class ColorCacheEntry
        {
            public Color Last;
            public Texture2D Tex;
        }

        private static void DrawAnyTexture(object obj)
        {
            var tex = (Texture)obj;

            var extraData = "";
            if (tex is Texture2D t2d) extraData = $"\nFormat={t2d.format} Mips={t2d.mipmapCount}";
            else if (tex is RenderTexture rt) extraData = $"\nFormat={rt.format} Mips={rt.useMipMap} AA={rt.antiAliasing} Depth={rt.depth} Cube={rt.isCubemap} Volume={rt.isVolume}";

            GUILayout.Label($"Name={tex.name} Size={tex.width}x{tex.height} Filter={tex.filterMode} Wrap={tex.wrapMode} {extraData}");

            GUILayout.FlexibleSpace();

            if (ObjectView.ObjectViewWindow.Initialized && GUILayout.Button("View", GUILayout.ExpandWidth(false)))
                ObjectView.ObjectViewWindow.Instance.SetShownObject(tex, tex.name);
        }

        private static void DrawSprite(ICacheEntry obj, object value)
        {
            var spr = (Sprite)value;

            DrawSprite(spr, spr.name);
        }

        private static void DrawSprite(Sprite spr, string objectName)
        {
            var extraData = "";
            if (spr.packed)
            {
                extraData += $"PackingMode={spr.packingMode}";
                if (spr.packingMode != SpritePackingMode.Tight)
                    extraData += $"TextureRect={spr.textureRect}";
            }

            GUILayout.Label($"Name={spr.name} Rect={spr.rect} Pivot={spr.pivot} Packed={spr.packed} {extraData}");

            GUILayout.FlexibleSpace();

            if (ObjectView.ObjectViewWindow.Initialized && GUILayout.Button("View", GUILayout.ExpandWidth(false)))
                ObjectView.ObjectViewWindow.Instance.SetShownObject(spr.GetVisibleTexture(), objectName);
        }

        private static void DrawImage(ICacheEntry obj, object value)
        {
            var img = (UnityEngine.UI.Image)value;
            DrawSprite(img.sprite, $"{img.transform.GetFullTransfromPath()} [Image ({img.name})]");
        }

        #region Method Invoke
        
        private static readonly GUIContent _buttonInvokeContent = new GUIContent("Invoke", "Execute this method. Will open a new window to let you specify any necessary parameters.");
        private static readonly int _currentlyInvokingWindowId = Core.RuntimeUnityEditorCore.Version.GetHashCode() + 80085;

        private static MethodCacheEntry _currentlyInvoking;
        private static object _currentlyInvokingResult;
        private static Exception _currentlyInvokingException;
        private static Rect _currentlyInvokingRect;
        private static Vector2 _currentlyInvokingPos;
        private static readonly List<string> _currentlyInvokingParams = new List<string>();
        private static readonly List<string> _currentlyInvokingArgs = new List<string>();

        private static void DrawInvokeField(MethodCacheEntry method)
        {
            if (GUILayout.Button(_buttonInvokeContent, GUILayout.ExpandWidth(false)))
                ShowInvokeWindow(method);

            GUILayout.Label(method.ParameterString, GUILayout.ExpandWidth(true));
        }

        public static void ShowInvokeWindow(MethodCacheEntry method)
        {
            if (_currentlyInvoking != method)
            {
                _currentlyInvoking = method;
                _currentlyInvokingResult = null;
                _currentlyInvokingException = null;
                _currentlyInvokingRect.Set(0, 0, 0, 0);
                _currentlyInvokingArgs.Clear();
                _currentlyInvokingParams.Clear();

                if (method != null)
                {
                    _currentlyInvokingArgs.AddRange(Enumerable.Repeat(string.Empty, method.MethodInfo.GetGenericArguments().Length));
                    _currentlyInvokingParams.AddRange(Enumerable.Repeat(string.Empty, method.MethodInfo.GetParameters().Length));
                }
            }
        }

        public static void DrawInvokeWindow()
        {
            if (_currentlyInvoking == null) return;

            if (_currentlyInvokingRect.height == 0 || _currentlyInvokingRect.width == 0)
            {
                var inspectorMidX = Inspector.Instance.WindowRect.xMin + Inspector.Instance.WindowRect.width / 2;
                var inspectorMidY = Inspector.Instance.WindowRect.yMin + Inspector.Instance.WindowRect.height / 2;
                const int w = 320;
                const int h = 240;
                _currentlyInvokingRect.Set(inspectorMidX - w / 2f, inspectorMidY - h / 2f, w, h);

                GUI.BringWindowToFront(_currentlyInvokingWindowId);
                GUI.FocusWindow(_currentlyInvokingWindowId);
            }

            _currentlyInvokingRect = GUILayout.Window(_currentlyInvokingWindowId, _currentlyInvokingRect, DrawInvokeWindowFunc, "Invoke " + _currentlyInvoking.Name());
        }

        private static void DrawInvokeWindowFunc(int id)
        {
            GUILayout.BeginVertical();
            {
                _currentlyInvokingPos = GUILayout.BeginScrollView(_currentlyInvokingPos, GUI.skin.box);
                {
                    const int indexColWidth = 25;

                    var generics = _currentlyInvoking.MethodInfo.GetGenericArguments();
                    if (generics.Length > 0)
                    {
                        GUILayout.Label("Generic arguments (Input a Type name, or pick a Type object from clipboard by typing #0, #1, #2...)");
                        for (var index = 0; index < generics.Length; index++)
                        {
                            var genericArg = generics[index];

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("#" + index, GUILayout.Width(indexColWidth));
                            GUILayout.Label(genericArg.FullDescription(), GUILayout.Width((_currentlyInvokingRect.width - indexColWidth) / 2.3f));
                            _currentlyInvokingArgs[index] = GUILayout.TextField(_currentlyInvokingArgs[index], GUILayout.ExpandWidth(true));
                            GUILayout.EndHorizontal();
                        }
                    }

                    var parameters = _currentlyInvoking.MethodInfo.GetParameters();
                    if (parameters.Length > 0)
                    {
                        GUILayout.Label("Parameters (Input a value, or pick a value from clipboard by typing #0, #1, #2...)");
                        for (var index = 0; index < parameters.Length; index++)
                        {
                            var parameter = parameters[index];

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("#" + index, GUILayout.Width(indexColWidth));
                            GUILayout.Label(parameter.ParameterType.FullDescription() + " " + parameter.Name, GUILayout.Width((_currentlyInvokingRect.width - indexColWidth) / 2.3f));
                            _currentlyInvokingParams[index] = GUILayout.TextField(_currentlyInvokingParams[index], GUILayout.ExpandWidth(true));
                            GUILayout.EndHorizontal();
                        }
                    }

                    if (generics.Length == 0 && parameters.Length == 0)
                        GUILayout.Label("This method has no parameters, click Invoke to run it.");
                }
                GUILayout.EndScrollView();

                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    GUILayout.Label("Invoke result: ", GUILayout.ExpandWidth(false));
                    GUILayout.TextArea(_currentlyInvokingException != null ? _currentlyInvokingException.GetType().Name + " - " + _currentlyInvokingException.Message : _currentlyInvokingResult == null ? "None / NULL" : _currentlyInvokingResult.ToString(), GUI.skin.label, GUILayout.ExpandWidth(true));
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    //todo
                    if (GUILayout.Button("Invoke"))
                    {
                        try
                        {
                            _currentlyInvokingException = null;

                            var methodInfo = _currentlyInvoking.MethodInfo;

                            if (_currentlyInvokingArgs.Count > 0)
                            {
                                var typeArgs = new Type[_currentlyInvokingArgs.Count];
                                for (var index = 0; index < _currentlyInvokingArgs.Count; index++)
                                {
                                    try
                                    {
                                        var arg = _currentlyInvokingArgs[index];
                                        typeArgs[index] = arg.StartsWith("#") ? (Type)ClipboardWindow.Contents[int.Parse(arg.Substring(1))] : AccessTools.TypeByName(arg);
                                    }
                                    catch (Exception e)
                                    {
                                        throw new ArgumentException($"Invalid generic argument #{index} - " + e.Message);
                                    }
                                }

                                methodInfo = methodInfo.MakeGenericMethod(typeArgs);
                            }

                            var methodParams = methodInfo.GetParameters();
                            var paramArgs = new object[_currentlyInvokingParams.Count];
                            for (var index = 0; index < _currentlyInvokingParams.Count; index++)
                            {
                                try
                                {
                                    var arg = _currentlyInvokingParams[index];
                                    var param = methodParams[index];
                                    var obj = arg.StartsWith("#") ? ClipboardWindow.Contents[int.Parse(arg.Substring(1))] : Convert.ChangeType(arg, param.ParameterType);
                                    paramArgs[index] = obj;
                                }
                                catch (Exception e)
                                {
                                    throw new ArgumentException($"Invalid parameter #{index} - " + e.Message);
                                }
                            }

                            _currentlyInvokingResult = methodInfo.Invoke(_currentlyInvoking.Instance, paramArgs);
                        }
                        catch (Exception e)
                        {
                            _currentlyInvokingResult = null;
                            _currentlyInvokingException = e;
                        }
                    }

                    GUILayout.FlexibleSpace();

                    GUI.enabled = _currentlyInvokingResult != null;
                    if (GUILayout.Button("Inspect result"))
                    {
                        Inspector.Instance.Push(new InstanceStackEntry(_currentlyInvokingResult, "Invoke " + _currentlyInvoking.Name(), _currentlyInvoking), false);
                        _currentlyInvoking = null;
                    }
                    if (GUILayout.Button("Copy result to clipboard"))
                    {
                        ClipboardWindow.Contents.Add(_currentlyInvokingResult);
                    }
                    GUI.enabled = true;

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Close"))
                        _currentlyInvoking = null;
                }
                GUILayout.EndHorizontal();

            }
            GUILayout.EndVertical();

            _currentlyInvokingRect = IMGUIUtils.DragResizeEat(id, _currentlyInvokingRect);
        }

        #endregion
    }
}
