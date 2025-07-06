using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public static class GUILayoutShim
    {
        public static GUILayoutOption MaxWidth(float width)
        {
#if IL2CPP
            var entity = GUILayout.ExpandHeight(true);
            entity.type = GUILayoutOption.Type.maxWidth;
            entity.value = width;
            return entity;
#else
            return GUILayout.MaxWidth(width);
#endif
        }

        public static GUILayoutOption MinWidth(float width)
        {
#if IL2CPP
            var entity = GUILayout.ExpandHeight(true);
            entity.type = GUILayoutOption.Type.minWidth;
            entity.value = width;
            return entity;
#else
            return GUILayout.MinWidth(width);
#endif
        }
    }
}