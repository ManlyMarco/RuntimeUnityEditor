using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    /// <summary>
    /// A single entry in the context menu.
    /// </summary>
    public readonly struct ContextMenuEntry
    {
        /// <summary>
        /// A list separator.
        /// </summary>
        public static readonly ContextMenuEntry Separator = new ContextMenuEntry();

        /// <inheritdoc cref="ContextMenuEntry(string,System.Func{object,System.Reflection.MemberInfo,bool},System.Action{object,System.Reflection.MemberInfo, string})"/>
        public static ContextMenuEntry Create<T>(string name, Func<T, MemberInfo, bool> onCheckVisible, Action<T, MemberInfo, string> onClick) => Create(new GUIContent(name), onCheckVisible, onClick);

        /// <inheritdoc cref="ContextMenuEntry(string,System.Func{object,System.Reflection.MemberInfo,bool},System.Action{object,System.Reflection.MemberInfo, string})"/>
        public static ContextMenuEntry Create<T>(GUIContent name, Func<T, MemberInfo, bool> onCheckVisible, Action<T, MemberInfo, string> onClick)
        {
            return new ContextMenuEntry(name,
                                        onCheckVisible != null ? (o, info) => o is T oT && onCheckVisible(oT, info) : (Func<object, MemberInfo, bool>)null,
                                        onClick != null ? (o, info, objName) => onClick((T)o, info, objName) : (Action<object, MemberInfo, string>)null);
        }

        /// <summary>
        /// Create a new context menu entry.
        /// </summary>
        /// <param name="name">Name of the enry.</param>
        /// <param name="onCheckVisible">Callback that checks if this item is visible for a given object.</param>
        /// <param name="onClick">Callback invoked when user clicks on this menu entry with the object as argument.</param>
        public ContextMenuEntry(string name, Func<object, MemberInfo, bool> onCheckVisible, Action<object, MemberInfo, string> onClick) : this(new GUIContent(name), onCheckVisible, onClick) { }

        /// <inheritdoc cref="ContextMenuEntry(string,System.Func{object,System.Reflection.MemberInfo,bool},System.Action{object,System.Reflection.MemberInfo, string})"/>
        public ContextMenuEntry(GUIContent name, Func<object, MemberInfo, bool> onCheckVisible, Action<object, MemberInfo, string> onClick)
        {
            _name = name;
            _onCheckVisible = onCheckVisible;
            _onClick = onClick;
        }

        private readonly GUIContent _name;
        private readonly Func<object, MemberInfo, bool> _onCheckVisible;
        private readonly Action<object, MemberInfo, string> _onClick;

        /// <summary>
        /// Determines whether the current instance represents a separator.
        /// </summary>
        public bool IsSeparator()
        {
            return _name == null && _onClick == null;
        }

        /// <summary>
        /// Check if this menu entry should be visible for a given object.
        /// </summary>
        public bool IsVisible(object obj, MemberInfo info)
        {
            return _onCheckVisible == null || _onCheckVisible(obj, info);
        }

        /// <summary>
        /// Draw this menu entry. Handles user clicking on the entry too.
        /// </summary>
        public bool Draw(object obj, MemberInfo info, string name)
        {
            if (_onClick != null)
            {
                if (GUILayout.Button(_name))
                {
                    if (IMGUIUtils.IsMouseRightClick())
                        return false;

                    _onClick(obj, info, name ?? _name?.text);
                    return true;
                }
            }
            else if (_name != null)
            {
                GUILayout.Label(_name);
            }
            else
            {
                GUILayout.Space(4);
            }

            return false;
        }
    }
}
