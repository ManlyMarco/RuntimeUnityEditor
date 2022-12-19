using System;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public interface ICacheEntry
    {
        string Name();
        string TypeName();
        GUIContent GetNameContent();
        /// <summary>
        /// Get object that is entered when variable name is clicked in inspector
        /// </summary>
        object EnterValue();
        object GetValue();
        void SetValue(object newValue);
        Type Type();
        bool CanSetValue();
        bool CanEnterValue();
    }
}