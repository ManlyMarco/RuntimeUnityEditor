using System;

namespace RuntimeUnityEditor.Core.ChangeHistory
{
    /// <summary>
    /// Metadata representing a change that can optionally be undone.
    /// </summary>
    public interface IChange
    {
        /// <summary>
        /// Object whose members were affected by this change.
        /// </summary>
        object Target { get; }
        /// <summary>
        /// Whether this change can be undone.
        /// </summary>
        bool CanUndo { get; }
        /// <summary>
        /// Undo this change. Throws if <see cref="CanUndo"/> is false.
        /// </summary>
        void Undo();
        /// <summary>
        /// How to display this change in the UI.
        /// </summary>
        string GetDisplayString();
        /// <summary>
        /// When this change was made.
        /// </summary>
        DateTime ChangeTime { get; }
    }
}