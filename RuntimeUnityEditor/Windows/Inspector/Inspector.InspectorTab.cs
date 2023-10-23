using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using RuntimeUnityEditor.Core.Utils.ObjectDumper;
using UnityEngine;
using Component = UnityEngine.Component;

namespace RuntimeUnityEditor.Core.Inspector
{
    public sealed partial class Inspector
    {
        private class InspectorTab
        {
            private readonly List<ICacheEntry> _fieldCache = new List<ICacheEntry>();
            private InspectorStackEntryBase _currentStackItem;
            public InspectorStackEntryBase CurrentStackItem
            {
                get => _currentStackItem ?? (InspectorStack.Count > 0 ? _currentStackItem = InspectorStack.Peek() : null);
                set
                {
                    _currentStackItem = value;
                    LoadStackEntry(_currentStackItem);
                }
            }

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
                var popdItem = InspectorStack.Pop();

                if (_currentStackItem == null || _currentStackItem == popdItem)
                    _currentStackItem = InspectorStack.Peek();

                LoadStackEntry(_currentStackItem);
            }

            public void Push(InspectorStackEntryBase stackEntry)
            {
                if (CurrentStackItem != null)
                {
                    // Pop everything above the selected item
                    while (InspectorStack.Count > 0 && InspectorStack.Peek() != CurrentStackItem)
                    {
                        InspectorStack.Pop();
                    }
                }

                InspectorStack.Push(stackEntry);
                _currentStackItem = stackEntry;
                LoadStackEntry(stackEntry);
            }

            private static IEnumerable<ICacheEntry> MethodsToCacheEntries(object instance, Type ownerType, IEnumerable<MethodInfo> methodsToCheck)
            {
                var cacheItems = methodsToCheck
                    .Where(x => !x.IsConstructor && !x.IsSpecialName)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Where(x => x.Name != "MemberwiseClone" && x.Name != "obj_address") // Instant game crash
                    .Select(m => new MethodCacheEntry(instance, m, ownerType)).Cast<ICacheEntry>();
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
                    CallbackCacheEntry GetExportTexEntry(Texture texture)
                    {
                        return new CallbackCacheEntry("Export Texture to file",
                            "Encode the texture to a PNG and save it to a new file",
                            texture.SaveTextureToFileWithDialog);
                    }

                    if (objectToOpen is Component cmp)
                    {
                        if (ObjectTreeViewer.Initialized)
                        {
                            _fieldCache.Add(new CallbackCacheEntry("Open in Scene Object Browser",
                                                                   "Navigate to GameObject this Component is attached to",
                                                                   () => ObjectTreeViewer.Instance.SelectAndShowObject(cmp.transform)));
                        }

                        if (objectToOpen is UnityEngine.UI.Image img)
                            _fieldCache.Add(GetExportTexEntry(img.mainTexture));
                        else if (objectToOpen is Renderer rend && MeshExport.CanExport(rend))
                        {
                            _fieldCache.Add(new CallbackCacheEntry("Export mesh to .obj", "Save base mesh used by this renderer to file", () => MeshExport.ExportObj(rend, false, false)));
                            _fieldCache.Add(new CallbackCacheEntry("Export mesh to .obj (Baked)", "Bakes current pose into the exported mesh", () => MeshExport.ExportObj(rend, true, false)));
                            _fieldCache.Add(new CallbackCacheEntry("Export mesh to .obj (World)", "Bakes pose while keeping world position", () => MeshExport.ExportObj(rend, true, true)));
                        }
                    }
                    else if (objectToOpen is GameObject castedObj)
                    {
                        if (ObjectTreeViewer.Initialized)
                        {
                            _fieldCache.Add(new CallbackCacheEntry("Open in Scene Object Browser",
                                                                   "Navigate to this object in the Scene Object Browser",
                                                                   () => ObjectTreeViewer.Instance.SelectAndShowObject(castedObj.transform)));
                        }

                        _fieldCache.Add(new ReadonlyCacheEntry("Child objects",
                                                               castedObj.transform.Cast<Transform>().ToArray()));
                        _fieldCache.Add(new ReadonlyCacheEntry("Components", castedObj.GetComponents<Component>()));
                    }
                    else if (objectToOpen is Texture tex)
                    {
                        _fieldCache.Add(GetExportTexEntry(tex));
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
                        .Select(f => new FieldCacheEntry(objectToOpen, f, type, parent)).Cast<ICacheEntry>());

                    var isRenderer = objectToOpen is Renderer;

                    _fieldCache.AddRange(type.GetAllProperties(false)
                                             .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                                             .Select(p =>
                                             {
                                                 if (isRenderer)
                                                 {
                                                     // Prevent unintentionally creating local material instances when viewing renderers in inspector
                                                     if (p.Name == "material")
                                                         return new CallbackCacheEntry<Material>("material", "Local instance of sharedMaterial (create on entry)", () => ((Renderer)objectToOpen).material);
                                                     if (p.Name == "materials")
                                                         return new CallbackCacheEntry<Material[]>("materials", "Local instance of sharedMaterials (create on entry)", () => ((Renderer)objectToOpen).materials);
                                                 }
                                                 return (ICacheEntry)new PropertyCacheEntry(objectToOpen, p, type, parent);
                                             }));

                    _fieldCache.AddRange(type.GetAllEvents(false)
                        .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                        .Select(p => new EventCacheEntry(objectToOpen, p, type)).Cast<ICacheEntry>());

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
                    .Select(f => new FieldCacheEntry(null, f, type)).Cast<ICacheEntry>());

                _fieldCache.AddRange(type.GetAllProperties(true)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(p => new PropertyCacheEntry(null, p, type)).Cast<ICacheEntry>());

                _fieldCache.AddRange(type.GetAllEvents(true)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(p => new EventCacheEntry(null, p, type)).Cast<ICacheEntry>());

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
                    case null:
                        _fieldCache.Clear();
                        return;
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