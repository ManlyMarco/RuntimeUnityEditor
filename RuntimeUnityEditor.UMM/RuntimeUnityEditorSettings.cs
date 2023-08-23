using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using UnityEngine;
using UnityModManagerNet;
#pragma warning disable CS0618

namespace RuntimeUnityEditor.UMM
{
    [XmlInclude(typeof(XmlRect))]
    public class RuntimeUnityEditorSettings : UnityModManager.ModSettings
    {
        private readonly Dictionary<string, Dictionary<string, SettingBase>> Categories = new Dictionary<string, Dictionary<string, SettingBase>>();

        public void Add(string category, string settingName, SettingBase setting)
        {
            if (!Categories.ContainsKey(category)) Categories.Add(category, new Dictionary<string, SettingBase>());
            Categories[category][settingName] = setting;
        }

        public bool Get<T>(string categoryName, string settingName, out Setting<T> setting)
        {
            if (Categories.TryGetValue(categoryName, out Dictionary<string, SettingBase> category) && category.TryGetValue(settingName, out var _setting))
            {
                setting = (Setting<T>) _setting;
                return true;
            }

            setting = default;
            return false;
        }

        [XmlElement("Category")]
        public List<SerializableCategory> SerializableSettings { get; set; }

        public override void Save(UnityModManager.ModEntry entry)
        {
            SerializableSettings = new List<SerializableCategory>();
            var filepath = GetPath(RuntimeUnityEditorUMM.Instance);
            foreach (string key in Categories.Keys)
            {
                List<SerializableSetting> settings = new List<SerializableSetting>();
                foreach (string setting in Categories[key].Keys)
                {
                    if (Categories[key][setting].GetType() == typeof(Setting<Rect>))
                    {
                        settings.Add(new SerializableSetting() { Key = setting, Value = new Setting<XmlRect>(new XmlRect((Categories[key][setting] as Setting<Rect>).Value)) });
                    }
                    else
                    {
                        settings.Add(new SerializableSetting() { Key = setting, Value = Categories[key][setting] });
                    }
                }
                SerializableSettings.Add(new SerializableCategory() { Name = key, Settings = settings });
            }
            Save(this, entry);

            try
            {
                using (var writer = new StreamWriter(filepath))
                {
                    var serializer = new XmlSerializer(typeof(RuntimeUnityEditorSettings));
                    serializer.Serialize(writer, RuntimeUnityEditorUMM.Settings);
                }
            }
            catch (Exception e)
            {
                RuntimeUnityEditorUMM.Instance.Logger.Error("Error writing to Settings.xml");
                RuntimeUnityEditorUMM.Instance.Logger.LogException(e);
            }
        }

        public void Load(UnityModManager.ModEntry entry)
        {
            if (SerializableSettings == null) return;

            foreach (SerializableCategory category in SerializableSettings)
            {          
                foreach (SerializableSetting setting in category.Settings)
                {
                    if (setting.Value is Setting<XmlRect> r)
                        Add(category.Name, setting.Key, new Setting<Rect>(new Rect(r.Value.x, r.Value.y, r.Value.width, r.Value.height)));
                    else
                        Add(category.Name, setting.Key, setting.Value);
                }
            }
        }

        public void Draw(UnityModManager.ModEntry entry)
        {
            foreach (string categoryName in Categories.Keys)
            {
                if (categoryName == "Windows") continue;
                
                GUILayout.Label(categoryName, UnityModManager.UI.bold);
                GUILayout.BeginVertical("box");

                foreach (string settingName in Categories[categoryName].Keys)
                {
                    var setting = Categories[categoryName][settingName];

                    GUILayout.BeginHorizontal();

                    if (setting is Setting<int> intSetting)
                    {
                        var value = intSetting.Value;
                        if (UnityModManager.UI.DrawIntField(ref value, settingName)) intSetting.Value = value;
                    }
                    else if (setting is Setting<float> floatSetting)
                    {
                        var value = floatSetting.Value;
                        if (UnityModManager.UI.DrawFloatField(ref value, settingName)) floatSetting.Value = value;
                    }
                    else if (setting is Setting<bool> boolSetting)
                    {
                        var value = GUILayout.Toggle(boolSetting.Value, settingName);
                        if (value != boolSetting.Value) boolSetting.Value = value;
                    }
                    else if (setting is Setting<UnityEngine.KeyCode> keycodeSetting)
                    {
                        GUILayout.Label(settingName, GUILayout.ExpandWidth(false));

                        var value = new KeyBinding() { keyCode = keycodeSetting.Value };
                        if (UnityModManager.UI.DrawKeybinding(ref value, settingName)) keycodeSetting.Value = value.keyCode;
                        GUILayout.FlexibleSpace();
                    }
                    else if (setting is Setting<string> stringSetting)
                    {
                        GUILayout.BeginVertical();
                        GUILayout.Label(settingName, GUILayout.ExpandWidth(false));

                        var value = GUILayout.TextField(stringSetting.Value, GUILayout.ExpandWidth(true));
                        if (value != stringSetting.Value) stringSetting.Value = value;
                        GUILayout.EndVertical();
                    }
                    else
                    {
                        GUILayout.Label($"Unknown config entry type: {setting.type.ToString()}");
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
        }

        public abstract class SettingBase
        {
            [XmlIgnore]
            public abstract object BoxedValue { get; set; }
            [XmlIgnore]
            public abstract Type type { get; }
        }

        public class Setting<T> : SettingBase
        {
            internal Setting() { }
            public Setting(T value) { Value = value; }
            private T _value;

            public T Value
            {
                get => _value;
                set 
                {
                    if (Equals(_value, value)) return;

                    _value = value;
                    OnChanged?.Invoke(value);
                }
            }

            [XmlIgnore]
            public override object BoxedValue { get => Value; set => Value = (T) value; }
            [XmlIgnore]
            public override Type type { get { return typeof(T); } }
            [XmlIgnore]
            public Action<T> OnChanged;
        }

        public class SerializableCategory
        {
            public SerializableCategory() { }
            [XmlAttribute]
            public string Name;
            [XmlElement("Setting")]
            public List<SerializableSetting> Settings;
        }

        public class SerializableSetting
        {
            [XmlAttribute("Name")]
            public string Key;
            [XmlElement(typeof(Setting<XmlRect>), ElementName = "Rect")]
            [XmlElement(typeof(Setting<UnityEngine.KeyCode>), ElementName = "KeyCode")]
            [XmlElement(typeof(Setting<bool>), ElementName = "bool")]
            [XmlElement(typeof(Setting<int>), ElementName = "int")]
            [XmlElement(typeof(Setting<float>), ElementName = "float")]
            [XmlElement(typeof(Setting<string>), ElementName = "text")]
            public SettingBase Value;
        }

        public class XmlRect
        {
            public XmlRect() { }
            public XmlRect(Rect r)
            {
                x = r.x; y = y = r.y; width = r.width; height = r.height;
            }

            public float x;
            public float y;
            public float width;
            public float height;

            Rect ToRect()
            {
                return new Rect(x, y, width, height);
            }
        }
    }
}
