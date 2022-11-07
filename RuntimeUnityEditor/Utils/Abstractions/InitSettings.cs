using System;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils.Abstractions
{
    public abstract class InitSettings
    {
        /// <summary>
        /// Register a new persistent setting.
        /// </summary>
        /// <typeparam name="T">Type of the setting</typeparam>
        /// <param name="category">Used for grouping</param>
        /// <param name="name">Name/Key</param>
        /// <param name="defaultValue">Initial value if setting was never changed</param>
        /// <param name="description">What the setting does</param>
        /// <param name="onValueUpdated">Called when the setting changes, and immediately after this method finishes (with either the default value or the previously stored value).</param>
        /// <returns>An Action that can be used to set the setting's value</returns>
        public abstract Action<T> RegisterSetting<T>(string category, string name, T defaultValue, string description, Action<T> onValueUpdated);
        /// <summary>
        /// Instance MB of the plugin
        /// </summary>
        public abstract MonoBehaviour PluginMonoBehaviour { get; }
        /// <summary>
        /// Log output
        /// </summary>
        public abstract ILoggerWrapper LoggerWrapper { get; }
        /// <summary>
        /// Path to write/read extra config files from
        /// </summary>
        public abstract string ConfigPath { get; }
    }
}