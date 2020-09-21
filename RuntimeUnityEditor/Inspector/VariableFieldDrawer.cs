using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector
{
    internal class VariableFieldDrawer
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
            };
        }

        public static void DrawSettingValue(ICacheEntry setting, object value)
        {
            if (Event.current.isKey && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)) _userHasHitReturn = true;

            if (setting is MethodCacheEntry)
                DrawUnknownField(value);
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
                else if (setting.CanEnterValue() && SettingDrawHandlers.TryGetValue(setting.Type(), out var drawMethod))
                    drawMethod(setting, value);
                else
                {
                    if (canSetValue && ToStringConverter.CanEditValue(setting, value))
                        DrawGenericEditableValue(setting, value, GUILayout.ExpandWidth(true));
                    else
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
                                if (currentWidth > 390)
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
                box = new ComboBox(dispRect, buttonText, list.Cast<object>().Select(x => new GUIContent(x.ToString())).ToArray(), InterfaceMaker.CustomSkin.button, Inspector.MaxWindowY);
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
    }
}
