using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.ChangeHistory;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <summary>
    /// Represents a cache entry for an item inside any IList.
    /// </summary>
    public class ListCacheEntry : CacheEntryBase
    {
        private Type _type;
        private readonly IList _list;
        private readonly int _index;

        /// <inheritdoc/>
        public ListCacheEntry(IList container, int index) : base(ReadonlyListCacheEntry.GetListItemName(index), $"Item contained inside of a list.\n\nIndex: {index}\n\nList type: {container.GetType().FullDescription()}", null)
        {
            _index = index;
            _list = container;
        }

        /// <inheritdoc/>
        public override object GetValueToCache()
        {
            return _list.Count > _index ? _list[_index] : "ERROR: The list was changed while browsing!";
        }

        /// <inheritdoc/>
        protected override bool OnSetValue(object newValue)
        {
            if (CanSetValue())
            {
                var oldValue = _list[_index];
                Change.WithUndo($"{{0}}[{_index}] = {{1}}", _list, newValue, (list, o) => list[_index] = o, oldValue: oldValue);
                _type = null;
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public override Type Type()
        {
            return _type ?? (_type = GetValue()?.GetType());
        }

        /// <inheritdoc/>
        public override MemberInfo MemberInfo => null;

        /// <inheritdoc/>
        public override bool CanSetValue()
        {
            return !_list.IsReadOnly;
        }
    }
}
