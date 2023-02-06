using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
                DrawMethodInvokeField(mce);
            }
            else if (setting is EventCacheEntry ece)
            {
                DrawEventInvokeField(ece);
            }
            else
            {
                var canSetValue = setting.CanSetValue();
                if (!canSetValue) GUI.color = Color.gray;

                var settingType = setting.Type();
                if (settingType.IsEnum)
                {
                    if (settingType.GetCustomAttributes(typeof(FlagsAttribute), false).Any())
                        DrawFlagsField(setting, Enum.GetValues(settingType), value);
                    else
                        DrawComboboxField(setting, Enum.GetValues(settingType), value);
                }
                else if (typeof(Delegate).IsAssignableFrom(settingType))
                {
                    DrawDelegateField(setting);
                }
                else if (value is Texture)
                {
                    DrawAnyTexture(value);
                }
                else if (SettingDrawHandlers.TryGetValue(settingType, out var drawMethod))
                {
                    drawMethod(setting, value);
                }
                else if (canSetValue && ToStringConverter.CanEditValue(setting, value))
                {
                    DrawGenericEditableValue(setting, value, GUILayout.ExpandWidth(true));
                }
                else
                {
                    DrawUnknownField(value);
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
            GUI.changed = false;
            var result = GUILayout.TextField(text, layoutParams);

            if ((GUI.changed && !Equals(text, result)) || isBeingEdited)
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
            GUI.changed = false;
            var setting = (Vector2)value;
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            if (GUI.changed && setting != copy) obj.SetValue(setting);
        }

        private static void DrawVector3(ICacheEntry obj, object value)
        {
            GUI.changed = false;
            var setting = (Vector3)value;
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            if (GUI.changed && setting != copy) obj.SetValue(setting);
        }

        private static void DrawVector4(ICacheEntry obj, object value)
        {
            GUI.changed = false;
            var setting = (Vector4)value;
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            setting.w = DrawSingleVectorSlider(setting.w, "W");
            if (GUI.changed && setting != copy) obj.SetValue(setting);
        }

        private static void DrawQuaternion(ICacheEntry obj, object value)
        {
            GUI.changed = false;
            var setting = (Quaternion)value;
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            setting.w = DrawSingleVectorSlider(setting.w, "W");
            if (GUI.changed && setting != copy) obj.SetValue(setting);
        }

        private static float DrawSingleVectorSlider(float setting, string label)
        {
            GUILayout.Label(label, GUILayout.ExpandWidth(false));
            float.TryParse(GUILayout.TextField(setting.ToString("F4", CultureInfo.InvariantCulture), GUILayout.ExpandWidth(true)), NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
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

            DrawSprite(spr, spr?.name);
        }

        private static void DrawSprite(Sprite spr, string objectName)
        {
            var isNullOrDestroyed = spr.IsNullOrDestroyed();
            if (isNullOrDestroyed != null)
            {
                GUILayout.Label(isNullOrDestroyed);
                GUILayout.FlexibleSpace();
                return;
            }

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
            DrawSprite(img?.sprite, $"{img?.transform.GetFullTransfromPath()} [Image ({img?.name})]");
        }

        #region Method Invoke

        private static readonly GUIContent _buttonInvokeContent = new GUIContent("Invoke", "Execute this method. Will open a new window to let you specify any necessary parameters.");
        private static readonly int _currentlyInvokingWindowId = Core.RuntimeUnityEditorCore.Version.GetHashCode() + 80085;

        private static MethodInfo _currentlyInvoking;
        private static object _currentlyInvokingInstance;
        private static object _currentlyInvokingResult;
        private static Exception _currentlyInvokingException;
        private static Rect _currentlyInvokingRect;
        private static Vector2 _currentlyInvokingPos;
        private static ParameterInfo[] _currentlyInvokingParams;
        private static Type[] _currentlyInvokingArgs;
        private static readonly List<string> _currentlyInvokingParamsValues = new List<string>();
        private static readonly List<string> _currentlyInvokingArgsValues = new List<string>();

        private static void DrawMethodInvokeField(MethodCacheEntry method)
        {
            if (GUILayout.Button(_buttonInvokeContent, GUILayout.ExpandWidth(false)))
                ShowInvokeWindow(method.MethodInfo, method.Instance);

            GUILayout.Label(method.ParameterString, GUILayout.ExpandWidth(true));
        }
        private static void DrawEventInvokeField(EventCacheEntry @event)
        {
            var eventInfo = @event.EventInfo;

            GUILayout.Label("Invoke event method: ", GUILayout.ExpandWidth(false));
            if (GUILayout.Button("Add", GUILayout.ExpandWidth(false)))
                ShowInvokeWindow(eventInfo.GetAddMethod(true), @event.Instance);

            if (GUILayout.Button("Remove", GUILayout.ExpandWidth(false)))
                ShowInvokeWindow(eventInfo.GetRemoveMethod(true), @event.Instance);

            // Raise method is always null in C# assemblies, but exists if assembly was compiled from VB.NET, F# or C++/CLI
            var raiseMethod = eventInfo.GetRaiseMethod(true);
            if (raiseMethod != null && GUILayout.Button("Raise", GUILayout.ExpandWidth(false)))
                ShowInvokeWindow(raiseMethod, @event.Instance);

            var backingDelegate = (Delegate)@event.GetValue();
            if (backingDelegate != null)
            {
                GUILayout.Space(10);
                //if (GUILayout.Button("Raise", GUILayout.ExpandWidth(false)))
                //    ShowInvokeWindow(backingDelegate.DynamicInvoke(), @event.Instance);

                var invocationList = backingDelegate.GetInvocationList();
                if (GUILayout.Button(invocationList.Length + " listener(s)"))
                    Inspector.Instance.Push(new InstanceStackEntry(invocationList, @event.Name() + " Invocation List", @event), false);
            }

            GUILayout.FlexibleSpace();

            //GUILayout.Label(method.ParameterString, GUILayout.ExpandWidth(true));
        }

        public static void ShowInvokeWindow(MethodInfo method, object instance, Type[] genericArguments = null, ParameterInfo[] parameters = null)
        {
            if (_currentlyInvoking != method)
            {
                _currentlyInvoking = method;
                _currentlyInvokingInstance = instance;
                _currentlyInvokingResult = null;
                _currentlyInvokingException = null;
                _currentlyInvokingRect.Set(0, 0, 0, 0);
                _currentlyInvokingArgs = genericArguments ?? method?.GetGenericArguments();
                _currentlyInvokingParams = parameters ?? method?.GetParameters();
                _currentlyInvokingArgsValues.Clear();
                _currentlyInvokingParamsValues.Clear();
                if (method != null)
                {
                    _currentlyInvokingArgsValues.AddRange(Enumerable.Repeat(string.Empty, _currentlyInvokingArgs.Length));
                    _currentlyInvokingParamsValues.AddRange(Enumerable.Repeat(string.Empty, _currentlyInvokingParams.Length));
                }
            }
        }

        private static void DrawDelegateField(ICacheEntry cacheEntry)
        {
            var v = (Delegate)cacheEntry.GetValue();

            if (v == null)
            {
                GUILayout.Label("NULL / Empty", GUILayout.ExpandWidth(true));
                return;
            }

            var t = cacheEntry.Type();
            // todo better handle parameters
            var invokeM = t.GetMethod("Invoke", AccessTools.all);
            var dynInvokeM = t.GetMethod(nameof(v.DynamicInvoke), AccessTools.all);

            var invocationList = v.GetInvocationList();

            //todo v.Method is null, work on delegate directly
            if (GUILayout.Button(_buttonInvokeContent, GUILayout.ExpandWidth(false)))
            {
                if (invokeM != null)
                    ShowInvokeWindow(invokeM, v);
                else
                    ShowInvokeWindow(dynInvokeM, v, null, v.Method.GetParameters());
            }

            if (GUILayout.Button(invocationList.Length + " delegate(s)", GUILayout.ExpandWidth(false)))
                Inspector.Instance.Push(new InstanceStackEntry(invocationList, cacheEntry.Name() + " Invocation List", cacheEntry), false);

            GUILayout.Label(MethodCacheEntry.GetParameterPreviewString(invokeM), GUILayout.ExpandWidth(true));
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

            _currentlyInvokingRect = GUILayout.Window(_currentlyInvokingWindowId, _currentlyInvokingRect, DrawInvokeWindowFunc, "Invoke " + _currentlyInvoking.Name);
        }

        private static void DrawInvokeWindowFunc(int id)
        {
            GUILayout.BeginVertical();
            {
                _currentlyInvokingPos = GUILayout.BeginScrollView(_currentlyInvokingPos, GUI.skin.box);
                {
                    const int indexColWidth = 25;

                    var generics = _currentlyInvokingArgs;
                    if (generics.Length > 0)
                    {
                        GUILayout.Label("Generic arguments (Input a Type name, or pick a Type object from clipboard by typing #0, #1, #2...)");
                        for (var index = 0; index < generics.Length; index++)
                        {
                            var genericArg = generics[index];

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("#" + index, GUILayout.Width(indexColWidth));
                            GUILayout.Label(genericArg.FullDescription(), GUILayout.Width((_currentlyInvokingRect.width - indexColWidth) / 2.3f));
                            _currentlyInvokingArgsValues[index] = GUILayout.TextField(_currentlyInvokingArgsValues[index], GUILayout.ExpandWidth(true));
                            GUILayout.EndHorizontal();
                        }
                    }

                    var parameters = _currentlyInvokingParams;
                    if (parameters.Length > 0)
                    {
                        GUILayout.Label("Parameters (Input a value, or pick a value from clipboard by typing #0, #1, #2...)");
                        for (var index = 0; index < parameters.Length; index++)
                        {
                            var parameter = parameters[index];

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("#" + index, GUILayout.Width(indexColWidth));
                            GUILayout.Label(parameter.ParameterType.FullDescription() + " " + parameter.Name, GUILayout.Width((_currentlyInvokingRect.width - indexColWidth) / 2.3f));
                            _currentlyInvokingParamsValues[index] = GUILayout.TextField(_currentlyInvokingParamsValues[index], GUILayout.ExpandWidth(true));
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

                            var methodInfo = _currentlyInvoking;

                            if (_currentlyInvokingArgs.Length > 0)
                            {
                                var typeArgs = new Type[_currentlyInvokingArgs.Length];
                                for (var index = 0; index < _currentlyInvokingArgs.Length; index++)
                                {
                                    try
                                    {
                                        var arg = _currentlyInvokingArgsValues[index];
                                        typeArgs[index] = arg.StartsWith("#") ? (Type)ClipboardWindow.Contents[int.Parse(arg.Substring(1))] : AccessTools.TypeByName(arg);
                                    }
                                    catch (Exception e)
                                    {
                                        throw new ArgumentException($"Invalid generic argument #{index} - " + e.Message);
                                    }
                                }

                                methodInfo = methodInfo.MakeGenericMethod(typeArgs);
                            }

                            var methodParams = _currentlyInvokingParams;
                            var paramArgs = new object[_currentlyInvokingParams.Length];
                            for (var index = 0; index < _currentlyInvokingParams.Length; index++)
                            {
                                try
                                {
                                    var arg = _currentlyInvokingParamsValues[index];
                                    var param = methodParams[index];
                                    var obj = arg.StartsWith("#") ? ClipboardWindow.Contents[int.Parse(arg.Substring(1))] : Convert.ChangeType(arg, param.ParameterType);
                                    paramArgs[index] = obj;
                                }
                                catch (Exception e)
                                {
                                    throw new ArgumentException($"Invalid parameter #{index} - " + e.Message);
                                }
                            }

                            _currentlyInvokingResult = methodInfo.Invoke(_currentlyInvokingInstance, methodInfo.Name == "DynamicInvoke" ? new object[] { paramArgs } : paramArgs);
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
                        Inspector.Instance.Push(new InstanceStackEntry(_currentlyInvokingResult, "Invoke " + _currentlyInvoking.Name), false);
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
