using System;
using System.Reflection;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <inheritdoc />
    public class CallbackCacheEntry : CacheEntryBase
    {
        private readonly string _message;
        private readonly Action _callback;

        /// <inheritdoc />
        public CallbackCacheEntry(string name, string message, Action callback) : base(name, "RUE Callback / Feature")
        {
            _message = message;
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        /// <inheritdoc />
        public override object GetValueToCache()
        {
            return _message;
        }

        /// <inheritdoc />
        public override bool CanEnterValue()
        {
            return true;
        }

        /// <inheritdoc />
        public override object EnterValue()
        {
            _callback();
            return null;
        }

        /// <inheritdoc />
        protected override bool OnSetValue(object newValue)
        {
            return false;
        }

        /// <inheritdoc />
        public override Type Type()
        {
            return typeof(void);
        }

        /// <inheritdoc />
        public override MemberInfo MemberInfo => null;

        /// <inheritdoc />
        public override bool CanSetValue()
        {
            return false;
        }
    }

    /// <inheritdoc />
    public class CallbackCacheEntry<T> : CacheEntryBase
    {
        private readonly string _message;
        private readonly Func<T> _callback;

        /// <inheritdoc />
        public CallbackCacheEntry(string name, string message, Func<T> callback) : base(name, "RUE Callback / Feature")
        {
            _message = message;
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        /// <inheritdoc />
        public override object GetValueToCache()
        {
            return _message;
        }

        /// <inheritdoc />
        public override bool CanEnterValue()
        {
            return true;
        }

        /// <inheritdoc />
        public override object EnterValue()
        {
            return _callback();
        }

        /// <inheritdoc />
        protected override bool OnSetValue(object newValue)
        {
            return false;
        }

        /// <inheritdoc />
        public override Type Type()
        {
            return typeof(T);
        }

        /// <inheritdoc />
        public override MemberInfo MemberInfo { get; }

        /// <inheritdoc />
        public override bool CanSetValue()
        {
            return false;
        }
    }
}