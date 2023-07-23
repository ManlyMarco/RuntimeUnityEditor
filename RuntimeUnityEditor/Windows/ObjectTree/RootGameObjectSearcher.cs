using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core.ObjectTree
{
    /// <summary>
    /// Keeps track of root gameobjects and allows searching objects in the scene
    /// </summary>
    public class RootGameObjectSearcher
    {
        private OrderedSet<GameObject> _cachedRootGameObjects;
        private List<GameObject> _searchResults;

        private Predicate<GameObject> _lastObjectFilter;
        private string _lastSearchString;
        private bool _lastSearchProperties;

        public bool IsSearching() => _searchResults != null;

        public static IEnumerable<GameObject> FindAllRootGameObjects()
        {
            return Resources.FindObjectsOfTypeAll<Transform>()
                .Where(t => t.parent == null)
                .Select(x => x.gameObject);
        }

        public IEnumerable<GameObject> GetRootObjects()
        {
            if (_cachedRootGameObjects != null)
            {
                _cachedRootGameObjects.RemoveAll(IsGameObjectNull);
                return _cachedRootGameObjects;
            }
            return Enumerable.Empty<GameObject>();
        }

        public IEnumerable<GameObject> GetSearchedOrAllObjects()
        {
            if (_searchResults != null)
            {
                _searchResults.RemoveAll(IsGameObjectNull);
                return _searchResults;
            }
            return GetRootObjects();
        }

        public void Refresh(bool full, Predicate<GameObject> objectFilter)
        {
            if (_cachedRootGameObjects == null)
                full = true;

            Stopwatch sw = null;

            if (full)
            {
                sw = Stopwatch.StartNew();
                _cachedRootGameObjects = new OrderedSet<GameObject>();
                foreach (var gameObject in FindAllRootGameObjects().OrderBy(x => x.name, StringComparer.InvariantCultureIgnoreCase))
                    _cachedRootGameObjects.AddLast(gameObject);
            }
            else
            {
                if (UnityFeatureHelper.SupportsScenes)
                {
                    var newItems = UnityFeatureHelper.GetSceneGameObjects();
                    for (var index = 0; index < newItems.Length; index++)
                        _cachedRootGameObjects.InsertSorted(newItems[index], GameObjectNameComparer.Instance);
                }
            }

            _lastObjectFilter = objectFilter;
            if (objectFilter != null)
                _cachedRootGameObjects.RemoveAll(objectFilter);

            if (full)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, $"Full GameObject list refresh finished in {sw.ElapsedMilliseconds}ms");

                // _lastSearchProperties==true takes too long to open the editor
                if (_searchResults != null && !_lastSearchProperties && _lastSearchString != null)
                    Search(_lastSearchString, _lastSearchProperties, false);
            }
        }

        public void Search(string searchString, bool searchProperties, bool refreshObjects = true)
        {
            _lastSearchProperties = searchProperties;
            _lastSearchString = null;
            _searchResults = null;
            if (!string.IsNullOrEmpty(searchString))
            {
                if (refreshObjects) Refresh(true, _lastObjectFilter);

                _lastSearchString = searchString;

                RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, searchProperties ? $"Deep searching for [{searchString}], this can take a while..." : $"Searching for [{searchString}]");
                var sw = Stopwatch.StartNew();

                _searchResults = GetRootObjects()
                    .SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                    .Where(x => x.name.Contains(searchString, StringComparison.InvariantCultureIgnoreCase) ||
                                x.GetComponents<Component>()
                                    .Any(c => SearchInComponent(searchString, c, searchProperties)))
                    .OrderBy(x => x.name, StringComparer.InvariantCultureIgnoreCase)
                    .Select(x => x.gameObject)
                    .ToList();

                RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, $"Search finished in {sw.ElapsedMilliseconds}ms");
            }
        }

        public static bool SearchInComponent(string searchString, Component c, bool searchProperties)
        {
            if (c == null) return false;

            if (c.ToString().Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                return true;

            var type = c.GetType();
            if (type.Name.Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (!searchProperties)
                return false;

            var nameBlacklist = new HashSet<string>
                {
                    "parent", "parentInternal", "root", "transform", "gameObject",
                    // Animator properties inaccessible outside of OnAnimatorIK
                    "bodyPosition", "bodyRotation",
                    // AudioSource obsolete properties
                    "minVolume", "maxVolume", "rolloffFactor",
                    // NavMeshAgent properties often spewing errors
                    "destination", "remainingDistance"
                };
            var typeBlacklist = new[] { typeof(bool) };

            foreach (var prop in type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => x.CanRead && !nameBlacklist.Contains(x.Name) &&
                            !typeBlacklist.Contains(x.PropertyType)))
            {
                try
                {
                    if (prop.GetValue(c, null).ToString()
                        .Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
                catch
                {
                    // Skip invalid values
                }
            }

            foreach (var field in type
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => !nameBlacklist.Contains(x.Name) && !typeBlacklist.Contains(x.FieldType)))
            {
                try
                {
                    if (field.GetValue(c).ToString()
                        .Contains(searchString, StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
                catch
                {
                    // Skip invalid values
                }
            }

            return false;
        }

        public bool SearchReferences(object objInstance)
        {
            if (objInstance == null || !objInstance.GetType().IsClass) return false;

            var sw = Stopwatch.StartNew();
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, "Deep searching for references, this can take a while...");

            var results = GetRootObjects()
                             .SelectMany(x => x.GetComponentsInChildren<Component>(true))
                             .Where(x => SearchReferencesInComponent(objInstance, x))
                             .OrderBy(x => x.name, StringComparer.InvariantCultureIgnoreCase)
                             .Select(x => x.gameObject)
                             .ToList();

            if (results.Count > 0)
                _searchResults = results;
            else
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, "No references were found");

            RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, $"SearchReferences finished in {sw.ElapsedMilliseconds}ms");

            return results.Count > 0;
        }

        public static bool SearchReferencesInComponent(object objInstance, Component c)
        {
            if (c == null) return false;

            var type = c.GetType();

            var nameBlacklist = new HashSet<string>
                {
                    "parent", "parentInternal", "root", "transform", "gameObject",
                    // Animator properties inaccessible outside of OnAnimatorIK
                    "bodyPosition", "bodyRotation",
                    // AudioSource obsolete properties
                    "minVolume", "maxVolume", "rolloffFactor",
                    // NavMeshAgent properties often spewing errors
                    "destination", "remainingDistance"
                };

            foreach (var prop in type
                .GetProperties(HarmonyLib.AccessTools.all)
                .Where(x => x.CanRead && x.PropertyType.IsClass && !nameBlacklist.Contains(x.Name)))
            {
                try
                {
                    if (prop.GetValue(c, null) == objInstance)
                        return true;
                }
                catch
                {
                    // Skip invalid values
                }
            }

            foreach (var field in type
                .GetFields(HarmonyLib.AccessTools.all)
                .Where(x => x.FieldType.IsClass && !nameBlacklist.Contains(x.Name)))
            {
                try
                {
                    if (field.GetValue(c) == objInstance)
                        return true;
                }
                catch
                {
                    // Skip invalid values
                }
            }

            return false;
        }

        private static bool IsGameObjectNull(GameObject o)
        {
            // This is around 25% faster than o == null
            // Object.IsNativeObjectAlive would be even better at above 35% but isn't public and reflection would eat the gains
            var isGameObjectNull = (object)o == null || o.GetInstanceID() == 0;
            System.Diagnostics.Debug.Assert(isGameObjectNull == (o == null));
            return isGameObjectNull;
        }

        private sealed class GameObjectNameComparer : IComparer<GameObject>
        {
            public static readonly GameObjectNameComparer Instance = new GameObjectNameComparer();
            public int Compare(GameObject x, GameObject y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                var compare = string.Compare(x.name, y.name, StringComparison.OrdinalIgnoreCase);
                // Handle different GOs with same names
                if (compare == 0) compare = x.GetInstanceID().CompareTo(y.GetInstanceID());
                return compare;
            }
        }
    }
}
