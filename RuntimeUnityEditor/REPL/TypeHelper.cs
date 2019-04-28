using System;
using System.Reflection;
using System.Text;

namespace RuntimeUnityEditor.Core.REPL
{
    public class TypeHelper
    {
        public object instance;
        public Type type;

        public TypeHelper(Type type)
        {
            this.type = type;
            instance = null;
        }

        public TypeHelper(object instance)
        {
            this.instance = instance;
            type = instance.GetType();
        }

        public T val<T>(string name) where T : class
        {
            var field = type.GetField(name,
                BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic);

            if (field != null)
            {
                if (!field.IsStatic && instance == null)
                    throw new ArgumentException("Field is not static, but instance is missing.");
                return field.GetValue(field.IsStatic ? null : instance) as T;
            }

            var prop = type.GetProperty(name,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                | BindingFlags.Instance);

            if (prop == null || !prop.CanWrite)
                throw new ArgumentException($"No field or settable property of name {name} was found!");

            var getter = prop.GetSetMethod(true);

            if (!getter.IsStatic && instance == null)
                throw new ArgumentException("Property is not static, but instance is missing.");

            return getter.Invoke(getter.IsStatic ? null : instance, null) as T;
        }

        public void set(string name, object value)
        {
            var field = type.GetField(name,
                BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic);

            if (field != null)
            {
                if (!field.IsStatic && instance == null)
                    throw new ArgumentException("Field is not static, but instance is missing.");
                field.SetValue(field.IsStatic ? null : instance, value);
                return;
            }

            var prop = type.GetProperty(name,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                | BindingFlags.Instance);

            if (prop == null || !prop.CanWrite)
                throw new ArgumentException($"No field or settable property of name {name} was found!");

            var setter = prop.GetSetMethod(true);

            if (!setter.IsStatic && instance == null)
                throw new ArgumentException("Property is not static, but instance is missing.");

            setter.Invoke(setter.IsStatic ? null : instance, new[] { value });
        }

        public object invoke(string name, params object[] args)
        {
            var method = type.GetMethod(name,
                BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic);
            if (method == null)
                throw new ArgumentException($"No method of name {name} was found!");
            if (!method.IsStatic && instance == null)
                throw new ArgumentException("Method is not static, but instance is missing.");

            return method.Invoke(method.IsStatic ? null : instance, args);
        }

        public string info()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Info about {type.FullName}");
            sb.AppendLine("Methods");

            foreach (var methodInfo in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                                                       | BindingFlags.Instance))
            {
                bool putComma = false;
                sb.Append(methodInfo.IsPublic ? "public" : "private").Append(" ");
                if (methodInfo.ContainsGenericParameters)
                {
                    sb.Append("<");
                    foreach (var genericArgument in methodInfo.GetGenericArguments())
                    {
                        if (putComma)
                            sb.Append(", ");
                        sb.Append(genericArgument.FullName);
                        putComma = true;
                    }

                    sb.Append(">");
                }

                sb.Append(methodInfo.Name).Append("(");

                putComma = false;
                foreach (var parameterInfo in methodInfo.GetParameters())
                {
                    if (putComma)
                        sb.Append(", ");
                    sb.Append(parameterInfo.ParameterType.FullName);
                    if (parameterInfo.DefaultValue != DBNull.Value)
                        sb.Append($"= {parameterInfo.DefaultValue}");
                    putComma = true;
                }

                sb.AppendLine(")");
            }

            sb.AppendLine().AppendLine("Fields");

            foreach (var fieldInfo in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            )
            {
                sb.Append(fieldInfo.IsPublic ? "public" : "private").Append(" ");
                sb.AppendLine(fieldInfo.Name);
            }

            return sb.ToString();
        }
    }
}