using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace RuntimeUnityEditor.Core.Inspector
{
    /// <summary>
    /// Provides a way to convert objects to strings for display in the inspector and other places across RUE.
    /// </summary>
    public static class ToStringConverter
    {
        private static readonly Dictionary<Type, Func<object, string>> _toStringConverters = new Dictionary<Type, Func<object, string>>();

        /// <summary>
        /// Adds a converter for a specific type to convert it to a string.
        /// </summary>
        /// <typeparam name="TObj">Type of the object</typeparam>
        /// <param name="objectToString">A custom ToString method for the type</param>
        /// <exception cref="ArgumentNullException"><paramref name="objectToString"/> cannot be null</exception>
        public static void AddConverter<TObj>(Func<TObj, string> objectToString)
        {
            if (objectToString == null) throw new ArgumentNullException(nameof(objectToString));

            var type = typeof(TObj);
            _toStringConverters[type] = o => objectToString.Invoke((TObj)o);
            _canCovertCache.Remove(typeof(TObj));
        }

        /// <summary>
        /// Converts an object to a string based on registered converters.
        /// Never throws, at worst it returns the base ToString result or the type name.
        /// </summary>
        public static string ObjectToString(object value)
        {
            var isNull = value.IsNullOrDestroyedStr();
            if (isNull != null) return isNull;

            switch (value)
            {
                case string str:
                    return str;
                case Transform t:
                    return t.name;
                case GameObject o:
                    return o.name;
                case Exception ex:
                    return "EXCEPTION: " + ex.Message;
                case Delegate d:
                    return DelegateToString(d);
            }

            var valueType = value.GetType();

            if (_toStringConverters.TryGetValue(valueType, out var func))
                return func(value);

            if (value is ICollection collection)
                return $"Count = {collection.Count}";

            if (value is IEnumerable || value.GetType().GetMethod("GetEnumerator", AccessTools.all, null, Type.EmptyTypes, null) != null)
            {
                var property = valueType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanRead)
                {
                    if (property.GetValue(value, null) is int count)
                        return $"Count = {count}";
                }

                return "IS ENUMERABLE";
            }

            var inheritedConverter = _toStringConverters.FirstOrDefault(x => x.Key.IsAssignableFrom(valueType));
            if (inheritedConverter.Key != null)
                return inheritedConverter.Value(value);

            try
            {
                if (valueType.IsGenericType)
                {
                    var baseType = valueType.GetGenericTypeDefinition();
#if IL2CPP
                    if (baseType == typeof(KeyValuePair<,>) || baseType == typeof(Il2CppSystem.Collections.Generic.KeyValuePair<,>))
#else
                    if (baseType == typeof(KeyValuePair<,>))
#endif
                    {
                        //var argTypes = baseType.GetGenericArguments();
                        var kvpKey = valueType.GetProperty("Key")?.GetValue(value, null);
                        var kvpValue = valueType.GetProperty("Value")?.GetValue(value, null);
                        return $"[{ObjectToString(kvpKey)} | {ObjectToString(kvpValue)}]";
                    }
                }

                return value.ToString();
            }
            catch
            {
                return valueType.Name;
            }
        }

        private static string DelegateToString(Delegate unityAction)
        {
            if (unityAction == null) return "[NULL]";
            string str;
            var isNull = unityAction.Target.IsNullOrDestroyedStr();
            if (isNull != null) str = "[" + isNull + "]";
            else str = unityAction.Target.GetType().GetSourceCodeRepresentation();
            var actionString = $"{str}.{unityAction.Method.Name}";
            return actionString;
        }

        internal static string EventEntryToString(UnityEventBase eventObj, int i)
        {
            if (eventObj == null) return "[NULL]";
            if (i < 0 || i >= eventObj.GetPersistentEventCount()) return "[Event index out of range]";
            // It's fine to use ? here because GetType works fine on disposed objects and we want to know the type name
            return $"{eventObj.GetPersistentTarget(i)?.GetType().GetSourceCodeRepresentation() ?? "[NULL]"}.{eventObj.GetPersistentMethodName(i)}";
        }

        internal static readonly Dictionary<Type, bool> _canCovertCache = new Dictionary<Type, bool>();

        /// <summary>
        /// Check if the value can be converted to a string and back to the original type.
        /// </summary>
        public static bool CanEditValue(ICacheEntry field, object value)
        {
            var valueType = field.Type();

            if (valueType == null)
                return false;

            if (valueType == typeof(string))
                return true;

            if (_canCovertCache.TryGetValue(valueType, out var stored))
                return stored;

            if (TomlTypeConverter.GetConverter(valueType) != null)
            {
                _canCovertCache[valueType] = true;
                return true;
            }

            try
            {
                var converted = ToStringConverter.ObjectToString(value);
                _ = Convert.ChangeType(converted, valueType);
                _canCovertCache[valueType] = true;
                return true;
            }
            catch
            {
                _canCovertCache[valueType] = false;
                return false;
            }
        }

        /// <summary>
        /// Sets the value of the field to the converted value from the string, if it is different from the current value.
        /// </summary>
        public static void SetEditValue(ICacheEntry field, object currentValue, string newValue)
        {
            var valueType = field.Type();
            object converted;
            if (valueType == typeof(string))
            {
                converted = newValue;
            }
            else
            {
                var typeConverter = TomlTypeConverter.GetConverter(valueType);
                if (typeConverter != null)
                    converted = typeConverter.ConvertToObject(newValue, valueType);
                else
                    converted = Convert.ChangeType(newValue, valueType);
            }

            if (!Equals(converted, currentValue))
                field.SetValue(converted);
        }

        /// <summary>
        /// Converts the value to a string for editing in the inspector.
        /// </summary>
        public static string GetEditValue(ICacheEntry field, object value)
        {
            if (value is string str)
                return str;

            var valueType = field.Type();

            if (value == null && valueType == typeof(string))
                return "";

            var isNull = value.IsNullOrDestroyedStr();
            if (isNull != null) return isNull;

            var typeConverter = TomlTypeConverter.GetConverter(valueType);
            if (typeConverter != null) return typeConverter.ConvertToString(value, valueType);

            return ObjectToString(value);
        }
    }
}