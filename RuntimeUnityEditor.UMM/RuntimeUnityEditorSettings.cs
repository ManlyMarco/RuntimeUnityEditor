using System;
using System.Collections.Generic;
using UnityModManagerNet;
using UnityEngine;
using System.Xml.Serialization;
using System.IO;
using System.Xml;
using static RuntimeUnityEditor.UMM.RuntimeUnityEditorSettings;

namespace RuntimeUnityEditor.UMM
{
    [XmlInclude(typeof(XmlRect))]
    public class RuntimeUnityEditorSettings : UnityModManager.ModSettings, IDrawable
    {
        static Type[] intTypes = new[] { typeof(int), typeof(long), typeof(int[]), typeof(long[]) };
        static Type[] floatTypes = new[] { typeof(float), typeof(double), typeof(float[]), typeof(double[]) };
        static Type[] vectorTtype = new[] { typeof(Vector2), typeof(Vector3), typeof(Vector4) };
        static Type[] specialTypes = new[] { typeof(string), typeof(Color), typeof(KeyCode), typeof(Rect) };

        public void OnChange() { }
        [Draw]
        private Dictionary<string, Dictionary<string, SettingBase>> Categories = new Dictionary<string, Dictionary<string, SettingBase>>();

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

        override public void Save(UnityModManager.ModEntry entry)
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
            //UnityModManager.UI.DrawFields(ref RuntimeUnityEditorUMM.Settings, entry, DrawFieldMask.OnlyDrawAttr);
            foreach (string categoryName in Categories.Keys)
            {
                if (categoryName == "Windows") continue;
                GUILayout.Label(categoryName);
                foreach (string settingName in Categories[categoryName].Keys)
                {
                    var setting = Categories[categoryName];
                    var value = Categories[categoryName][settingName];
                    var type = value.GetType();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(settingName);

                    if (Array.Exists<Type>(intTypes, (x) => x == type))
                    {
                        if (type.IsArray)
                        {
                            /*foreach (var item in value as object[])
                            {

                            }*/
                        } else
                        {
                            float newValue = (float)Convert.ToDouble(value);
                            if (UnityModManager.UI.DrawFloatField(ref newValue, settingName)) setting[settingName].BoxedValue = newValue;
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        public abstract class SettingBase
        {
            [XmlIgnore]
            public abstract object BoxedValue { get; set; }
            [XmlIgnore]
            Type type;
        }

        public class Setting<T> : SettingBase
        {
            internal Setting() { }
            public Setting(T value) { Value = value; }
            private T _value;

            public T Value
            {
                get => _value;
                set { _value = value; }

            }
            [XmlIgnore]
            public override object BoxedValue { get => Value; set => Value = (T) value; }
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
        public class SerializableValueBase { }
        public class SerializableValue<T> : SerializableValueBase
        {
            T Value;
        }
        //[XmlInclude(typeof(Setting<XmlRect>))]
        //[XmlInclude(typeof(Setting<UnityEngine.KeyCode>))]
        //[XmlInclude(typeof(Setting<bool>))]
        //[XmlInclude(typeof(Setting<int>))]
        //[XmlInclude(typeof(Setting<float>))]
        //[XmlInclude(typeof(Setting<string>))]
        //[XmlInclude(typeof(Setting<UnityEngine.Rect>))]
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
