using System;
using System.Diagnostics;
using System.Linq;

namespace RuntimeUnityEditor.Core.Breakpoints
{
    public sealed class BreakpointHit
    {
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

        public readonly BreakpointPatchInfo Origin;
        public readonly object Instance;
        public readonly object[] Args;
        public readonly object Result;
        public readonly StackTrace Trace;
        internal readonly string TraceString;
        public readonly DateTime Time;

        private string _toStr, _searchStr;
        public string GetSearchableString()
        {
            if (_searchStr == null)
                _searchStr = $"{Origin.Target.DeclaringType?.FullName}.{Origin.Target.Name}\t{Result}\t{string.Join("\t", Args.Select(x => x?.ToString() ?? "").ToArray())}";
            return _searchStr;
        }
        public override string ToString()
        {
            if (_toStr == null)
                _toStr = $"{Origin.Target.DeclaringType?.FullName ?? "???"}.{Origin.Target.Name} |Result> {Result?.ToString() ?? "NULL"} |Args> {string.Join(" | ", Args.Select(x => x?.ToString() ?? "NULL").ToArray())}";
            return _toStr;
        }
    }
}
