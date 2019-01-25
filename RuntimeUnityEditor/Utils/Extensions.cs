using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace RuntimeUnityEditor.Utils
{
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
        public static void SetPrivateExplicit<T>(this T self, string name, object value)
        {
            typeof(T).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).SetValue(self, value);
        }
        public static void SetPrivate(this object self, string name, object value)
        {
            self.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).SetValue(self, value);
        }
        public static object CallPrivateExplicit<T>(this T self, string name, params object[] p)
        {
            return typeof(T).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).Invoke(self, p);
        }
        public static object CallPrivate(this object self, string name, params object[] p)
        {
            return self.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).Invoke(self, p);
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
    }
}