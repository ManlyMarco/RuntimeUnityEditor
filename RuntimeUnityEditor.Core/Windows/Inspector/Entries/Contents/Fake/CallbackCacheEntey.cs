using System;
using System.Reflection;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <inheritdoc cref="CacheEntryBase" />
    public class CallbackCacheEntry : CacheEntryBase, IFakeCacheEntry
    {
        private readonly string _message;
        private readonly Action _callback;
        
        /// <inheritdoc />
        internal CallbackCacheEntry(string name, string message) : base(name, "RUE Callback / Feature")
        {
            _message = message;
        }

        /// <inheritdoc />
        public CallbackCacheEntry(string name, string message, Action callback) : base(name, "RUE Callback / Feature")
        {
            _message = message;
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        /// <inheritdoc />
        public override object GetValueToCache() => _message;

        /// <inheritdoc />
        public override bool CanEnterValue() => true;

        /// <inheritdoc />
        public override object EnterValue()
        {
            _callback();
            return null;
        }

        /// <inheritdoc />
        protected override bool OnSetValue(object newValue) => false;

        /// <inheritdoc />
        public override Type Type() => typeof(void);

        /// <inheritdoc />
        public override MemberInfo MemberInfo => null;

        /// <inheritdoc />
        public override bool CanSetValue() => false;

        /// <inheritdoc />
        public override string ToString() => $"{_message} (RUE Callback / Feature)";
    }

    /// <inheritdoc />
    public class CallbackCacheEntry<T> : CallbackCacheEntry
    {
        private readonly Func<T> _callback;

        /// <inheritdoc />
        public CallbackCacheEntry(string name, string message, Func<T> callback) : base(name, message)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        /// <inheritdoc />
        public override object EnterValue() => _callback();

        /// <inheritdoc />
        public override Type Type() => typeof(T);
    }
}