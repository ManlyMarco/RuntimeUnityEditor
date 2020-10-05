using System;
using System.Reflection;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public class PropertyCacheEntry : CacheEntryBase
    {
        public PropertyCacheEntry(object ins, PropertyInfo p) : this(ins, p, null) { }
        public PropertyCacheEntry(object ins, PropertyInfo p, ICacheEntry parent) : base(FieldCacheEntry.GetMemberName(ins, p))
        {
            if (p == null)
                throw new ArgumentNullException(nameof(p));

            _instance = ins;
            PropertyInfo = p;
            _parent = parent;
        }

        public PropertyInfo PropertyInfo { get; }
        private readonly object _instance;
        private readonly ICacheEntry _parent;

        public override bool CanEnterValue()
        {
            return PropertyInfo.CanRead && base.CanEnterValue();
        }

        public override object GetValueToCache()
        {
            if (!PropertyInfo.CanRead)
                return "WRITE ONLY";

            try
            {
                return PropertyInfo.GetValue(_instance, null);
            }
            catch (TargetInvocationException ex)
            {
                return ex.InnerException ?? ex;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        protected override bool OnSetValue(object newValue)
        {
            if (PropertyInfo.CanWrite)
            {
                PropertyInfo.SetValue(_instance, newValue, null);
                // Needed for structs to propagate changes back to the original field/prop
                if (_parent != null && _parent.CanSetValue()) _parent.SetValue(_instance);
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
            return PropertyInfo.CanWrite && (_parent == null || _parent.CanSetValue());
        }
    }
}
