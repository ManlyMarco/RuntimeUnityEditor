using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RuntimeUnityEditor.Core.Clipboard;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine.Events;

namespace RuntimeUnityEditor.Core.Utils
{
    /// <summary>
    /// Utility class for reflection operations.
    /// </summary>
    public static class ReflectionUtils
    {
        /// <summary>
        /// Sets the value of a member (field or property) on an object.
        /// </summary>
        public static void SetValue(MemberInfo member, object obj, object value)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    ((FieldInfo)member).SetValue(obj, value);
                    break;
                case MemberTypes.Property:
                    ((PropertyInfo)member).SetValue(obj, value, null);
                    break;
            }
        }

        /// <summary>
        /// Gets the value of a member (field or property) on an object.
        /// </summary>
        public static object GetValue(MemberInfo member, object obj)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).GetValue(obj);
                case MemberTypes.Property:
                    return ((PropertyInfo)member).GetValue(obj, null);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Gets the details of a UnityEventBase object, including the target and method information.
        /// </summary>
        public static string GetEventDetails(UnityEventBase eventObj)
        {
            var mList = new List<KeyValuePair<object, MethodInfo>>();
            for (var i = 0; i < eventObj.GetPersistentEventCount(); ++i)
            {
                // It's fine to use ? here because GetType works fine on disposed objects and we want to know the type name
                var name = eventObj.GetPersistentMethodName(i);
                var target = eventObj.GetPersistentTarget(i);
                var m = target?.GetType()
                              .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                              .FirstOrDefault(info => info.Name == name);
                if (m != null) mList.Add(new KeyValuePair<object, MethodInfo>(target, m));
            }

            try
            {
                var calls = eventObj.GetPrivateExplicit("m_Calls").GetPrivate("m_RuntimeCalls").CastToEnumerable();
                foreach (var call in calls)
                {
                    if (call.GetPrivate("Delegate") is Delegate d)
                        mList.Add(new KeyValuePair<object, MethodInfo>(d.Target, d.Method));
                }
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
            }

            var sb = new StringBuilder();

            foreach (var kvp in mList)
            {
                sb.Append(kvp.Key.GetType().GetSourceCodeRepresentation());
                // todo make this more powerful somehow, still doesn't show much, maybe with cecil?
                var locals = kvp.Value.GetMethodBody()?.LocalVariables.Select(x => x.ToString());
                if (locals != null) sb.Append(" - " + string.Join("; ", locals.ToArray()));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a string representation of a method call, including the instance, method name, and parameters.
        /// </summary>
        public static string MethodCallToSourceRepresentation(object instance, MethodBase methodInfo, ICollection<string> parameterStrings)
        {
            var generics = methodInfo.GetGenericArgumentsSafe();
            var genericsString = generics.Length == 0 ? "" : "<" + string.Join(", ", generics.Select(x => x.FullDescription()).ToArray()) + ">";
            return $"{instance?.GetType().FullDescription() ?? methodInfo.DeclaringType?.FullDescription() ?? "<Unknown>"}.{methodInfo.Name}{genericsString}({string.Join(", ", Enumerable.ToArray<string>(ClipboardWindow.ResolveMethodParameters(parameterStrings)))})";
        }
    }
}