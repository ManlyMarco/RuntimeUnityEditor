using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.ChangeHistory;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <summary>
    /// Represents a cache entry for an item inside an array of any rank.
    /// </summary>
    public class ArrayCacheEntry : CacheEntryBase, IFakeCacheEntry
    {
        private Type _type;
        private readonly Array _array;
        private readonly int[] _indices;

        /// <inheritdoc />
        public ArrayCacheEntry(Array container, int flatIndex) : this(container, GetIndices(container, flatIndex)) { }

        private ArrayCacheEntry(Array container, int[] indices) : base(GetArrayItemName(indices), $"Item contained inside of an array.\n\nIndex: [{FormatIndices(indices)}]\n\nArray type: {container.GetType().FullDescription()}", null)
        {
            _array = container;
            _indices = indices;
        }

        /// <summary>
        /// Convert a flat row-major index into per-dimension indices.
        /// </summary>
        internal static int[] GetIndices(Array array, int flatIndex)
        {
            var indices = new int[array.Rank];
            for (var dim = array.Rank - 1; dim >= 0; dim--)
            {
                var length = array.GetLength(dim);
                indices[dim] = array.GetLowerBound(dim) + flatIndex % length;
                flatIndex /= length;
            }
            return indices;
        }

        internal static string GetArrayItemName(int[] indices)
        {
            return indices.Length == 1
                ? ReadonlyListCacheEntry.GetListItemName(indices[0])
                : "Index: [" + FormatIndices(indices) + "]";
        }

        private static string FormatIndices(int[] indices)
        {
            return string.Join(", ", indices.Select(x => x.ToString()).ToArray());
        }

        /// <inheritdoc />
        public override object GetValueToCache()
        {
            return _array.GetValue(_indices);
        }

        /// <inheritdoc />
        protected override bool OnSetValue(object newValue)
        {
            var oldValue = _array.GetValue(_indices);
            Change.WithUndo($"{{0}}[{FormatIndices(_indices)}] = {{1}}", _array, newValue, (arr, o) => arr.SetValue(o, _indices), oldValue: oldValue);
            _type = null;
            return true;
        }

        /// <inheritdoc />
        public override Type Type()
        {
            return _type ?? (_type = GetValue()?.GetType() ?? _array.GetType().GetElementType());
        }

        /// <inheritdoc />
        public override MemberInfo MemberInfo => null;

        /// <inheritdoc />
        public override bool CanSetValue()
        {
            return true;
        }
    }
}
