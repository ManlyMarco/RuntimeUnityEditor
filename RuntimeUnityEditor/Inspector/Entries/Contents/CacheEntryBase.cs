using System;
using RuntimeUnityEditor.Core.Utils;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public abstract class CacheEntryBase : ICacheEntry
    {
        // todo add gui option
        public static bool CachingEnabled { get; set; } = false;

        protected CacheEntryBase(string name)
        {
            _name = name;
        }

        public virtual object EnterValue()
        {
            if (!CachingEnabled) return GetValue();

            return _valueCache = (GetValueToCache() ?? GetValue());
        }

        public abstract object GetValueToCache();
        private object _valueCache;
        public virtual object GetValue()
        {
            if (!CachingEnabled) return GetValueToCache();

            return _valueCache ?? (_valueCache = GetValueToCache());
        }

        public void SetValue(object newValue)
        {
            if (OnSetValue(newValue))
                _valueCache = newValue;
        }

        protected abstract bool OnSetValue(object newValue);

        public abstract Type Type();
        public abstract bool CanSetValue();

        private readonly string _name;
        private string _typeName;

        public string Name() => _name;

        public string TypeName()
        {
            if (_typeName == null)
            {
                var type = Type();
                if (type != null)
                    _typeName = type.GetSourceCodeRepresentation();
                else
                    _typeName = "INVALID";
            }
            return _typeName;
        }

        private bool? _canEnter;
        public virtual bool CanEnterValue()
        {
            if (_canEnter == null)
            {
                var type = Type();
                _canEnter = type != null && !type.IsPrimitive;
            }
            return _canEnter.Value;
        }
    }
}