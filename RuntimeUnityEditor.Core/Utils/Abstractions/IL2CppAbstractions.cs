using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if IL2CPP
using Il2CppInterop.Runtime.Injection;
#endif

namespace RuntimeUnityEditor.Core
{
    internal static class IL2CppAbstractions
    {
        public static void Set(this RectOffset obj, int left, int right, int top, int bottom)
        {
            obj.left = left;
            obj.right = right;
            obj.top = top;
            obj.bottom = bottom;
        }

        public static Transform[] AbstractGetChildren(this Transform transform, bool castedIl2Cpp = true)
        {
            var children = new Transform[transform.childCount];
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
#if IL2CPP
                children[i] = castedIl2Cpp ? child.TryAutoCast() as Transform ?? child : child;
#else
                children[i] = child;
#endif
            }
            return children;
        }

        public static Component[] AbstractGetAllComponents(this GameObject gameObject, bool castedIl2Cpp = true)
        {
            var result = (Component[])gameObject.GetComponents<Component>();
#if IL2CPP
            if (castedIl2Cpp)
                result = result.Select(x => IL2CppExtensions.TryAutoCast(x) ?? x).OfType<Component>().ToArray();
#endif
            return result;
        }

        public static Component[] AbstractGetAllComponents(this Component component, bool castedIl2Cpp = true)
        {
            var result = (Component[])component.GetComponents<Component>();
#if IL2CPP
            if (castedIl2Cpp)
                result = result.Select(x => x.TryAutoCast() ?? x).OfType<Component>().ToArray();
#endif
            return result;
        }

        public static Component[] AbstractGetAllComponentsInChildren(this GameObject gameObject, bool includeInactive, bool castedIl2Cpp = true)
        {
            var result = (Component[])gameObject.GetComponentsInChildren<Component>(includeInactive);
#if IL2CPP
            if (castedIl2Cpp)
                result = result.Select(x => x.TryAutoCast() ?? x).OfType<Component>().ToArray();
#endif
            return result;
        }

        public static Component[] AbstractGetAllComponentsInChildren(this Component component, bool includeInactive, bool castedIl2Cpp = true)
        {
            var result = (Component[])component.GetComponentsInChildren<Component>(includeInactive);
#if IL2CPP
            if (castedIl2Cpp)
                result = result.Select(x => x.TryAutoCast() ?? x).OfType<Component>().ToArray();
#endif
            return result;
        }

        public static Coroutine AbstractStartCoroutine(this MonoBehaviour monoBehaviour, IEnumerator routine)
        {
#if IL2CPP
            // TODO Remove all direct dependencies on BepInEx
            return monoBehaviour.StartCoroutine(BepInEx.Unity.IL2CPP.Utils.Collections.CollectionExtensions.WrapToIl2Cpp(routine));
#else
            return monoBehaviour.StartCoroutine(routine);
#endif
        }

        public static T AbstractAddComponent<T>(this GameObject go) where T : Component
        {
#if IL2CPP
            var t = typeof(T);
            if (!ClassInjector.IsTypeRegisteredInIl2Cpp(t))
                ClassInjector.RegisterTypeInIl2Cpp(t);
            return go.AddComponent(Il2CppInterop.Runtime.Il2CppType.From(t)).Cast<T>();
#else
            return go.AddComponent<T>();
#endif
        }
    }
}
