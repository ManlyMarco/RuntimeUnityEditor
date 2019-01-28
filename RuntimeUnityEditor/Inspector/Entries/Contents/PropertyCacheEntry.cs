using System;
using System.Reflection;

namespace RuntimeUnityEditor.Inspector.Entries
{
    public class PropertyCacheEntry : CacheEntryBase
    {
        public PropertyCacheEntry(object ins, PropertyInfo p) : base(FieldCacheEntry.GetMemberName(ins, p))
        {
            if (p == null)
                throw new ArgumentNullException(nameof(p));

            _instance = ins;
            PropertyInfo = p;
        }

        public PropertyInfo PropertyInfo { get; }

        private readonly object _instance;

        public override object GetValueToCache()
        {
            if (!PropertyInfo.CanRead)
                return "WRITE ONLY";

            try { return PropertyInfo.GetValue(_instance, null); }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        protected override bool OnSetValue(object newValue)
        {
            if (PropertyInfo.CanWrite)
            {
                PropertyInfo.SetValue(_instance, newValue, null);
                return true;
            }
            return false;
        }

        public override Type Type()
        {
            return PropertyInfo.PropertyType;
        }

        public override bool CanSetValue()
        {
            return PropertyInfo.CanWrite;
        }
    }
}
