using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils.Abstractions
{
    /// <summary>
    /// Collection of things required for RUE to be successfully initialized.
    /// </summary>
    public abstract class InitSettings
    {
        #region API

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
        protected abstract Action<T> RegisterSetting<T>(string category, string name, T defaultValue, string description, Action<T> onValueUpdated);
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

        #endregion

        #region Helpers

        /// <summary>
        /// Wrapper for a setting.
        /// </summary>
        public abstract class SettingBase
        {
            private static readonly List<SettingBase> _events;
            private static readonly System.Timers.Timer _timer;
            static SettingBase()
            {
                _events = new List<SettingBase>(2);
                _timer = new System.Timers.Timer(700);
                _timer.AutoReset = false;
                _timer.Stop();
                _timer.Elapsed += (sender, e) =>
                {
                    lock (_events)
                    {
                        foreach (var setting in _events)
                            setting.OnValueChanged();
                        _events.Clear();
                    }
                };
            }

            /// <summary>
            /// Queue a setting to be written back to the config system.
            /// </summary>
            protected static void QueueValueChanged(SettingBase setting)
            {
                lock (_events)
                {
                    if (!_events.Contains(setting))
                        _events.Add(setting);
                    _timer.Stop();
                    _timer.Start();
                }
            }

            /// <summary>
            /// Called when the value of the setting changes.
            /// </summary>
            protected abstract void OnValueChanged();
        }

        /// <inheritdoc />
        public sealed class Setting<T> : SettingBase
        {
            private T _value;
            /// <summary>
            /// Triggered when <see cref="Value"/> changes.
            /// </summary>
            public event Action<T> ValueChanged;

            /// <summary>
            /// Current value of the setting.
            /// </summary>
            public T Value
            {
                get => _value;
                set
                {
                    if (!Equals(_value, value))
                    {
                        _value = value;
                        QueueValueChanged(this);
                    }
                }
            }

            /// <inheritdoc />
            protected override void OnValueChanged()
            {
                ValueChanged?.Invoke(_value);
            }
        }
        /// <inheritdoc cref="RegisterSetting{T}(string,string,T,string,Action{T})" />
        public Setting<T> RegisterSetting<T>(string category, string name, T defaultValue, string description)
        {
            var setting = new Setting<T>();
            var callback = RegisterSetting(category, name, defaultValue, description, obj => setting.Value = obj);
            setting.ValueChanged += callback;
            return setting;
        }

        #endregion
    }
}