using System;
using System.Reflection;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <summary>
    /// Represents a read-only cache entry in the inspector.
    /// </summary>
    public class ReadonlyCacheEntry : CacheEntryBase
    {
        /// <summary>
        /// The object that this entry represents.
        /// </summary>
        public readonly object Object;
        private readonly Type _type;
        private string _tostringCache;

        /// <inheritdoc/>
        public ReadonlyCacheEntry(string name, object obj) : base(name, "Read-only item (RUE-only, it doesn't actually exist).")
        {
            Object = obj;
            _type = obj.GetType();
        }

        /// <inheritdoc/>
        public override object GetValueToCache()
        {
            return Object;
        }

        /// <summary>
        /// Always false.
        /// </summary>
        protected override bool OnSetValue(object newValue)
        {
            return false;
        }

        /// <inheritdoc/>
        public override Type Type()
        {
            return _type;
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        public override MemberInfo MemberInfo => null;

        /// <summary>
        /// Always false.
        /// </summary>
        public override bool CanSetValue()
        {
            return false;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return _tostringCache ?? (_tostringCache = Name() + " | " + Object);
        }
    }
}