namespace RuntimeUnityEditor.Core.Breakpoints
{
    /// <summary>
    /// What debugger-related action should happen when a breakpoint is hit.
    /// </summary>
    public enum DebuggerBreakType
    {
        /// <summary>
        /// Do nothing.
        /// </summary>
        None = 0,
        /// <summary>
        /// Call Debugger.Break.
        /// </summary>
        DebuggerBreak,
        /// <summary>
        /// Throw <see cref="BreakpointHitException"/>.
        /// </summary>
        ThrowCatch
    }
}
