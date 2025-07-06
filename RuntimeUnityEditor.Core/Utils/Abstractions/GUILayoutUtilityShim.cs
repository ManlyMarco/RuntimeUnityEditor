using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public static class GUILayoutUtilityShim
    {
        public static Rect GetLastRect()
        {
#if IL2CPP
            // https://github.com/Unity-Technologies/UnityCsReference/blob/4b463aa72c78ec7490b7f03176bd012399881768/Modules/IMGUI/GUILayoutUtility.cs#L517-L527
            switch (Event.current.type)
            {
                case EventType.Layout:
                case EventType.Used:
                    return new Rect(0,0,1,1);
                default:
                    return GUILayoutUtility.current.topLevel.GetLastShim();
            }
#else
            return GUILayoutUtility.GetLastRect();
#endif
        }
    }
    public static class GUILayoutGroupShim
    {
#if IL2CPP
        // https://github.com/Unity-Technologies/UnityCsReference/blob/4b463aa72c78ec7490b7f03176bd012399881768/Modules/IMGUI/LayoutGroup.cs#L136-L154
        public static UnityEngine.Rect GetLastShim(this GUILayoutGroup group)
        {
            if (group.m_Cursor == 0)
            {
                if (Event.current.type == EventType.Repaint)
                    Debug.LogError("You cannot call GetLast immediately after beginning a group.");
                return new Rect(0,0,1,1);
            }

            if (group.m_Cursor <= group.entries.Count)
            {
                GUILayoutEntry e = (GUILayoutEntry)group.entries[group.m_Cursor - 1];
                return e.rect;
            }

            if (Event.current.type == EventType.Repaint)
                Debug.LogError("Getting control " + group.m_Cursor + "'s position in a group with only " + group.entries.Count + " controls when doing " + Event.current.rawType);
            return new Rect(0,0,1,1);
        }
#endif
    }
}