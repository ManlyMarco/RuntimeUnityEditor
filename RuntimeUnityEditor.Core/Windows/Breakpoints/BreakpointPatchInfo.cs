using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RuntimeUnityEditor.Core.Breakpoints
{
    /// <summary>
    /// Information about a breakpoint patch.
    /// </summary>
    public sealed class BreakpointPatchInfo
    {
        /// <summary>
        /// The method that was patched.
        /// </summary>
        public MethodBase Target { get; }
        /// <summary>
        /// The method that will be called when the breakpoint is hit.
        /// </summary>
        public MethodInfo Patch { get; }
        /// <summary>
        /// The instance filters that will be used to determine if the breakpoint should be hit.
        /// </summary>
        public List<object> InstanceFilters { get; } = new List<object>();

        /// <summary>
        /// Create a new breakpoint patch info.
        /// </summary>
        public BreakpointPatchInfo(MethodBase target, MethodInfo patch, object instanceFilter)
        {
            Target = target;
            Patch = patch;
            if (instanceFilter != null)
                InstanceFilters.Add(instanceFilter);
        }

        private string _toStr, _searchStr;

        internal string GetSearchableString()
        {
            if (_searchStr == null)
                _searchStr = $"{Target.DeclaringType?.FullName}.{Target.Name}\t{string.Join("\t", InstanceFilters.Select(x => x?.ToString()).ToArray())}";
            return _searchStr;
        }
        /// <inheritdoc />
        public override string ToString()
        {
            if (_toStr == null)
                _toStr = $"{Target.DeclaringType?.FullName ?? "???"}.{Target.Name} |Instances> {string.Join(" | ", InstanceFilters.Select(x => x?.ToString() ?? "NULL").ToArray())}";
            return _toStr;
        }
    }
}
