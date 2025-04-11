using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace RuntimeUnityEditor.Core.Breakpoints
{
    /// <summary>
    /// Class for managing breakpoints in methods.
    /// </summary>
    public static class Breakpoints
    {
        private static readonly Harmony _harmony = new Harmony("RuntimeUnityEditor.Core.Breakpoints");
        private static readonly HarmonyMethod _handlerMethodRet = new HarmonyMethod(typeof(Hooks), nameof(Hooks.BreakpointHandlerReturn));
        private static readonly HarmonyMethod _handlerMethodNoRet = new HarmonyMethod(typeof(Hooks), nameof(Hooks.BreakpointHandlerNoReturn));
        private static readonly Dictionary<MethodBase, BreakpointPatchInfo> _appliedPatches = new Dictionary<MethodBase, BreakpointPatchInfo>();
        /// <summary>
        /// A collection of all applied patches.
        /// </summary>
        public static ICollection<BreakpointPatchInfo> AppliedPatches => _appliedPatches.Values;

        /// <summary>
        /// Whether all breakpoints are enabled or not.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// What to do when a breakpoint is hit.
        /// </summary>
        public static DebuggerBreakType DebuggerBreaking { get; set; }

        /// <summary>
        /// Event that is called when a breakpoint is hit.
        /// </summary>
        public static event Action<BreakpointHit> OnBreakpointHit;

        /// <summary>
        /// Attaches a breakpoint to a method.
        /// </summary>
        /// <param name="target">Method to attach to</param>
        /// <param name="instance">Only trigger when method is called on this instance. If null then break on all calls.</param>
        /// <returns>True if patch was applied successfully</returns>
        public static bool AttachBreakpoint(MethodBase target, object instance)
        {
            if (_appliedPatches.TryGetValue(target, out var pi))
            {
                if (instance != null)
                    pi.InstanceFilters.Add(instance);
                else
                    pi.InstanceFilters.Clear();
                return true;
            }

            var hasReturn = target is MethodInfo mi && mi.ReturnType != typeof(void);
            var patch = _harmony.Patch(target, postfix: hasReturn ? _handlerMethodRet : _handlerMethodNoRet);
            if (patch != null)
            {
                _appliedPatches[target] = new BreakpointPatchInfo(target, patch, instance);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Detaches a breakpoint from a method.
        /// </summary>
        /// <param name="target">Method to detach from</param>
        /// <param name="instance">Only remove this instance filter. If null, completely remove this breakpoint.</param>
        /// <returns>True if the breakpoint was completely removed, false otherwise.</returns>
        public static bool DetachBreakpoint(MethodBase target, object instance)
        {
            if (_appliedPatches.TryGetValue(target, out var pi))
            {
                if (instance == null)
                    pi.InstanceFilters.Clear();
                else
                    pi.InstanceFilters.Remove(instance);

                if (pi.InstanceFilters.Count == 0)
                {
                    _harmony.Unpatch(target, pi.Patch);
                    _appliedPatches.Remove(target);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a breakpoint is attached to a method.
        /// </summary>
        public static bool IsAttached(MethodBase target, object instance)
        {
            if (_appliedPatches.TryGetValue(target, out var pi))
            {
                return instance == null && pi.InstanceFilters.Count == 0 || pi.InstanceFilters.Contains(instance);
            }

            return false;
        }

        /// <summary>
        /// Detaches all breakpoints.
        /// </summary>
        public static void DetachAll()
        {
            _harmony.UnpatchSelf();
            _appliedPatches.Clear();
        }

        private static void AddHit(object __instance, MethodBase __originalMethod, object[] __args, object __result)
        {
            if (!Enabled) return;

            if (!_appliedPatches.TryGetValue(__originalMethod, out var pi)) return;

            if (pi.InstanceFilters.Count > 0 && !pi.InstanceFilters.Contains(__instance)) return;

            if (DebuggerBreaking == DebuggerBreakType.ThrowCatch)
            {
                try { throw new BreakpointHitException(pi.Target.Name); }
                catch (BreakpointHitException) { }
            }
            else if (DebuggerBreaking == DebuggerBreakType.DebuggerBreak)
            {
                Debugger.Break();
            }

            OnBreakpointHit?.Invoke(new BreakpointHit(pi, __instance, __args, __result, new StackTrace(2, true)));
        }

        private static class Hooks
        {
            public static void BreakpointHandlerReturn(object __instance, MethodBase __originalMethod, object[] __args, object __result)
            {
                AddHit(__instance, __originalMethod, __args, __result);
            }

            public static void BreakpointHandlerNoReturn(object __instance, MethodBase __originalMethod, object[] __args)
            {
                AddHit(__instance, __originalMethod, __args, null);
            }
        }
    }
}
