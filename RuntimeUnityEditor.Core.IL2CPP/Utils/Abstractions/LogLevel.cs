using System;

namespace RuntimeUnityEditor.Core.Utils.Abstractions
{
    /// <summary>
    /// How a log message should be treated.
    /// A single log message can have multiple levels attached to it (usually Message + something else).
    /// Same as BepInEx5 LogLevel.
    /// </summary>
    [Flags]
    public enum LogLevel
    {
        /// <summary>
        /// This shouldn't happen.
        /// </summary>
        None = 0,
        /// <summary>
        /// The world is burning.
        /// </summary>
        Fatal = 1,
        /// <summary>
        /// Something bad and unexpected happened.
        /// </summary>
        Error = 2,
        /// <summary>
        /// Something bad but expected or safe to ignore happened.
        /// </summary>
        Warning = 4,
        /// <summary>
        /// The user should see this, ideally in the UI.
        /// </summary>
        Message = 8,
        /// <summary>
        /// Writing home about the cool things I did.
        /// </summary>
        Info = 16,
        /// <summary>
        /// Info useful mostly for debugging RUE itself.
        /// </summary>
        Debug = 32,
        /// <summary>
        /// All for one.
        /// </summary>
        All = Debug | Info | Message | Warning | Error | Fatal,
    }
}