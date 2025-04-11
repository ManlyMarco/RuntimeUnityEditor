using System;
using System.Reflection;
using RuntimeUnityEditor.Core.ChangeHistory;
using RuntimeUnityEditor.Core.Utils;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <summary>
    /// Represents a cache entry for a property in the inspector.
    /// </summary>
    public class PropertyCacheEntry : CacheEntryBase
    {
        /// <inheritdoc/>
        public PropertyCacheEntry(object ins, PropertyInfo p, Type owner) : this(ins, p, owner, null) { }
        /// <inheritdoc/>
        public PropertyCacheEntry(object ins, PropertyInfo p, Type owner, ICacheEntry parent) : base(FieldCacheEntry.GetMemberName(ins, p), p.GetFancyDescription(), owner)
        {
            _instance = ins;
            PropertyInfo = p ?? throw new ArgumentNullException(nameof(p));
            _parent = parent;
        }

        /// <summary>
        /// PropertyInfo for the property.
        /// </summary>
        public PropertyInfo PropertyInfo { get; }
        /// <summary>
        /// Checks if the property is declared in the owner type.
        /// </summary>
        public bool IsDeclared => Owner == PropertyInfo.DeclaringType;
        private readonly object _instance;
        private readonly ICacheEntry _parent;

        /// <inheritdoc/>
        public override bool CanEnterValue() => PropertyInfo.CanRead && base.CanEnterValue();

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override Type Type()
        {
            return PropertyInfo.PropertyType;
        }

        /// <inheritdoc/>
        public override MemberInfo MemberInfo => PropertyInfo;

        /// <inheritdoc/>
        public override bool CanSetValue()
        {
            return PropertyInfo.CanWrite && (_parent == null || _parent.CanSetValue());
        }
    }
}
