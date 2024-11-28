using System;

namespace RuntimeUnityEditor.Core.ChangeHistory
{
    internal class ChangeAction<TObj> : IChange
    {
        public ChangeAction(string actionNameFormat, TObj target, Action<TObj> undoAction)
        {
            if (actionNameFormat == null) throw new ArgumentNullException(nameof(actionNameFormat));
            Target = target;
            _undoAction = undoAction;

            // Check if we need to format the target object.
            // If user included format info like {0:00} then pass the Target object through as it is.
            var targetFormatted = actionNameFormat.Contains("{0}") ? Change.GetTargetDisplayString(Target) : (object)Target;
            _displayString = string.Format(actionNameFormat, targetFormatted);
        }

        public ChangeAction(string actionName, Action undoAction = null)
        {
            _displayString = actionName ?? throw new ArgumentNullException(nameof(actionName));
            if (undoAction != null) _undoAction = _ => undoAction();
        }

        public TObj Target { get; }
        object IChange.Target => Target;

        private readonly Action<TObj> _undoAction;
        public bool CanUndo => _undoAction != null; //todo make this single-shot?
        public void Undo()
        {
            if (!CanUndo) throw new InvalidOperationException("Can't undo this change");

            _undoAction(Target);
        }

        private readonly string _displayString;
        public string GetDisplayString() => _displayString;
        public DateTime ChangeTime { get; } = DateTime.Now;
    }
}