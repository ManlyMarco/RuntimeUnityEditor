using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <inheritdoc cref="ICacheEntry"/>
    public abstract class CacheEntryBase : ICacheEntry
    {
        /// <summary>
        /// Enable caching of the value returned by <see cref="GetValueToCache"/>. This will speed up the inspector when inspecting large objects.
        /// TODO: Disabled because of relatively low performance impact. Maybe add as a gui option
        /// </summary>
        public static bool CachingEnabled { get; set; } = false;

        /// <summary>
        /// Constructor for the cache entry.
        /// </summary>
        protected CacheEntryBase(string name, string description, Type owner = null)
        {
            Owner = owner;
            _name = name;
            _nameContent = new GUIContent(_name, null, description + "\n\nLeft click to inspect in current tab\nMiddle click to inspect in a new tab\nRight click to open a menu with more options");
        }

        /// <summary>
        /// Name of the member for display in the UI.
        /// </summary>
        public GUIContent GetNameContent() => _nameContent;

        /// <summary>
        /// Get object that is entered when variable name is clicked in inspector
        /// </summary>
        public virtual object EnterValue()
        {
            if (!CachingEnabled) return GetValue();

            return _valueCache = (GetValueToCache() ?? GetValue());
        }

        /// <summary>
        /// Get the member's value. This method is called when caching is disabled or when the cache is empty.
        /// </summary>
        public abstract object GetValueToCache();
        private object _valueCache;
        /// <summary>
        /// Get the member's value, either from the cache or by calling <see cref="GetValueToCache"/>.
        /// </summary>
        public virtual object GetValue()
        {
            if (!CachingEnabled) return GetValueToCache();

            return _valueCache ?? (_valueCache = GetValueToCache());
        }

        /// <summary>
        /// Set the member's value. This method is called when the value is changed in the inspector.
        /// </summary>
        public void SetValue(object newValue)
        {
            if (OnSetValue(newValue))
                _valueCache = newValue;
        }

        /// <summary>
        /// Set the member's value. This method is called when the value is changed in the inspector.
        /// </summary>
        protected abstract bool OnSetValue(object newValue);

        /// <summary>
        /// Return/field type of this member.
        /// </summary>
        public abstract Type Type();
        /// <summary>
        /// Member's reflection info.
        /// </summary>
        public abstract MemberInfo MemberInfo { get; }
        /// <summary>
        /// True if the value of this member can be set. This is used to determine if the member is editable in the inspector.
        /// </summary>
        public abstract bool CanSetValue();

        private readonly string _name;
        private string _typeName;
        /// <summary>
        /// Type that contains this member.
        /// </summary>
        public Type Owner { get; }

        /// <summary>
        /// Name of the member.
        /// </summary>
        public string Name() => _name;

        /// <summary>
        /// Name of the member's field/return type for use in UI.
        /// </summary>
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
        private readonly GUIContent _nameContent;

        /// <inheritdoc />
        public virtual bool CanEnterValue()
        {
            if (_canEnter == null)
            {
                var type = Type();
                _canEnter = type != null && !type.IsPrimitive;
            }
            return _canEnter.Value;
        }

        /// <inheritdoc />
        public int ItemHeight { get; set; } = Inspector.InspectorRecordInitialHeight;
    }
}