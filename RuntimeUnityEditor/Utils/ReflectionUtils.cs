using System.Reflection;

namespace RuntimeUnityEditor.Core.Utils
{
    public static class ReflectionUtils
    {
        public static void SetValue(MemberInfo member, object obj, object value)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    ((FieldInfo) member).SetValue(obj, value);
                    break;
                case MemberTypes.Property:
                    ((PropertyInfo) member).SetValue(obj, value, null);
                    break;
            }
        }
        
        public static object GetValue(MemberInfo member, object obj)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo) member).GetValue(obj);
                case MemberTypes.Property:
                    return ((PropertyInfo) member).GetValue(obj, null);
                default:
                    return null;
            }
        }
    }
}