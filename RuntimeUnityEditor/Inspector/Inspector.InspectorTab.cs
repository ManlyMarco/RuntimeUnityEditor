using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using Component = UnityEngine.Component;

namespace RuntimeUnityEditor.Core.Inspector
{
    public sealed partial class Inspector
    {
        private class InspectorTab
        {
            private readonly List<ICacheEntry> _fieldCache = new List<ICacheEntry>();
            public InspectorStackEntryBase CurrentStackItem => InspectorStack.Count > 0 ? InspectorStack.Peek() : null;
            public IList<ICacheEntry> FieldCache => _fieldCache;
            public Stack<InspectorStackEntryBase> InspectorStack { get; } = new Stack<InspectorStackEntryBase>();
            public Vector2 InspectorStackScrollPos { get; set; }

            public void Clear()
            {
                InspectorStack.Clear();
                CacheAllMembers(null);
            }

            public void Pop()
            {
                InspectorStack.Pop();
                LoadStackEntry(InspectorStack.Peek());
            }

            public void Push(InspectorStackEntryBase stackEntry)
            {
                InspectorStack.Push(stackEntry);
                LoadStackEntry(stackEntry);
            }

            private static IEnumerable<ICacheEntry> MethodsToCacheEntries(object instance, Type instanceType,
                IEnumerable<MethodInfo> methodsToCheck)
            {
                var cacheItems = methodsToCheck
                    .Where(x => !x.IsConstructor && !x.IsSpecialName && x.GetParameters().Length == 0)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Where(x => x.Name != "MemberwiseClone" && x.Name != "obj_address") // Instant game crash
                    .Select(m =>
                    {
                        if (m.ContainsGenericParameters)
                            try
                            {
                                return m.MakeGenericMethod(instanceType);
                            }
                            catch (Exception)
                            {
                                return null;
                            }

                        return m;
                    }).Where(x => x != null)
                    .Select(m => new MethodCacheEntry(instance, m)).Cast<ICacheEntry>();
                return cacheItems;
            }

            private void CacheAllMembers(InstanceStackEntry entry)
            {
                _fieldCache.Clear();

                var objectToOpen = entry?.Instance;
                if (objectToOpen == null) return;

                var type = objectToOpen.GetType();

                try
                {
                    if (objectToOpen is Component cmp)
                    {
                        _fieldCache.Add(new CallbackCacheEntry("Open in Scene Object Browser",
                            "Navigate to GameObject this Component is attached to",
                            () => Inspector._treeListShowCallback(cmp.transform)));
                    }
                    else if (objectToOpen is GameObject castedObj)
                    {
                        _fieldCache.Add(new CallbackCacheEntry("Open in Scene Object Browser",
                            "Navigate to this object in the Scene Object Browser",
                            () => Inspector._treeListShowCallback(castedObj.transform)));
                        _fieldCache.Add(new ReadonlyCacheEntry("Child objects",
                            castedObj.transform.Cast<Transform>().ToArray()));
                        _fieldCache.Add(new ReadonlyCacheEntry("Components", castedObj.GetComponents<Component>()));
                    }

                    // If we somehow enter a string, this allows user to see what the string actually says
                    if (type == typeof(string))
                    {
                        _fieldCache.Add(new ReadonlyCacheEntry("this", objectToOpen));
                    }
                    else if (objectToOpen is Transform)
                    {
                        // Prevent the list overloads from listing subcomponents
                    }
                    else if (objectToOpen is IList list)
                    {
                        for (var i = 0; i < list.Count; i++)
                            _fieldCache.Add(new ListCacheEntry(list, i));
                    }
                    else if (objectToOpen is IEnumerable enumerable)
                    {
                        _fieldCache.AddRange(enumerable.Cast<object>()
                            .Select((x, y) => x is ICacheEntry ? x : new ReadonlyListCacheEntry(x, y))
                            .Cast<ICacheEntry>());
                    }

                    // No need if it's not a value type, only used to propagate changes back so it's redundant with classes
                    var parent = entry.Parent?.Type().IsValueType == true ? entry.Parent : null;

                    // Instance members
                    _fieldCache.AddRange(type.GetAllFields(false)
                        .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                        .Select(f => new FieldCacheEntry(objectToOpen, f, parent)).Cast<ICacheEntry>());
                    _fieldCache.AddRange(type.GetAllProperties(false)
                        .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                        .Select(p => new PropertyCacheEntry(objectToOpen, p, parent)).Cast<ICacheEntry>());
                    _fieldCache.AddRange(MethodsToCacheEntries(objectToOpen, type, type.GetAllMethods(false)));

                    CacheStaticMembersHelper(type);
                }
                catch (Exception ex)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "[Inspector] CacheFields crash: " + ex);
                }
            }

            private void CacheStaticMembers(StaticStackEntry entry)
            {
                _fieldCache.Clear();

                if (entry?.StaticType == null) return;

                try
                {
                    CacheStaticMembersHelper(entry.StaticType);
                }
                catch (Exception ex)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "[Inspector] CacheFields crash: " + ex);
                }
            }

            private void CacheStaticMembersHelper(Type type)
            {
                _fieldCache.AddRange(type.GetAllFields(true)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(f => new FieldCacheEntry(null, f)).Cast<ICacheEntry>());
                _fieldCache.AddRange(type.GetAllProperties(true)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(p => new PropertyCacheEntry(null, p)).Cast<ICacheEntry>());
                _fieldCache.AddRange(MethodsToCacheEntries(null, type, type.GetAllMethods(true)));
            }

            private void LoadStackEntry(InspectorStackEntryBase stackEntry)
            {
                switch (stackEntry)
                {
                    case InstanceStackEntry instanceStackEntry:
                        CacheAllMembers(instanceStackEntry);
                        break;
                    case StaticStackEntry staticStackEntry:
                        CacheStaticMembers(staticStackEntry);
                        break;
                    default:
                        throw new InvalidEnumArgumentException(
                            "Invalid stack entry type: " + stackEntry.GetType().FullName);
                }
            }

            public void PopUntil(InspectorStackEntryBase item)
            {
                if (CurrentStackItem == item) return;
                while (CurrentStackItem != null && CurrentStackItem != item) InspectorStack.Pop();
                LoadStackEntry(CurrentStackItem);
            }
        }
    }
}