using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.Utils.Abstractions;

namespace RuntimeUnityEditor.Core.Utils
{
    public static class TypeNameExtensions
    {
        public static string GetSourceCodeRepresentation(this Type type)
        {
            try
            {
                return GetSourceCodeRepresentationInt(type, new List<Type>());
            }
            catch
            {
                return type.FullName;
            }
        }

        private static string GetSourceCodeRepresentationInt(Type type, List<Type> travesed)
        {
            // Potential infinite recursion
            if (travesed.Count > 20) throw new ArgumentException();

            travesed.Add(type);

            var prefixName = string.Empty;
            if (type.DeclaringType != null)
            {
                if (!travesed.Contains(type.DeclaringType))
                    prefixName = GetSourceCodeRepresentationInt(type.DeclaringType, travesed) + ".";
            }
            else if (!string.IsNullOrEmpty(type.Namespace))
                prefixName = type.Namespace + ".";

            if (type.IsGenericType)
            {
                // Fill the list with nulls to preserve the depth
                var genargNames = type.GetGenericArgumentsSafe().Select(type1 => GetSourceCodeRepresentationInt(type1, new List<Type>(Enumerable.Repeat<Type>(null, travesed.Count))));
                var idx = type.Name.IndexOf('`');
                var typename = idx > 0 ? type.Name.Substring(0, idx) : type.Name;
                return $"{prefixName}{typename}<{string.Join(", ", genargNames.ToArray())}>";
            }

            if (type.IsArray)
            {
                return $"{GetSourceCodeRepresentation(type.GetElementType())}[{new string(Enumerable.Repeat(',', type.GetArrayRank() - 1).ToArray())}]";
            }

            return $"{prefixName}{type.Name}";
        }

        public static string GetFancyDescription(this MemberInfo member)
        {
            switch (member)
            {
                case FieldInfo fieldInfo:
                    return
                        $"{(fieldInfo.IsPublic ? "public " : fieldInfo.IsPrivate ? "private " : fieldInfo.IsFamily ? "protected " : fieldInfo.IsAssembly ? "internal " : "non-public ")}{(fieldInfo.IsStatic ? "static " : "instance ")}{(fieldInfo.IsLiteral ? "const " : fieldInfo.IsInitOnly ? "readonly " : "")}field\n" +
                        $"Name: {fieldInfo.Name}\n" +
                        $"Declared in: {fieldInfo.DeclaringType.FullDescription()}\n" +
                        $"Field type: {fieldInfo.FieldType.FullDescription()}";
                case PropertyInfo propertyInfo:
                    var getter = propertyInfo.GetGetMethod(true);
                    var setter = propertyInfo.GetSetMethod(true);
                    return
                        $"{(getter?.IsPublic ?? setter.IsPublic ? "public " : getter?.IsPrivate ?? setter.IsPrivate ? "private " : getter?.IsFamily ?? setter.IsFamily ? "protected " : getter?.IsAssembly ?? setter.IsAssembly ? "internal " : "non-public ")}{(getter?.IsStatic ?? setter.IsStatic ? "static " : "instance ")}property\n" +
                        $"Name: {propertyInfo.Name}\n" +
                        $"Declared in: {propertyInfo.DeclaringType.FullDescription()}\n" +
                        $"Getter: {(getter != null ? (getter.IsPublic ? "(public) " : "(non-public) ") + getter.FullDescription() : "No getter")}\n" +
                        $"Setter: {(setter != null ? (setter.IsPublic ? "(public) " : "(non-public) ") + setter.FullDescription() : "No setter")}\n" +
                        $"Property type: {propertyInfo.PropertyType.FullDescription()}";
                case MethodInfo methodInfo:
                    return
                        $"{(methodInfo.IsPublic ? "public " : methodInfo.IsPrivate ? "private " : methodInfo.IsFamily ? "protected " : methodInfo.IsAssembly ? "internal " : "non-public ")}{(methodInfo.IsStatic ? "static " : "instance ")}method\n" +
                        $"Name: {methodInfo.Name}\n" +
                        $"Declared in: {methodInfo.DeclaringType.FullDescription()}\n" +
                        $"Generic arguments: {methodInfo.GetGenericArgumentsSafe().Join(p => p.FullDescription(), ", ")}\n" +
                        $"Parameters: {methodInfo.GetParameters().Join(p => p.ParameterType.FullDescription() + " " + p.Name, ", ")}\n" +
                        $"Return type: {methodInfo.ReturnType.FullDescription()}";
                case EventInfo eventInfo:
                    var adder = eventInfo.GetAddMethod(true);
                    var remover = eventInfo.GetRemoveMethod(true);
                    var raiser = eventInfo.GetRaiseMethod(true);
                    return
                        $"{(adder.IsPublic ? "public " : adder.IsPrivate ? "private " : adder.IsFamily ? "protected " : adder.IsAssembly ? "internal " : "non-public ")}{(adder.IsStatic ? "static " : "instance ")}event\n" +
                        $"Name: {eventInfo.Name}\n" +
                        $"Declared in: {eventInfo.DeclaringType.FullDescription()}\n" +
                        $"Add: {(adder.IsPublic ? "(public) " : "(non-public) ")}{adder.FullDescription()}\n" +
                        $"Remove: {(remover != null ? (remover.IsPublic ? "(public) " : "(non-public) ") + remover.FullDescription() : "No remove")}\n" +
                        $"Invoke: {(raiser != null ? (raiser.IsPublic ? "(public) " : "(non-public) ") + raiser.FullDescription() : "No invoke")}\n" +
                        $"Event handler type: {eventInfo.EventHandlerType.FullDescription()}";
                case Type type:
                    return type.FullDescription();

                case null:
                    return null;

                default:
                    Core.RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "Unknown MemberInfo type: " + member.GetType().FullDescription());
                    return null;
            }
        }

        /// <inheritdoc cref="Type.GetGenericArguments"/>
        public static Type[] GetGenericArgumentsSafe(this Type type)
        {
            try
            {
                return type.GetGenericArguments();
            }
            catch (Exception e)
            {
                if (type.IsGenericType || type.IsGenericTypeDefinition)
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "GetGenericArguments failed: " + e);

                return new[] { typeof(FailedToGetGenericArguments) };
            }
        }

        /// <inheritdoc cref="MethodBase.GetGenericArguments"/>
        public static Type[] GetGenericArgumentsSafe(this MethodBase methodInfo)
        {
            try
            {
                return methodInfo.GetGenericArguments();
            }
            catch (Exception e)
            {
                if (methodInfo.IsGenericMethod || methodInfo.IsGenericMethodDefinition)
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "GetGenericArguments failed: " + e);

                return new[] { typeof(FailedToGetGenericArguments) };
            }
        }
    }
}

internal static class FailedToGetGenericArguments { }
