using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    /// <summary>
    /// Provides a shim for GUILayout methods that are often missing in IL2CPP.
    /// </summary>
    public static class GUILayoutShim
    {
        /// <inheritdoc cref="GUILayout.MaxWidth"/>
        public static GUILayoutOption MaxWidth(float width)
        {
#if IL2CPP
            var entity = new UnityEngine.GUILayoutOption(GUILayoutOption.Type.maxWidth, width);
            return entity;
#else
            return GUILayout.MaxWidth(width);
#endif
        }

        /// <inheritdoc cref="GUILayout.ExpandWidth"/>
        public static GUILayoutOption ExpandWidth(bool expand)
        {
#if IL2CPP
            var entity = new UnityEngine.GUILayoutOption(GUILayoutOption.Type.stretchWidth, !expand ? 0 : 1);
            return entity;
#else
            return GUILayout.ExpandWidth(expand);
#endif
        }

        /// <inheritdoc cref="GUILayout.ExpandHeight"/>
        public static GUILayoutOption ExpandHeight(bool expand)
        {
#if IL2CPP
            var entity = new UnityEngine.GUILayoutOption(GUILayoutOption.Type.stretchHeight, !expand ? 0 : 1);
            return entity;
#else
            return GUILayout.ExpandHeight(expand);
#endif
        }


        /// <inheritdoc cref="GUILayout.MinWidth"/>
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
