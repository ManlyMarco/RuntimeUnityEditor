using HarmonyLib;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using RuntimeUnityEditor.Core.Utils.ObjectDumper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Component = UnityEngine.Component;

namespace RuntimeUnityEditor.Core.Inspector
{
    /// <summary>
    /// Helper class for caching members (fields, properties, methods, events) for inspector objects.
    /// </summary>
    internal static class MemberCollector
    {
        public static ICollection<ICacheEntry> CollectAllMembers(InstanceStackEntry entry)
        {
                var fieldCache = new List<ICacheEntry>();
            var objectToOpen = entry?.Instance;
            if (objectToOpen == null) return fieldCache;

            var type = objectToOpen.GetType();

            //TODO
            // - Handle il2cpp-side fields by making them act like normal fields
            // - give il2cpp-side fields and methods a special color or something in inspector, show pointer address in tooltip
            var staticFields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic).Where(x => x.IsInitOnly).ToList();
            var fieldPointerLookup = staticFields.Where(x => x.Name.StartsWith("NativeFieldInfoPtr_"))
                                                 .ToDictionary(x => x.Name.Replace("NativeFieldInfoPtr_", ""), x => x.GetValue(null));
            var methodPointerLookup = staticFields.Where(x => x.Name.StartsWith("NativeMethodInfoPtr_"))
                                                  .ToDictionary(x => x.Name.Replace("NativeMethodInfoPtr_", ""), x => x.GetValue(null));

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
                        fieldCache.Add(new CallbackCacheEntry("Open in Scene Object Browser",
                                                               "Navigate to GameObject this Component is attached to",
                                                               () => ObjectTreeViewer.Instance.SelectAndShowObject(cmp.transform)));
                    }

                    if (objectToOpen is UnityEngine.UI.Image img)
                        fieldCache.Add(GetExportTexEntry(img.mainTexture));
                    else if (objectToOpen is Renderer rend && MeshExport.CanExport(rend))
                    {
                        fieldCache.Add(new CallbackCacheEntry("Export mesh to .obj", "Save base mesh used by this renderer to file", () => MeshExport.ExportObj(rend, false, false)));
                        fieldCache.Add(new CallbackCacheEntry("Export mesh to .obj (Baked)", "Bakes current pose into the exported mesh", () => MeshExport.ExportObj(rend, true, false)));
                        fieldCache.Add(new CallbackCacheEntry("Export mesh to .obj (World)", "Bakes pose while keeping world position", () => MeshExport.ExportObj(rend, true, true)));
                    }
                }
                else if (objectToOpen is GameObject castedObj)
                {
                    if (ObjectTreeViewer.Initialized)
                    {
                        fieldCache.Add(new CallbackCacheEntry("Open in Scene Object Browser",
                                                               "Navigate to this object in the Scene Object Browser",
                                                               () => ObjectTreeViewer.Instance.SelectAndShowObject(castedObj.transform)));
                    }
#if !IL2CPP
                        fieldCache.Add(new ReadonlyCacheEntry("Child objects", castedObj.transform.Cast<Transform>().ToArray()));
#endif
                    fieldCache.Add(new ReadonlyCacheEntry("Components", castedObj.AbstractGetAllComponents()));
                }
                else if (objectToOpen is Texture tex)
                {
                    fieldCache.Add(GetExportTexEntry(tex));
                }

                // If we somehow enter a string, this allows user to see what the string actually says
                if (type == typeof(string))
                {
                    fieldCache.Add(new ReadonlyCacheEntry("this", objectToOpen));
                }
                else if (objectToOpen is Transform)
                {
                    // Prevent the list overloads from listing subcomponents
                }
                else if (objectToOpen is IList list)
                {
                    for (var i = 0; i < list.Count; i++)
                        fieldCache.Add(new ListCacheEntry(list, i));
                }
                else if (objectToOpen is IEnumerable enumerable)
                {
                    fieldCache.AddRange(enumerable.Cast<object>()
                        .Select((x, y) => x is ICacheEntry ? x : new ReadonlyListCacheEntry(x, y))
                        .Cast<ICacheEntry>());
                }
                else
                {
                    // Needed for IL2CPP collections since they don't implement IEnumerable
                    // Can cause side effects if the object is not a real collection
                    var getEnumeratorM = type.GetMethod("GetEnumerator", AccessTools.all, null, Type.EmptyTypes, null);
                    if (getEnumeratorM != null)
                    {
                        try
                        {
                            var enumerator = getEnumeratorM.Invoke(objectToOpen, null);
                            if (enumerator != null)
                            {
                                var enumeratorType = enumerator.GetType();
                                var moveNextM = enumeratorType.GetMethod("MoveNext", AccessTools.all, null, Type.EmptyTypes, null);
                                var currentP = enumeratorType.GetProperty("Current");
                                if (moveNextM != null && currentP != null)
                                {
                                    var count = 0;
                                    while ((bool)moveNextM.Invoke(enumerator, null))
                                    {
                                        var current = currentP.GetValue(enumerator, null);
                                        fieldCache.Add(new ReadonlyListCacheEntry(current, count));
                                        count++;
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, $"Failed to enumerate object \"{objectToOpen}\" ({type.FullName}) : {e}");
                        }
                    }
                }

                // No need if it's not a value type, only used to propagate changes back so it's redundant with classes
                var parent = entry.Parent?.Type().IsValueType == true ? entry.Parent : null;

                // Instance members
                fieldCache.AddRange(type.GetAllFields(false)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(f => new FieldCacheEntry(objectToOpen, f, type, parent)).Cast<ICacheEntry>());

                var isRenderer = objectToOpen is Renderer;
#if IL2CPP
                var isIl2cppType = objectToOpen is Il2CppSystem.Type;
#endif
                fieldCache.AddRange(type.GetAllProperties(false)
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
#if IL2CPP
                                             else if (isIl2cppType)
                                             {
                                                 // These two are dangerous to evaluate, they hard crash the game with access violation more often than not
                                                 if (p.Name == nameof(Il2CppSystem.Type.DeclaringType))
                                                     return new CallbackCacheEntry<Il2CppSystem.Type>(nameof(Il2CppSystem.Type.DeclaringType), "Skipped evaluation, click to enter (DANGER, MAY HARD CRASH)", () => ((Il2CppSystem.Type)objectToOpen).DeclaringType);
                                                 if (p.Name == nameof(Il2CppSystem.Type.DeclaringMethod))
                                                     return new CallbackCacheEntry<Il2CppSystem.Reflection.MethodBase>(nameof(Il2CppSystem.Type.DeclaringMethod), "Skipped evaluation, click to enter (DANGER, MAY HARD CRASH)", () => ((Il2CppSystem.Type)objectToOpen).DeclaringMethod);
                                             }
#endif

                                             return (ICacheEntry)new PropertyCacheEntry(objectToOpen, p, type, parent);
                                         }));

                fieldCache.AddRange(type.GetAllEvents(false)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(p => new EventCacheEntry(objectToOpen, p, type)).Cast<ICacheEntry>());

                fieldCache.AddRange(MethodsToCacheEntries(objectToOpen, type, type.GetAllMethods(false)));

                fieldCache.AddRange(CacheStaticMembersHelper(type));

                return fieldCache;
            }
            catch (Exception ex)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "[Inspector] CacheFields crash: " + ex);
                fieldCache.Clear();
                fieldCache.Add(new ReadonlyCacheEntry("Exception", ex));
            }

            return fieldCache;
        }

        public static ICollection<ICacheEntry> CollectStaticMembers(StaticStackEntry entry)
        {
            var fieldCache = new List<ICacheEntry>();
            if (entry?.StaticType == null) return fieldCache;
            fieldCache.AddRange(CacheStaticMembersHelper(entry.StaticType));
            return fieldCache;
        }

        private static ICollection<ICacheEntry> CacheStaticMembersHelper(Type type)
        {
            var fieldCache = new List<ICacheEntry>();
            fieldCache.AddRange(type.GetAllFields(true)
                                     .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                                     .Select(f => new FieldCacheEntry(null, f, type)).Cast<ICacheEntry>());

            fieldCache.AddRange(type.GetAllProperties(true)
                                     .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                                     .Select(p => new PropertyCacheEntry(null, p, type)).Cast<ICacheEntry>());

            fieldCache.AddRange(type.GetAllEvents(true)
                                     .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                                     .Select(p => new EventCacheEntry(null, p, type)).Cast<ICacheEntry>());

            fieldCache.AddRange(MethodsToCacheEntries(null, type, type.GetAllMethods(true)));
            return fieldCache;
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
    }
}
