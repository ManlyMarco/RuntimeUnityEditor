using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils
{
    /// <summary>
    /// Based on https://github.com/BepInEx/BepInEx/blob/master/BepInEx/Configuration/TomlTypeConverter.cs
    /// Original is under MIT License - Copyright(c) 2018 Bepis
    /// </summary>
    internal static class TomlTypeConverter
    {
        /// <summary>
        /// A serializer/deserializer combo for some type(s). Used by the config system.
        /// </summary>
        public class TypeConverter
        {
            /// <summary>
            /// Used to serialize the type into a (hopefully) human-readable string.
            /// Object is the instance to serialize, Type is the object's type.
            /// </summary>
            public Func<object, Type, string> ConvertToString { get; set; }

            /// <summary>
            /// Used to deserialize the type from a string.
            /// String is the data to deserialize, Type is the object's type, should return instance to an object of Type.
            /// </summary>
            public Func<string, Type, object> ConvertToObject { get; set; }
        }

        private static Dictionary<Type, TypeConverter> TypeConverters = new Dictionary<Type, TypeConverter>();

        static TomlTypeConverter()
        {
            AddConverter(typeof(Enum), new TypeConverter
            {
                ConvertToString = (obj, type) => obj.ToString(),
                ConvertToObject = (str, type) => Enum.Parse(type, str, true),
            });

            AddConverter(typeof(Color), new TypeConverter
            {
                ConvertToString = (obj, type) => ColorUtility.ToHtmlStringRGBA((Color)obj),
                ConvertToObject = (str, type) =>
                {
                    if (!ColorUtility.TryParseHtmlString("#" + str.Trim('#', ' '), out var c))
                        throw new FormatException("Invalid color string, expected hex #RRGGBBAA");
                    return c;
                },
            });
            
            AddConverter(typeof(Vector2), new TypeConverter
            {
                ConvertToString = (obj, type) => ToStringUtility.ObjectToString((Vector2)obj),
                ConvertToObject = (str, type) => ToStringUtility.StringToVector2(str),
            });
            AddConverter(typeof(Vector3), new TypeConverter
            {
                ConvertToString = (obj, type) => ToStringUtility.ObjectToString((Vector3)obj),
                ConvertToObject = (str, type) => ToStringUtility.StringToVector3(str),
            });
            AddConverter(typeof(Vector4), new TypeConverter
            {
                ConvertToString = (obj, type) => ToStringUtility.ObjectToString((Vector4)obj),
                ConvertToObject = (str, type) => ToStringUtility.StringToVector4(str),
            });
            AddConverter(typeof(Quaternion), new TypeConverter
            {
                ConvertToString = (obj, type) => ToStringUtility.ObjectToString((Quaternion)obj),
                ConvertToObject = (str, type) => ToStringUtility.StringToQuaternion(str),
            });
        }

        /// <summary>
        /// Convert object of a given type to a string using available converters.
        /// </summary>
        public static string ConvertToString(object value, Type valueType)
        {
            var conv = GetConverter(valueType);
            if (conv == null)
                throw new InvalidOperationException($"Cannot convert from type {valueType}");

            return conv.ConvertToString(value, valueType);
        }

        /// <summary>
        /// Convert string to an object of a given type using available converters.
        /// </summary>
        public static T ConvertToValue<T>(string value)
        {
            return (T)ConvertToValue(value, typeof(T));
        }

        /// <summary>
        /// Convert string to an object of a given type using available converters.
        /// </summary>
        public static object ConvertToValue(string value, Type valueType)
        {
            var conv = GetConverter(valueType);
            if (conv == null)
                throw new InvalidOperationException($"Cannot convert to type {valueType.Name}");

            return conv.ConvertToObject(value, valueType);
        }

        /// <summary>
        /// Get a converter for a given type if there is any.
        /// </summary>
        public static TypeConverter GetConverter(Type valueType)
        {
            if (valueType == null) throw new ArgumentNullException(nameof(valueType));

            if (valueType.IsEnum)
                return TypeConverters[typeof(Enum)];

            TypeConverters.TryGetValue(valueType, out var result);
            return result;
        }

        /// <summary>
        /// Add a new type converter for a given type. 
        /// If a different converter is already added, this call is ignored and false is returned.
        /// </summary>
        public static bool AddConverter(Type type, TypeConverter converter)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (converter == null) throw new ArgumentNullException(nameof(converter));
            if (CanConvert(type))
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "Tried to add a TomlConverter when one already exists for type " + type.FullName);
                return false;
            }

            TypeConverters.Add(type, converter);
            return true;
        }

        /// <summary>
        /// Check if a given type can be converted to and from string.
        /// </summary>
        public static bool CanConvert(Type type)
        {
            return GetConverter(type) != null;
        }

        /// <summary>		
        /// Give a list of types with registered converters.
        /// </summary>
        public static IEnumerable<Type> GetSupportedTypes()
        {
            return TypeConverters.Keys;
        }
    }
}