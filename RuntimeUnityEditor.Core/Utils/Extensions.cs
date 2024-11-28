using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;
using Object = UnityEngine.Object;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace RuntimeUnityEditor.Core.Utils
{
    /// <summary>
    /// Useful stuff.
    /// </summary>
    public static class Extensions
    {
        public static bool REContains(this string s, string searchText, StringComparison sc)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(searchText))
                return false;
            return s.IndexOf(searchText, sc) >= 0;
        }

        public static T Next<T>(this T src) where T : struct
        {
            if (!typeof(T).IsEnum) throw new ArgumentException($"Argumnent {typeof(T).FullName} is not an Enum");

            var arr = (T[])Enum.GetValues(src.GetType());
            var j = Array.IndexOf(arr, src) + 1;
            return (arr.Length == j) ? arr[0] : arr[j];
        }

        /// <summary>
        /// Gets value of a field.
        /// WARNING: Only use on game types, not plugin or BepInEx types.
        /// The reason is that fields are properties in IL2CPP interop assemblies, and this automatically adjusts for that.
        /// </summary>
        public static object GetPrivateExplicit<T>(this T self, string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            var type = typeof(T);
#if IL2CPP // In IL2CPP, properties are used for fields in the interop assemblies
            var memberInfo = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
#else
            var memberInfo = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
#endif
            if (memberInfo == null) throw new MemberNotFoundException($"Could not find '{name}' on {type.FullName}");
            return memberInfo.GetValue(self);
        }

        /// <summary>
        /// Gets value of a field.
        /// WARNING: Only use on game types, not plugin or BepInEx types.
        /// The reason is that fields are properties in IL2CPP interop assemblies, and this automatically adjusts for that.
        /// </summary>
        public static object GetPrivate(this object self, string name)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            if (name == null) throw new ArgumentNullException(nameof(name));
            var type = self.GetType();
#if IL2CPP // In IL2CPP, properties are used for fields in the interop assemblies
            var memberInfo = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
#else
            var memberInfo = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
#endif
            if (memberInfo == null) throw new MemberNotFoundException($"Could not find '{name}' on {type.FullName}");
            return memberInfo.GetValue(self);
        }

        public static object TryGetPropertyValue(this object self, string name, object[] index = null)
        {
            return AccessTools.Property(self.GetType(), name)?.GetValue(self, index);
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
            foreach (var t in self.AbstractGetChildren())
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

        /// <summary>
        /// SetActive may change the scene of the GameObject if it is currently NULL, which might be unexpected to the user.
        /// If this happens, show a warning.
        /// </summary>
        public static void SetActiveWithSceneChangeWarning(this GameObject o, bool value)
        {
            // BUG: NOT IN 4.x
            var sceneBak = o.scene;
            o.SetActive(value);
            if (sceneBak != o.scene)
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning | LogLevel.Message, $"Scene of GameObject [{o.name}] changed from [{sceneBak.name ?? "NULL"}] to [{o.scene.name ?? "NULL"}]");
        }

        /// <summary>
        /// Turn anything with a GetEnumerator method to an IEnumerable with a casted type.
        /// Will throw on failure at start, or throw during enumeration if casting fails.
        /// </summary>
        public static IEnumerable<T> CastToEnumerable<T>(this object obj)
        {
            if (obj is IEnumerable<T> ie)
                return ie;

            return CastToEnumerable(obj).Cast<T>();
        }

        /// <summary>
        /// Turn anything with a GetEnumerator method to an IEnumerable.
        /// Will throw on failure at start.
        /// </summary>
        public static IEnumerable CastToEnumerable(this object obj)
        {
            if (obj is IEnumerable ie2)
                return ie2;

            return DynamicAsEnumerable(obj);

            IEnumerable DynamicAsEnumerable(object targetObj)
            {
                // Enumerate through reflection since mono version doesn't have dynamic keyword
                // In IL2CPP using foreach with dynamic targetObj throws cast exceptions because of IL2CPP types
                var mGetEnumerator = targetObj.GetType().GetMethod("GetEnumerator");
                if (mGetEnumerator == null) throw new ArgumentNullException(nameof(mGetEnumerator));
                var enumerator = mGetEnumerator.Invoke(targetObj, null);
                if (enumerator == null) throw new ArgumentNullException(nameof(enumerator));
                var enumeratorType = enumerator.GetType();
                var mMoveNext = enumeratorType.GetMethod("MoveNext");
                if (mMoveNext == null) throw new ArgumentNullException(nameof(mMoveNext));
                var mCurrent = enumeratorType.GetProperty("Current");
                if (mCurrent == null) throw new ArgumentNullException(nameof(mCurrent));
                while ((bool)mMoveNext.Invoke(enumerator, null))
                    yield return mCurrent.GetValue(enumerator, null);
            }
        }
    }
}