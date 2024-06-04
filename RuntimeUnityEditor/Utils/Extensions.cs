using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RuntimeUnityEditor.Core.Inspector.Entries;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuntimeUnityEditor.Core.Utils
{
    /// <summary>
    /// Useful stuff.
    /// </summary>
    public static class Extensions
    {
        public static bool Contains(this string s, string searchText, StringComparison sc)
        {
            return s.IndexOf(searchText, sc) >= 0;
        }

        public static T Next<T>(this T src) where T : struct
        {
            if (!typeof(T).IsEnum) throw new ArgumentException($"Argumnent {typeof(T).FullName} is not an Enum");

            var arr = (T[])Enum.GetValues(src.GetType());
            var j = Array.IndexOf(arr, src) + 1;
            return (arr.Length == j) ? arr[0] : arr[j];
        }

        public static object GetPrivateExplicit<T>(this T self, string name)
        {
            return typeof(T).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).GetValue(self);
        }
        public static object GetPrivate(this object self, string name)
        {
            return self.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).GetValue(self);
        }
        [Obsolete("Obsoleted by AccessTools")]
        public static void SetPrivateExplicit<T>(this T self, string name, object value)
        {
            typeof(T).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).SetValue(self, value);
        }
        [Obsolete("Obsoleted by AccessTools")]
        public static void SetPrivate(this object self, string name, object value)
        {
            self.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).SetValue(self, value);
        }
        [Obsolete("Obsoleted by AccessTools")]
        public static object CallPrivateExplicit<T>(this T self, string name, params object[] p)
        {
            return typeof(T).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).Invoke(self, p);
        }
        [Obsolete("Obsoleted by AccessTools")]
        public static object CallPrivate(this object self, string name, params object[] p)
        {
            return self.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).Invoke(self, p);
        }

        public static object TryGetFieldValue(this object self, string name)
        {
            return AccessTools.Field(self.GetType(), name)?.GetValue(self);
        }

        public static object TryGetPropertyValue(this object self, string name, object[] index = null)
        {
            return AccessTools.Property(self.GetType(), name)?.GetValue(self, index);
        }

        public static void ExecuteDelayed(this MonoBehaviour self, Action action, int waitCount = 1)
        {
            self.StartCoroutine(ExecuteDelayed_Routine(action, waitCount));
        }

        private static IEnumerator ExecuteDelayed_Routine(Action action, int waitCount)
        {
            for (var i = 0; i < waitCount; ++i)
                yield return null;
            action();
        }

        public static string GetPathFrom(this Transform self, Transform root)
        {
            var self2 = self;
            var path = self2.name;
            self2 = self2.parent;
            while (self2 != root)
            {
                path = self2.name + "/" + path;
                self2 = self2.parent;
            }
            return path;
        }

        public static Transform FindDescendant(this Transform self, string name)
        {
            if (self.name.Equals(name))
                return self;
            foreach (Transform t in self)
            {
                var res = t.FindDescendant(name);
                if (res != null)
                    return res;
            }
            return null;
        }

        public static MemberInfo GetMemberInfo(this ICacheEntry centry, bool throwOnError)
        {
            switch (centry)
            {
                case MethodCacheEntry m: return m.MethodInfo;
                case PropertyCacheEntry p: return p.PropertyInfo;
                case FieldCacheEntry f: return f.FieldInfo;
                case EventCacheEntry e: return e.EventInfo;

                case null: return throwOnError ? throw new ArgumentNullException(nameof(centry)) : (MemberInfo)null;
                default: return throwOnError ? throw new Exception("Cannot open items of type " + centry.GetType().FullName) : (MemberInfo)null;
            }
        }

        public static IEnumerable<Type> GetTypesSafe(this Assembly ass)
        {
            try { return ass.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null); }
            catch { return Enumerable.Empty<Type>(); }
        }

        public static string GetFullTransfromPath(this Transform target)
        {
            var name = target.name;
            var parent = target.parent;
            while (parent != null)
            {
                name = $"{parent.name}/{name}";
                parent = parent.parent;
            }
            return name;
        }

        internal static string IsNullOrDestroyedStr(this object value)
        {
            if (ReferenceEquals(value, null)) return "NULL";

            if (value is Object uobj)
            {
                // This is necessary because the is operator ignores the == override that makes Objects look like null
                if (uobj.Equals(null)) return "NULL (Destroyed)";
            }

            return null;
        }

        internal static bool IsNullOrDestroyed(this object value)
        {
            if (ReferenceEquals(value, null)) return true;

            if (value is Object uobj)
            {
                // This is necessary because the is operator ignores the == override that makes Objects look like null
                if (uobj.Equals(null)) return true;
            }

            return false;
        }

        public static void FillTexture(this Texture2D tex, Color color)
        {
            for (var x = 0; x < tex.width; x++)
                for (var y = 0; y < tex.height; y++)
                    tex.SetPixel(x, y, color);

            tex.Apply(false);
        }

        /// <summary>
        /// Get all public and private fields, including from base classes
        /// </summary>
        public static IEnumerable<FieldInfo> GetAllFields(this Type t, bool getStatic)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | (getStatic ? BindingFlags.Static : BindingFlags.Instance);
            return t.BaseType == null ? t.GetFields(flags) : t.GetFields(flags).Concat(GetAllFields(t.BaseType, getStatic));
        }

        /// <summary>
        /// Get all public and private properties, including from base classes
        /// </summary>
        public static IEnumerable<PropertyInfo> GetAllProperties(this Type t, bool getStatic)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | (getStatic ? BindingFlags.Static : BindingFlags.Instance);
            return t.BaseType == null ? t.GetProperties(flags) : t.GetProperties(flags).Concat(GetAllProperties(t.BaseType, getStatic));
        }

        /// <summary>
        /// Get all public and private methods, including from base classes
        /// </summary>
        public static IEnumerable<MethodInfo> GetAllMethods(this Type t, bool getStatic)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | (getStatic ? BindingFlags.Static : BindingFlags.Instance);
            return t.BaseType == null ? t.GetMethods(flags) : t.GetMethods(flags).Concat(GetAllMethods(t.BaseType, getStatic));
        }

        /// <summary>
        /// Get all public and private methods, including from base classes
        /// </summary>
        public static IEnumerable<EventInfo> GetAllEvents(this Type t, bool getStatic)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | (getStatic ? BindingFlags.Static : BindingFlags.Instance);
            return t.BaseType == null ? t.GetEvents(flags) : t.GetEvents(flags).Concat(GetAllEvents(t.BaseType, getStatic));
        }

        public static void SetLossyScale(this Transform targetTransform, Vector3 lossyScale)
        {
            targetTransform.localScale = new Vector3(targetTransform.localScale.x * (lossyScale.x / targetTransform.lossyScale.x),
                                                     targetTransform.localScale.y * (lossyScale.y / targetTransform.lossyScale.y),
                                                     targetTransform.localScale.z * (lossyScale.z / targetTransform.lossyScale.z));
        }
    }
}