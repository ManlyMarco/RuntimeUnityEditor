using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using RuntimeUnityEditor.Core.Inspector.Entries;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils.Abstractions
{
    /// <summary>
    /// Feature that makes it possible to quicly jump to a given element inside decompiled assembly in dnSpy.
    /// </summary>
    public class DnSpyHelper : IFeature
    {
        private static InitSettings.Setting<string> _dnSpyPath;
        private static InitSettings.Setting<string> _dnSpyArgs;
        void IFeature.OnInitialize(InitSettings initSettings)
        {
            _dnSpyPath = initSettings.RegisterSetting("Inspector", "Path to dnSpy.exe", string.Empty, "Full path to dnSpy that will enable integration with Inspector. When correctly configured, you will see a new ^ buttons that will open the members in dnSpy.");
            _dnSpyPath.ValueChanged += OnPathChanged;
            OnPathChanged(_dnSpyPath.Value);

            _dnSpyArgs = initSettings.RegisterSetting("Inspector", "Optional dnSpy arguments", string.Empty, "Additional parameters that are added to the end of each call to dnSpy.");
        }

        private static void OnPathChanged(string newPath)
        {
            IsAvailable = false;
            if (!string.IsNullOrEmpty(newPath))
            {
                if (File.Exists(newPath) && newPath.EndsWith("dnspy.exe", StringComparison.OrdinalIgnoreCase))
                    IsAvailable = true;
                else
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Error | LogLevel.Message, "[DnSpyHelper] Invalid dnSpy path. The path has to point to 64bit dnSpy.exe");
            }
        }

        /// <summary>
        /// Path to dnSpy.exe
        /// </summary>
        public static string DnSpyPath
        {
            get => _dnSpyPath?.Value ?? string.Empty;
            set => _dnSpyPath.Value = value?.Trim(' ', '"') ?? string.Empty;
        }
        /// <summary>
        /// Arguments to use when launching dnSpy.
        /// </summary>
        public static string DnSpyArgs
        {
            get => _dnSpyArgs?.Value ?? string.Empty;
            set => _dnSpyArgs.Value = value?.Trim() ?? string.Empty;
        }
        /// <summary>
        /// Is dnSpy configured correctly and actually present on disk.
        /// </summary>
        public static bool IsAvailable { get; private set; }

        /// <summary>
        /// Open object contained in a given entry in dnSpy.
        /// </summary>
        public static void OpenInDnSpy(ICacheEntry entry)
        {
            try
            {
                OpenInDnSpy(entry.GetMemberInfo(true));
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error | LogLevel.Message, "[DnSpyHelper] " + e.Message);
            }
        }

        /// <summary>
        /// Navigate to a given type in dnSpy.
        /// </summary>
        public static void OpenInDnSpy(Type type)
        {
            try
            {
                StartDnSpy(GetTypeRefArgs(type));
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error | LogLevel.Message, "[DnSpyHelper] " + e.Message);
            }
        }

        private static string GetTypeRefArgs(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            if (type.ToString().Contains(','))
                throw new Exception("Unsupported type with generic parameters: " + type.FullName);
            var refString = $"\"{type.Assembly.Location}\" --select T:{type.FullName}";
            return refString;
        }

        /// <summary>
        /// Navigate to a given method in dnSpy.
        /// </summary>
        public static void OpenInDnSpy(MemberInfo entry)
        {
            try
            {
                StartDnSpy(GetMemberRefArgs(entry));
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error | LogLevel.Message, "[DnSpyHelper] " + e.Message);
            }
        }

        private static string GetMemberRefArgs(MemberInfo entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var declaringType = entry.DeclaringType;
            if (declaringType == null) throw new ArgumentException("null DeclaringType");
            if (declaringType.FullName == null) throw new ArgumentException("null DeclaringType.FullName");

            // TODO support for generic types
            if (declaringType.FullName.Contains(',')) throw new Exception("Unsupported type with generic parameters: " + declaringType.FullName);
            switch (entry)
            {
                case MethodBase m:
                    var methodNameStr = m.ToString();
                    if (methodNameStr.Contains(',')) throw new Exception("Unsupported method with generic parameters: " + m);
                    return $"\"{declaringType.Assembly.Location}\" --select M:{declaringType.FullName}.{methodNameStr.Split(new[] { ' ' }, 2).Last()}";
                case PropertyInfo p:
                    return $"\"{declaringType.Assembly.Location}\" --select P:{declaringType.FullName}.{p.Name}";
                case FieldInfo f:
                    return $"\"{declaringType.Assembly.Location}\" --select F:{declaringType.FullName}.{f.Name}";
                case EventInfo e:
                    return $"\"{declaringType.Assembly.Location}\" --select E:{declaringType.FullName}.{e.Name}";
                default:
                    throw new Exception("Unknown MemberInfo " + entry.GetType().FullName);
            }
        }

        private static void StartDnSpy(string args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            args = args + " " + DnSpyArgs;
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, $"[DnSpyHelper] Opening {DnSpyPath} {args}");
            Process.Start(DnSpyPath, args);
        }

        bool IFeature.Enabled
        {
            get => IsAvailable;
            set => _ = value;
        }
        void IFeature.OnUpdate() { }
        void IFeature.OnLateUpdate() { }
        void IFeature.OnOnGUI() { }
        void IFeature.OnEditorShownChanged(bool visible) { }
        FeatureDisplayType IFeature.DisplayType => FeatureDisplayType.Hidden;
        string IFeature.DisplayName => "DnSpyHelper";

        private static readonly GUIContent _guiContent = new GUIContent("^", null, "Navigate to this member in dnSpy");
        internal static void DrawDnSpyButtonIfAvailable(MemberInfo mi, GUIContent customButtonContent = null)
        {
            if (IsAvailable && GUILayout.Button(customButtonContent ?? _guiContent, IMGUIUtils.LayoutOptionsExpandWidthFalse))
                OpenInDnSpy(mi);
        }
    }
}