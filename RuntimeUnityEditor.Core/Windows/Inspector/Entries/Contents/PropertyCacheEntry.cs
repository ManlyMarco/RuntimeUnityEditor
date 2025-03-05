using System;
using System.Reflection;
using RuntimeUnityEditor.Core.ChangeHistory;
using RuntimeUnityEditor.Core.Utils;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public class PropertyCacheEntry : CacheEntryBase
    {
        public PropertyCacheEntry(object ins, PropertyInfo p, Type owner) : this(ins, p, owner, null) { }
        public PropertyCacheEntry(object ins, PropertyInfo p, Type owner, ICacheEntry parent) : base(FieldCacheEntry.GetMemberName(ins, p), p.GetFancyDescription(), owner)
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
                Change.MemberAssignment(_instance, newValue, PropertyInfo);
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

        public override MemberInfo MemberInfo => PropertyInfo;

        public override bool CanSetValue()
        {
            return PropertyInfo.CanWrite && (_parent == null || _parent.CanSetValue());
        }
    }
}
