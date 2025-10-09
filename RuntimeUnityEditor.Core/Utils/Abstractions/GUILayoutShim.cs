using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public static class GUILayoutShim
    {
        public static GUILayoutOption MaxWidth(float width)
        {
#if IL2CPP
            var entity = new UnityEngine.GUILayoutOption(GUILayoutOption.Type.maxWidth, width);
            return entity;
#else
            return GUILayout.MaxWidth(width);
#endif
        }

         public static GUILayoutOption ExpandWidth(bool expand)
        {
#if IL2CPP
            var entity = new UnityEngine.GUILayoutOption(GUILayoutOption.Type.stretchWidth, expand);
            return entity;
#else
            return GUILayout.ExpandWidth(expand);
#endif
        }

        public static GUILayoutOption ExpandHeight(bool expand)
        {
#if IL2CPP
            var entity = new UnityEngine.GUILayoutOption(GUILayoutOption.Type.stretchHeight, expand);
            return entity;
#else
            return GUILayout.ExpandWidth(expand);
#endif
        }


        public static GUILayoutOption MinWidth(float width)
        {
#if IL2CPP
            var entity = new UnityEngine.GUILayoutOption(GUILayoutOption.Type.minWidth, width);
            return entity;
#else
            return GUILayout.MinWidth(width);
#endif
        }
    }
}
