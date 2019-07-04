using System;
using System.Linq;

namespace RuntimeUnityEditor.Core.Utils
{
    public static class TypeNameExtensions
    {
        public static string GetSourceCodeRepresentation(this Type type)
        {
            var prefixName = string.Empty;
            if (type.DeclaringType != null)
                prefixName = GetSourceCodeRepresentation(type.DeclaringType) + ".";
            else if (!string.IsNullOrEmpty(type.Namespace))
                prefixName = type.Namespace + ".";

            if (type.IsGenericType)
            {
                var genargNames = type.GetGenericArguments().Select(GetSourceCodeRepresentation);
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
    }
}
