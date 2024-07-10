using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
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
        private static bool _lastSearchProperties;
        private bool _lastSearchComponents;
        private bool _lastSearchNames;
        
        // -1 for all scenes
        // todo only additive scenes option?
        private int _sceneIndexFilter = -1;
        internal int SceneIndexFilter
        {
            get => _sceneIndexFilter;
            set
            {
                if (_sceneIndexFilter != value)
                {
                    _sceneIndexFilter = value;
                    QueueFullReindex();
                    RedoLastSearch();
                }
            }
        }

        /// <summary>
        /// Search results are currently being indexed.
        /// </summary>
        public bool BusyIndexing => _indexingTimer.ElapsedMilliseconds > 100 && _indexingCo != null;

        /// <summary>
        /// A filtered list is currently being shown instead of all items.
        /// </summary>
        public bool IsSearching() => _searchResults != null;

        /// <summary>
        /// Find all currently existing root Transforms. Slow.
        /// </summary>
        public static IEnumerable<GameObject> FindAllRootGameObjects()
        {
            return Resources.FindObjectsOfTypeAll<Transform>()
                .Where(t => t.parent == null)
                .Select(x => x.gameObject);
        }

        /// <summary>
        /// Get a mostly up-to-date list of all root Transforms. Fast.
        /// </summary>
        public IEnumerable<GameObject> GetRootObjects()
        {
            if (SceneIndexFilter >= 0)
            {
                return UnityFeatureHelper.GetSceneRootObjects(SceneIndexFilter);
            }

            if (_cachedRootGameObjects != null)
            {
                _cachedRootGameObjects.RemoveAll(IsGameObjectNull);
                return _cachedRootGameObjects;
            }

            return Enumerable.Empty<GameObject>();
        }

        /// <summary>
        /// Get a list of what should be displayed.
        /// </summary>
        public IEnumerable<GameObject> GetSearchedOrAllObjects()
        {
            if (_searchResults != null)
            {
                _searchResults.RemoveAll(IsGameObjectNull);
                return _searchResults;
            }
            return GetRootObjects();
        }

        /// <summary>
        /// Refresh the list of GameObjects currently in the scene.
        /// </summary>
        /// <param name="full">Gather root Transforms again, slow. Otherwise use tricks to approximate the same result but much faster.</param>
        /// <param name="objectFilter">Optional filter to exclude some GameObjects from the results.</param>
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

                QueueFullReindex();
            }
            else
            {
                if (UnityFeatureHelper.SupportsScenes)
                {
                    var newItems = UnityFeatureHelper.GetActiveSceneGameObjects();
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
                    RedoLastSearch();
            }
        }

        private void RedoLastSearch()
        {
            Search(_lastSearchString, _lastSearchNames, _lastSearchComponents, _lastSearchProperties, false);
        }

        /// <summary>
        /// Perform a search in all current GameObjects. GameOjbect names are always searched.
        /// </summary>
        /// <param name="searchString">What to search for. Checks if the string is contained while ignoring case.</param>
        /// <param name="searchNames">Search GameObject names.</param>
        /// <param name="searchComponents">Search component names.</param>
        /// <param name="searchProperties">Search values of component properties. Very slow.</param>
        /// <param name="refreshObjects">Perform a full refresh if necessary.</param>
        public void Search(string searchString, bool searchNames, bool searchComponents, bool searchProperties, bool refreshObjects = true)
        {
            if (_lastSearchProperties != searchProperties)
            {
                if (searchProperties)
                    QueueFullReindex();

                _lastSearchProperties = searchProperties;
            }

            _lastSearchNames = searchNames;
            _lastSearchComponents = searchComponents;

            _lastSearchString = null;
            _searchResults = null;
            if (!string.IsNullOrEmpty(searchString))
            {
                if (refreshObjects && SceneIndexFilter >= 0) Refresh(true, _lastObjectFilter);

#if DEBUG
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, $"Searching in{(searchNames ? " Names" : "")}{(searchComponents ? " Components" : "")}{(searchProperties ? " Properties" : "")} for [{searchString}]");
#endif
                var sw = Stopwatch.StartNew();

                _lastSearchString = searchString.ToLowerInvariant();

                try
                {
                    _searchResults = DoSearch();

                    if (sw.ElapsedMilliseconds > 100)
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, $"Search took {sw.ElapsedMilliseconds}ms!");
                }
                catch (Exception e)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, "Search failed: " + e);
                }
            }
            else
            {
                QueueFullReindex();
            }
        }

        /// <summary>
        /// Search for a string inside a given component. Component name is always searched.
        /// </summary>
        /// <param name="searchString">What to search for. Checks if the string is contained while ignoring case.</param>
        /// <param name="c">Component to search in.</param>
        /// <param name="searchProperties">Search values of component's properties. Very slow.</param>
        public static bool SearchInComponent(string searchString, Component c, bool searchProperties)
        {
            if (c == null) return false;

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

        /// <summary>
        /// Search for references to an object inside of all components currently instantiated.
        /// Only top-level properties and fields are searched inside the component.
        /// </summary>
        /// <param name="objInstance">Instance to search for.</param>
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

        /// <summary>
        /// Search for references to an object inside of all components currently instantiated.
        /// Only top-level properties and fields are searched inside the component.
        /// </summary>
        /// <param name="c">Component to search in.</param>
        /// <param name="objInstance">Instance to search for.</param>
        public static bool SearchReferencesInComponent(object objInstance, Component c)
        {
            if (c == null) return false;

            var type = c.GetType();

            foreach (var prop in type
                .GetProperties(HarmonyLib.AccessTools.all)
                .Where(x => x.CanRead && x.PropertyType.IsClass && !_nameBlacklist.Contains(x.Name)))
            {
                try
                {
                    if (prop.GetValue(c, null) == objInstance)
                        return true;
                }
                catch { /* Skip invalid values */ }
            }

            foreach (var field in type
                .GetFields(HarmonyLib.AccessTools.all)
                .Where(x => x.FieldType.IsClass && !_nameBlacklist.Contains(x.Name)))
            {
                try
                {
                    if (field.GetValue(c) == objInstance)
                        return true;
                }
                catch { /* Skip invalid values */ }
            }

            return false;
        }

        private static bool IsGameObjectNull(GameObject o)
        {
            // Looks like Unity 4.x doesn't set InstanceID to 0 after object is destroyed. Checking SupportsScenes seems close enough.
            if (!UnityFeatureHelper.SupportsScenes) return o == null;

            // This is around 25% faster than o == null
            // Object.IsNativeObjectAlive would be even better at above 35% but isn't public and reflection would eat the gains
            var isGameObjectNull = (object)o == null || o.GetInstanceID() == 0;
            System.Diagnostics.Debug.Assert(isGameObjectNull == (o == null));
            return isGameObjectNull;
        }
        private static bool IsGameObjectAlive(GameObject o)
        {
            return !IsGameObjectNull(o);
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

        #region Search and indexing

        private List<GameObject> DoSearch()
        {
            StartIndexing();

            var targets = GetRootObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Select(x => x.gameObject).Where(IsGameObjectAlive).ToList();

            var searched = targets.RunParallel(go =>
            {
                _searchIndex.TryGetValue(go, out var searchStrings);
                return searchStrings != null && searchStrings.Match(_lastSearchString, _lastSearchNames, _lastSearchComponents, _lastSearchProperties) ? searchStrings : null;
            });

            // This one's kind of slow because of sorting
            var results = searched.Where(x => x != null)
                                  .OrderBy(x => x.Name, StringComparer.Ordinal)
                                  .ThenBy(x => x.Owner.GetInstanceID()) // Stops order changing randomly as list is populated
                                  .Select(x => x.Owner)
                                  .ToList();

            return results;
        }

        private readonly Dictionary<GameObject, SearchStrings> _searchIndex = new Dictionary<GameObject, SearchStrings>();
        private readonly Stopwatch _indexingTimer = new Stopwatch();
        private Coroutine _indexingCo;
        private bool _fullIndexUpdate = true;

        private void QueueFullReindex()
        {
            _fullIndexUpdate = true;
        }

        private void StartIndexing()
        {
            if (_indexingCo == null)
                _indexingCo = RuntimeUnityEditorCore.PluginObject.StartCoroutine(IndexObjectsCo());
        }

        private void StopIndexing()
        {
            if (_indexingCo != null)
                RuntimeUnityEditorCore.PluginObject.StopCoroutine(_indexingCo);
            _indexingCo = null;

            if (_indexingTimer.ElapsedMilliseconds > 0)
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, $"Took {_indexingTimer.ElapsedMilliseconds}ms to index objects in the scene");
            _indexingTimer.Reset();
        }

        private IEnumerator IndexObjectsCo()
        {
            _indexingTimer.Start();

        Restart:

            _searchIndex.Keys.Where(IsGameObjectNull).ToList().ForEach(x => _searchIndex.Remove(x));

            var rootObjects = GetRootObjects();

            if (!_fullIndexUpdate)
                rootObjects = rootObjects.Where(x => !_searchIndex.ContainsKey(x)).ToList();
            _fullIndexUpdate = false;

            yield return null;

            var timer = Stopwatch.StartNew();

            foreach (var tr in rootObjects.SelectMany(go => go.GetComponentsInChildren<Transform>(true)))
            {
                var go = tr.gameObject;
                _searchIndex[go] = SearchStrings.Create(go, _lastSearchProperties);

                if (timer.ElapsedMilliseconds > 20)
                {
                    _indexingTimer.Stop();

                    yield return null;

                    _indexingTimer.Start();

                    _searchResults = DoSearch();

                    if (_fullIndexUpdate)
                    {
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, "Restarting indexing...");
                        goto Restart;
                    }

                    timer.Reset();
                    timer.Start();
                }
            }

            _indexingCo = null;
            StopIndexing();
        }

        private static readonly HashSet<string> _nameBlacklist = new HashSet<string>
        {
            "parent", "parentInternal", "root", "transform", "gameObject",
            // Animator properties inaccessible outside of OnAnimatorIK
            "bodyPosition", "bodyRotation",
            // AudioSource obsolete properties
            "minVolume", "maxVolume", "rolloffFactor",
            // NavMeshAgent properties often spewing errors
            "destination", "remainingDistance"
        };
        private static readonly Type _boolType = typeof(bool);
        private static string ExtractComponentSearchString(Component c)
        {
            var type = c.GetType();

            var sb = new StringBuilder();

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                     .Where(x => x.CanRead && x.PropertyType != _boolType && !_nameBlacklist.Contains(x.Name)))
            {
                try { sb.AppendLine(prop.GetValue(c, null)?.ToString()); }
                catch { /* Skip invalid values */ }
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                      .Where(x => x.FieldType != _boolType && !_nameBlacklist.Contains(x.Name)))
            {
                try { sb.AppendLine(field.GetValue(c)?.ToString()); }
                catch { /* Skip invalid values */ }
            }

            return sb.ToString();
        }

        private sealed class SearchStrings
        {
            public readonly GameObject Owner;
            public readonly string Name;
            public readonly string ComponentNames;
            public readonly string ComponentProperties;

            private SearchStrings(GameObject owner, string name, string componentNames, string componentProperties)
            {
                Owner = owner;
                Name = name;
                ComponentNames = componentNames;
                ComponentProperties = componentProperties;
            }

            public bool Match(string searchString, bool searchNames, bool searchComponents, bool searchProperties)
            {
                if (searchNames && Name.Contains(searchString, StringComparison.Ordinal))
                    return true;

                if (searchComponents && ComponentNames != null && ComponentNames.Contains(searchString, StringComparison.Ordinal))
                    return true;

                if (searchProperties && ComponentProperties != null && ComponentProperties.Contains(searchString, StringComparison.Ordinal))
                    return true;

                return false;
            }

            public static SearchStrings Create(GameObject obj, bool searchProperties)
            {
                if (IsGameObjectNull(obj)) return null;

                var components = obj.GetComponents<Component>().Where(c => c).ToArray();
                var componentNames = string.Join("\0", components.Select(c => c.GetType().Name).ToArray());
                var componentProperties = searchProperties ? string.Join("\0", components.Select(ExtractComponentSearchString).ToArray()) : null;
                return new SearchStrings(obj, obj.name.ToLowerInvariant(), componentNames.ToLowerInvariant(), componentProperties?.ToLowerInvariant());
            }
        }

        #endregion
    }
}
