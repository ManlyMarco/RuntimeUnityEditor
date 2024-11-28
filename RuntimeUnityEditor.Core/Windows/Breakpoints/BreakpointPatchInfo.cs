using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RuntimeUnityEditor.Core.Breakpoints
{
    public sealed class BreakpointPatchInfo
    {
        public MethodBase Target { get; }
        public MethodInfo Patch { get; }
        public List<object> InstanceFilters { get; } = new List<object>();

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
        public override string ToString()
        {
            if (_toStr == null)
                _toStr = $"{Target.DeclaringType?.FullName ?? "???"}.{Target.Name} |Instances> {string.Join(" | ", InstanceFilters.Select(x => x?.ToString() ?? "NULL").ToArray())}";
            return _toStr;
        }
    }
}
