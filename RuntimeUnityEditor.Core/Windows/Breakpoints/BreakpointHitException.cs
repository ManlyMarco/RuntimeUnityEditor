using System;

namespace RuntimeUnityEditor.Core.Breakpoints
{
    internal sealed class BreakpointHitException : Exception
    {
        public BreakpointHitException(string message) : base(message) { }
    }
}
