using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.ChangeHistory
{
    /// <summary>
    /// API for making changes that are tracked in the Change History window and optionally undoable.
    /// </summary>
    public static class Change
    {
        /// <summary>
        /// List of changes made since startup in chronological order.
        /// </summary>
        public static List<IChange> Changes { get; } = new List<IChange>();

        /// <summary>
        /// Assigns a new value to a property or field and tracks the change in the Change History window.
        /// The original value is automatically saved to allow undoing the change.
        /// </summary>
        /// <typeparam name="TObj">Type of the object that contains the affected member</typeparam>
        /// <typeparam name="TVal">Type of the member</typeparam>
        /// <param name="target">Object that contains the affected member</param>
        /// <param name="newValue">New value to be set</param>
        /// <param name="memberSelector">Lambda that selects either a field or a property that should have the value set to it</param>
        public static IChange MemberAssignment<TObj, TVal>(TObj target, TVal newValue, Expression<Func<TObj, TVal>> memberSelector)
        {
            if (memberSelector == null)
                throw new ArgumentNullException(nameof(memberSelector));

            if (memberSelector.Body is MemberExpression body)
                return MemberAssignment(target, newValue, body.Member);

            throw new ArgumentException("Lambda must be a Property or a Field", nameof(memberSelector));
        }

        /// <summary>
        /// Assigns a new value to a property or field and tracks the change in the Change History window.
        /// The original value is automatically saved to allow undoing the change.
        /// </summary>
        /// <typeparam name="TObj">Type of the object that contains the affected member</typeparam>
        /// <typeparam name="TVal">Type of the member</typeparam>
        /// <param name="target">Object that contains the affected member</param>
        /// <param name="newValue">New value to be set</param>
        /// <param name="member">Field or a property that should have the value set to it</param>
        public static IChange MemberAssignment<TObj, TVal>(TObj target, TVal newValue, MemberInfo member)
        {
            if (member == null)
                throw new ArgumentNullException(nameof(member));

            if (member.DeclaringType != typeof(TObj))
                throw new ArgumentException("Member must be declared in the type of the target object", nameof(member));

            if (member is PropertyInfo pi)
            {
                void PropSet(TObj obj, TVal val) => pi.SetValue(obj, val, null);
                var defaultValue = pi.CanRead ? (TVal)pi.GetValue(target, null) : default;
                return Do("{0}." + pi.Name + " = {1}", target, newValue, PropSet, pi.CanRead ? PropSet : (Action<TObj, TVal>)null, defaultValue);
            }

            if (member is FieldInfo fi)
            {
                void FieldSet(TObj obj, TVal val) => fi.SetValue(obj, val);
                var defaultValue = (TVal)fi.GetValue(target);
                return Do("{0}." + fi.Name + " = {1}", target, newValue, FieldSet, FieldSet, defaultValue);
            }

            throw new ArgumentException("Member must be a Property or a Field", nameof(member));
        }

        /// <summary>
        /// Assigns a new value by using the set delegate and tracks the change in the Change History window. No undo is possible.
        /// </summary>
        /// <typeparam name="TObj">Type of the object that has its member(s) modified</typeparam>
        /// <typeparam name="TVal">Type of the value to be changed</typeparam>
        /// <param name="actionNameFormat">String format of how this change is represented in the Change History window. {0} inserts type name of <see cref="TObj"/>, while {1} inserts <paramref name="newValue"/> (format string can be used, e.g. {1:00})</param>
        /// <param name="target">Object that has its member(s) modified</param>
        /// <param name="newValue">New value to be set</param>
        /// <param name="set">Action used to set the new value</param>
        public static IChange WithoutUndo<TObj, TVal>(string actionNameFormat, TObj target, TVal newValue, Action<TObj, TVal> set)
        {
            return Do(actionNameFormat: actionNameFormat,
                      target: target,
                      newValue: newValue,
                      set: set,
                      undoAction: null,
                      oldValue: default);
        }

        /// <summary>
        /// Assigns a new value by using the set delegate and tracks the change in the Change History window.
        /// Undo is possible by providing an undo value, by using a custom action, or by using the get delegate to automatically get the original value.
        /// If none of the undo options are provided, undoing the change will set it to the default value of <see cref="TVal"/>.
        /// </summary>
        /// <typeparam name="TObj">Type of the object that has its member(s) modified</typeparam>
        /// <typeparam name="TVal">Type of the value to be changed</typeparam>
        /// <param name="actionNameFormat">String format of how this change is represented in the Change History window. {0} inserts type name of <see cref="TObj"/>, while {1} inserts <paramref name="newValue"/> (format strings can be used, e.g. {0:00.0})</param>
        /// <param name="target">Object that has its member(s) modified</param>
        /// <param name="newValue">New value to be set</param>
        /// <param name="set">Action used to set the new value</param>
        /// <param name="undoAction">Action used to undo the change</param>
        /// <param name="getOldValue">Function used to get the current value to later use for undo</param>
        /// <param name="oldValue">Value to use for undo</param>
        public static IChange WithUndo<TObj, TVal>(string actionNameFormat, TObj target, TVal newValue, Action<TObj, TVal> set,
                                                   Action<TObj, TVal> undoAction = null,
                                                   Func<TObj, TVal> getOldValue = null, TVal oldValue = default)
        {
            if (Equals(oldValue, default) && getOldValue != null)
                oldValue = getOldValue(target);

            return Do(actionNameFormat, target, newValue, set, undoAction, oldValue);
        }

        private static IChange Do<TObj, TVal>(string actionNameFormat, TObj target, TVal newValue,
                                              Action<TObj, TVal> set, Action<TObj, TVal> undoAction, TVal oldValue)
        {
            if (actionNameFormat == null) throw new ArgumentNullException(nameof(actionNameFormat));
            if (set == null) throw new ArgumentNullException(nameof(set));

            set(target, newValue);

            // Check if last change was to the same property, if so, combine them to avoid spam
            if (Changes.Count > 0 && Changes[Changes.Count - 1] is ChangeAssignment<TObj, TVal> lastChange &&
                lastChange.ActionNameFormat == actionNameFormat && Equals(lastChange.Target, target))
            {
                lastChange.UpdateNewValue(newValue);
                return lastChange;
            }

            var change = new ChangeAssignment<TObj, TVal>(target, newValue, oldValue, undoAction ?? set, actionNameFormat);
            Changes.Add(change);
            return change;
        }

        /// <summary>
        /// Do an action and track it in the Change History window. Optionally undoable.
        /// </summary>
        /// <typeparam name="TObj">Type of the object that the action uses</typeparam>
        /// <param name="actionNameFormat">String format of how this change is represented in the Change History window. {0} inserts type name of <see cref="TObj"/> (format strings can be used, e.g. {0:00.0})</param>
        /// <param name="target">Object that has its member(s) modified</param>
        /// <param name="action">Action invoked on the target object</param>
        /// <param name="undoAction">Action invoked on the target object to undo the change</param>
        public static IChange Action<TObj>(string actionNameFormat, TObj target, Action<TObj> action, Action<TObj> undoAction = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var change = new ChangeAction<TObj>(actionNameFormat, target, undoAction);

            action(target);

            Changes.Add(change);
            return change;
        }

        /// <summary>
        /// Report a change that already happened and track it in the Change History window. Optionally undoable.
        /// </summary>
        /// <param name="actionName">How this change is represented in the Change History window</param>
        /// <param name="undoAction">Action invoked to undo the change</param>
        public static IChange Report(string actionName, Action undoAction = null)
        {
            if (actionName == null) throw new ArgumentNullException(nameof(actionName));
            var change = new ChangeAction<object>(actionName, undoAction);

            Changes.Add(change);
            return change;
        }

        internal static string GetTargetDisplayString(object target)
        {
            if (target is UnityEngine.Object uObj && !uObj)
                return uObj.GetType().GetSourceCodeRepresentation() + "(Destroyed)";

            switch (target)
            {
                case Transform t:
                    return $"({t.GetFullTransfromPath()})::{t.GetType().Name}";
                case GameObject go:
                    return $"({go.transform.GetFullTransfromPath()})::GameObject";
                case Component c:
                    return $"({c.transform.GetFullTransfromPath()})::{c.GetType().GetSourceCodeRepresentation()}";
                case string str:
                    return str;
                default:
                    return target == null ? "null" : target.GetType().FullDescription();
            }
        }
    }
}