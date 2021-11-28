using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.ObjectTree
{
    /// <summary>
    /// Keeps track of root gameobjects and allows searching objects in the scene
    /// </summary>
    public class GameObjectSearcher
    {
        private List<GameObject> _cachedRootGameObjects;
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
                _cachedRootGameObjects.RemoveAll(o => o == null);
                return _cachedRootGameObjects;
            }
            return Enumerable.Empty<GameObject>();
        }

        public IEnumerable<GameObject> GetSearchedOrAllObjects()
        {
            if (_searchResults != null)
            {
                _searchResults.RemoveAll(o => o == null);
                return _searchResults;
            }
            return GetRootObjects();
        }

        public void Refresh(bool full, Predicate<GameObject> objectFilter)
        {
            Stopwatch sw = null;
            if (full) sw = Stopwatch.StartNew();

            if (_cachedRootGameObjects == null || full)
            {
                _cachedRootGameObjects = FindAllRootGameObjects().OrderBy(x => x.name, StringComparer.InvariantCultureIgnoreCase).ToList();
                full = true;
            }
            else
            {
                _cachedRootGameObjects.RemoveAll(o => o == null);
            }

            if (UnityFeatureHelper.SupportsScenes && !full)
            {
                var any = false;
                var newItems = UnityFeatureHelper.GetSceneGameObjects();
                foreach (var newItem in newItems)
                {
                    if (!_cachedRootGameObjects.Contains(newItem))
                    {
                        any = true;
                        _cachedRootGameObjects.Add(newItem);
                    }
                }
                if (any)
                {
                    _cachedRootGameObjects.Sort((o1, o2) => string.Compare(o1.name, o2.name, StringComparison.InvariantCultureIgnoreCase));
                }
            }

            _lastObjectFilter = objectFilter;
            if (objectFilter != null)
                _cachedRootGameObjects.RemoveAll(objectFilter);

            if (full)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, $"Full GameObject list refresh finished in {sw.ElapsedMilliseconds}ms");

                // _lastSearchProperties=true takes too long to open the editor
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
    }
}
