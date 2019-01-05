using System;
using System.Collections;

namespace RuntimeUnityEditor.Inspector.Entries
{
    public class ListCacheEntry : CacheEntryBase
    {
        private Type _type;
        private readonly IList _list;
        private readonly int _index;

        public ListCacheEntry(IList container, int index) : base(ReadonlyListCacheEntry.GetListItemName(index))
        {
            _index = index;
            _list = container;
        }

        public override object GetValueToCache()
        {
            return _list[_index];
        }

        protected override bool OnSetValue(object newValue)
        {
            if (CanSetValue())
            {
                _list[_index] = newValue;
                _type = null;
                return true;
            }

            return false;
        }

        public override Type Type()
        {
            return _type ?? (_type = GetValue().GetType());
        }

        public override bool CanSetValue()
        {
            return !_list.IsReadOnly;
        }
    }
}
