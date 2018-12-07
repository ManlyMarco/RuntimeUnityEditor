using System;

namespace RuntimeUnityEditor.Inspector.Entries
{
    public class ListCacheEntry : CacheEntryBase
    {
        private readonly object _target;
        private readonly Type _type;

        public ListCacheEntry(object o, int index) : base("ID: " + index)
        {
            _target = o;
            _type = o.GetType();
        }

        public override object GetValueToCache()
        {
            return null;
        }

        public override object GetValue()
        {
            return _target;
        }

        protected override bool OnSetValue(object newValue)
        {
            return false;
        }

        public override Type Type()
        {
            return _type;
        }

        public override bool CanSetValue()
        {
            return false;
        }

    }
}
