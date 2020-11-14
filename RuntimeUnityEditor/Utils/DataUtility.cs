// ColorUtility and JsonUtility replacements because 4.x Unity does not have them

using System.Globalization;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils
{
    public static class ColorUtility
    {
        private static bool DoTryParseHtmlColor(string htmlString, out Color32 result)
        {
            result = new Color32(255, 255, 255, 255);
            
            try
            {
                if (htmlString.IndexOf('#') != -1)
                    htmlString = htmlString.Replace("#", "");
                
                var r = byte.Parse(htmlString.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                var g = byte.Parse(htmlString.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                var b = byte.Parse(htmlString.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                var a = byte.Parse(htmlString.Substring(6, 2), NumberStyles.AllowHexSpecifier);
                
                result = new Color32(r, g, b, a);
            }
            catch
            {
                return false;
            }
            
            return true;
        }
        
        public static bool TryParseHtmlString(string htmlString, out Color color)
        {
            var ret = DoTryParseHtmlColor(htmlString, out var c);
            color = c;
            
            return ret;
        }
        
        public static string ToHtmlStringRGBA(Color color)
        {
            var col32 = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * 255), 0, 255));

            return $"{col32.r:X2}{col32.g:X2}{col32.b:X2}{col32.a:X2}";
        }
    }
    
    public static class ToStringUtility
    {
        public static string ObjectToString(Vector2 obj) => $"{obj.x} {obj.y}";
        
        public static string ObjectToString(Vector3 obj) => $"{obj.x} {obj.y} {obj.z}";
        
        public static string ObjectToString(Vector4 obj) => $"{obj.x} {obj.y} {obj.z}, {obj.w}";

        public static string ObjectToString(Quaternion obj) => $"{obj.x} {obj.y} {obj.z}, {obj.w}";
        
        public static Vector2 StringToVector2(string str)
        {
            var array = str.Split(' ');
            return new Vector2(float.Parse(array[0]), float.Parse(array[1]));
        }
        
        public static Vector3 StringToVector3(string str)
        {
            var array = str.Split(' ');
            return new Vector3(float.Parse(array[0]), float.Parse(array[1]), float.Parse(array[2]));
        }
        
        public static Vector4 StringToVector4(string str)
        {
            var array = str.Split(' ');
            return new Vector4(float.Parse(array[0]), float.Parse(array[1]), float.Parse(array[2]), float.Parse(array[3]));
        }
        
        public static Quaternion StringToQuaternion(string str)
        {
            var array = str.Split(' ');
            return new Quaternion(float.Parse(array[0]), float.Parse(array[1]), float.Parse(array[2]), float.Parse(array[3]));
        }
    }
}