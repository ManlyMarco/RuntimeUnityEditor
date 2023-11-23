using System;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <summary>
    /// Representation of a type's member or some other entity that is shown inside the Inspector's member list.
    /// </summary>
    public interface ICacheEntry
    {
        /// <summary>
        /// Name of the member.
        /// </summary>
        string Name();
        /// <summary>
        /// Name of the member's field/return type.
        /// </summary>
        string TypeName();
        /// <summary>
        /// Name content shown in the UI.
        /// </summary>
        GUIContent GetNameContent();
        /// <summary>
        /// Type that owns this member.
        /// </summary>
        Type Owner { get; }
        /// <summary>
        /// Get object that is entered when variable name is clicked in inspector
        /// </summary>
        object EnterValue();
        /// <summary>
        /// Get the member's value.
        /// </summary>
        object GetValue();
        /// <summary>
        /// Set the member's value.
        /// </summary>
        void SetValue(object newValue);
        /// <summary>
        /// Member's field/return type.
        /// </summary>
        Type Type();
        /// <summary>
        /// Value of this member can be set.
        /// </summary>
        bool CanSetValue();
        /// <summary>
        /// Inspector can further inspect the value returned by this member.
        /// </summary>
        bool CanEnterValue();
    }
}