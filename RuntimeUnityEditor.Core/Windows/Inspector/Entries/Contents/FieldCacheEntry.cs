using System;
using System.Reflection;
using RuntimeUnityEditor.Core.ChangeHistory;
using RuntimeUnityEditor.Core.Utils;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <inheritdoc />
    public class FieldCacheEntry : CacheEntryBase
    {
        /// <inheritdoc />
        public FieldCacheEntry(object ins, FieldInfo f, Type owner) : this(ins, f, owner, null) { }
        /// <inheritdoc />
        public FieldCacheEntry(object ins, FieldInfo f, Type owner, ICacheEntry parent) : base(GetMemberName(ins, f), f.GetFancyDescription(), owner)
        {
            _instance = ins;
            FieldInfo = f ?? throw new ArgumentNullException(nameof(f));
            _parent = parent;
        }

        internal static string GetMemberName(object ins, MemberInfo f)
        {
            if (ins != null)
                return f?.Name;
            return "S/" + f?.Name;
        }

        /// <summary>
        /// FieldInfo for the field.
        /// </summary>
        public FieldInfo FieldInfo { get; }
        /// <summary>
        /// If the field is declared in the owner type.
        /// </summary>
        public bool IsDeclared => Owner == FieldInfo.DeclaringType;
        private readonly object _instance;
        private readonly ICacheEntry _parent;

        /// <inheritdoc />
        public override object GetValueToCache() => FieldInfo.GetValue(_instance);

        /// <inheritdoc />
        protected override bool OnSetValue(object newValue)
        {
            if (!FieldInfo.IsInitOnly)
            {
                Change.MemberAssignment(_instance, newValue, FieldInfo);
                // Needed for structs to propagate changes back to the original field/prop
                if (_parent != null && _parent.CanSetValue()) _parent.SetValue(_instance);
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        public override Type Type()
        {
            return FieldInfo.FieldType;
        }

        /// <inheritdoc />
        public override MemberInfo MemberInfo => FieldInfo;

        /// <inheritdoc />
        public override bool CanSetValue()
        {
            return (FieldInfo.Attributes & FieldAttributes.Literal) == 0 && !FieldInfo.IsInitOnly && (_parent == null || _parent.CanSetValue());
        }
    }
}
