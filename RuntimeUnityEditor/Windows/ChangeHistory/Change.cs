using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.ChangeHistory
{
    public static class Change
    {
        public static List<IChange> Changes { get; } = new List<IChange>();

        public static IChange MemberAssignment<TObj, TVal>(TObj target, TVal newValue, Expression<Func<TObj, TVal>> memberSelector)
        {
            if (memberSelector.Body is MemberExpression body)
                return MemberAssignment(target, newValue, body.Member);
            throw new ArgumentException("Lambda must be a Property or a Field", nameof(memberSelector));
        }

        public static IChange MemberAssignment<TObj, TVal>(TObj target, TVal newValue, MemberInfo member)
        {
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

        public static IChange WithoutUndo<TObj, TVal>(string actionNameFormat, TObj target, TVal newValue, Action<TObj, TVal> set)
        {
            return Do(actionNameFormat: actionNameFormat,
                      target: target,
                      newValue: newValue,
                      set: set,
                      undoAction: null,
                      oldValue: default);
        }

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

        public static IChange Action<TObj>(string actionNameFormat, TObj target, Action<TObj> action, Action<TObj> undoAction = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var change = new ChangeAction<TObj>(actionNameFormat, target, undoAction);

            action(target);

            Changes.Add(change);
            return change;
        }
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
                return uObj.GetType().FullName + "(Destroyed)";

            switch (target)
            {
                case Transform t:
                    return $"({t.GetFullTransfromPath()})::{t.GetType().Name}";
                case GameObject go:
                    return $"({go.transform.GetFullTransfromPath()})::GameObject";
                case Component c:
                    return $"({c.transform.GetFullTransfromPath()})::{c.GetType().FullName}";
                case string str:
                    return str;
                default:
                    return target == null ? "null" : target.GetType().FullDescription();
            }
        }
    }
}