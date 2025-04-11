using System;
using System.Diagnostics;
using System.Linq;

namespace RuntimeUnityEditor.Core.Breakpoints
{
    /// <summary>
    /// Represents a hit on a breakpoint.
    /// </summary>
    public sealed class BreakpointHit
    {
        /// <summary>
        /// Create a new breakpoint hit.
        /// </summary>
        public BreakpointHit(BreakpointPatchInfo origin, object instance, object[] args, object result, StackTrace trace)
        {
            Origin = origin;
            Instance = instance;
            Args = args;
            Result = result;
            Trace = trace;
            TraceString = trace.ToString();
            Time = DateTime.UtcNow;
        }

        /// <summary>
        /// The breakpoint that was hit.
        /// </summary>
        public readonly BreakpointPatchInfo Origin;
        /// <summary>
        /// The instance that was used to call the method.
        /// </summary>
        public readonly object Instance;
        /// <summary>
        /// The arguments that were passed to the method.
        /// </summary>
        public readonly object[] Args;
        /// <summary>
        /// The result of the method call.
        /// </summary>
        public readonly object Result;
        /// <summary>
        /// The stack trace at the time of the hit.
        /// </summary>
        public readonly StackTrace Trace;
        internal readonly string TraceString;
        /// <summary>
        /// The time at which the breakpoint was hit.
        /// </summary>
        public readonly DateTime Time;

        private string _toStr, _searchStr;
        /// <summary>
        /// Returns a string that can be used to search for this breakpoint hit.
        /// </summary>
        public string GetSearchableString()
        {
            if (_searchStr == null)
                _searchStr = $"{Origin.Target.DeclaringType?.FullName}.{Origin.Target.Name}\t{Result}\t{string.Join("\t", Args.Select(x => x?.ToString() ?? "").ToArray())}";
            return _searchStr;
        }
        /// <inheritdoc/>
        public override string ToString()
        {
            if (_toStr == null)
                _toStr = $"{Origin.Target.DeclaringType?.FullName ?? "???"}.{Origin.Target.Name} |Result> {Result?.ToString() ?? "NULL"} |Args> {string.Join(" | ", Args.Select(x => x?.ToString() ?? "NULL").ToArray())}";
            return _toStr;
        }
    }
}
