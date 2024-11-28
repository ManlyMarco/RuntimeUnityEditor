using System;

namespace RuntimeUnityEditor.Core.ChangeHistory
{
    internal class ChangeAssignment<TObj, TVal> : IChange
    {
        public ChangeAssignment(TObj target, TVal newValue, TVal originalValue, Action<TObj, TVal> undoAction, string actionNameFormat)
        {
            if (actionNameFormat == null) throw new ArgumentNullException(nameof(actionNameFormat));

            Target = target;
            NewValue = newValue;
            OriginalValue = originalValue;
            _undoAction = undoAction;
            ActionNameFormat = actionNameFormat;
        }

        public TObj Target { get; }
        object IChange.Target => Target;

        public TVal NewValue { get; private set; }
        public TVal OriginalValue { get; }

        public bool CanUndo => _undoAction != null;
        private readonly Action<TObj, TVal> _undoAction;
        public void Undo()
        {
            if (!CanUndo) throw new InvalidOperationException("Can't undo this change");

            _undoAction(Target, OriginalValue);
        }

        /// <summary>
        /// String format used to get the name of this change.
        /// Has 2 available parameters, the target object {0} and the new value {1}.
        /// </summary>
        internal readonly string ActionNameFormat;
        private string _displayStringCached;
        public string GetDisplayString()
        {
            if (_displayStringCached != null)
                return _displayStringCached;

            // Check if we need to format the target object.
            // If user included format info like {0:00} then pass the Target object through as it is.
            var target = ActionNameFormat.Contains("{0}") ? Change.GetTargetDisplayString(Target) : (object)Target;

            return _displayStringCached = string.Format(ActionNameFormat, target, NewValue);
        }

        public DateTime ChangeTime { get; private set; } = DateTime.Now;

        public void UpdateNewValue(TVal newValue)
        {
            NewValue = newValue;
            _displayStringCached = null;
            ChangeTime = DateTime.Now;
        }
    }
}
