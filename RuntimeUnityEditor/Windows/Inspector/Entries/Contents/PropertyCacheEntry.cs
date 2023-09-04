using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public class PropertyCacheEntry : CacheEntryBase
    {
        public PropertyCacheEntry(object ins, PropertyInfo p, Type owner, object ownerInstance) : this(ins, p, owner,ownerInstance, null) { }
        public PropertyCacheEntry(object ins, PropertyInfo p, Type owner, object ownerInstance, ICacheEntry parent) : base(FieldCacheEntry.GetMemberName(ins, p), p.GetFancyDescription(), owner, ownerInstance ?? parent.GetValue())
        {
            _instance = ins;
            PropertyInfo = p ?? throw new ArgumentNullException(nameof(p));
            _parent = parent;
        }

        public PropertyInfo PropertyInfo { get; }
        public bool IsDeclared => Owner == PropertyInfo.DeclaringType;
        private readonly object _instance;
        private readonly ICacheEntry _parent;

        public override bool CanEnterValue() => PropertyInfo.CanRead && base.CanEnterValue();

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
