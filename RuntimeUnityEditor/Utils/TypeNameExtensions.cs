using System;
using System.Collections.Generic;
using System.Linq;

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
                var genargNames = type.GetGenericArguments().Select(type1 => GetSourceCodeRepresentationInt(type1, new List<Type>(Enumerable.Repeat<Type>(null, travesed.Count))));
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
