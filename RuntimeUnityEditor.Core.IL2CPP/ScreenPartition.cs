namespace RuntimeUnityEditor.Core
{
    /// <summary>
    /// Sections of the screen managed by RUE's window manager.
    /// </summary>
    public enum ScreenPartition
    {
        /// <summary>
        /// Let RUE choose, behavior can change in future versions.
        /// </summary>
        Default = 0,
        /// <summary>
        /// Span the entire screen, minus the taskbar.
        /// </summary>
        Full = 5,
        /// <summary>
        /// Span the center of the screen, top to bottom, minus the taskbar.
        /// </summary>
        Center = 10,
        /// <summary>
        /// Upper half of screen center. It's larger than the lower part.
        /// </summary>
        CenterUpper = 11,
        /// <summary>
        /// Lower half of screen center. It's smaller than the upper part.
        /// </summary>
        CenterLower = 12,
        /// <summary>
        /// Span the left part of the screen, top to bottom, minus the taskbar.
        /// </summary>
        Left = 20,
        /// <summary>
        /// Upper half of the screen's left side. Not necessarily equal in size to the lower half.
        /// </summary>
        LeftUpper = 21,
        /// <summary>
        /// Lower half of the screen's left side. Not necessarily equal in size to the upper half.
        /// </summary>
        LeftLower = 22,
        /// <summary>
        /// Span the right part of the screen, top to bottom, minus the taskbar.
        /// </summary>
        Right = 30,
        /// <summary>
        /// Upper half of the screen's right side. Not necessarily equal in size to the lower half.
        /// </summary>
        RightUpper = 31,
        /// <summary>
        /// Lower half of the screen's right side. Not necessarily equal in size to the upper half.
        /// </summary>
        RightLower = 32,
    }
}
