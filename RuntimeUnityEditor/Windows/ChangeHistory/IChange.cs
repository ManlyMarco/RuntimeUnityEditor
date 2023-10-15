using System;

namespace RuntimeUnityEditor.Core.ChangeHistory
{
    public interface IChange
    {
        object Target { get; }
        bool CanUndo { get; }
        void Undo();
        string GetDisplayString();
        DateTime ChangeTime { get; }
    }
}