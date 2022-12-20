using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using RuntimeUnityEditor.Core.Inspector.Entries;

namespace RuntimeUnityEditor.Core.Utils.Abstractions
{
    public class DnSpyHelper : IFeature
    {
        private static Action<string> _confPath;
        private static Action<string> _confArgs;
        void IFeature.OnInitialize(InitSettings initSettings)
        {
            _confPath = initSettings.RegisterSetting("Inspector", "Path to dnSpy.exe", string.Empty, "Full path to dnSpy that will enable integration with Inspector. When correctly configured, you will see a new ^ buttons that will open the members in dnSpy.", x => DnSpyPath = x);
            _confArgs = initSettings.RegisterSetting("Inspector", "Optional dnSpy arguments", string.Empty, "Additional parameters that are added to the end of each call to dnSpy.", x => DnSpyArgs = x);
        }

        private static string _dnSpyPath = string.Empty;
        public static string DnSpyPath
        {
            get => _dnSpyPath;
            set
            {
                var newValue = value?.Trim(' ', '"') ?? string.Empty;

                if (newValue != _dnSpyPath)
                {
                    _dnSpyPath = newValue;

                    IsAvailable = false;
                    if (!string.IsNullOrEmpty(_dnSpyPath))
                    {
                        if (File.Exists(_dnSpyPath) && _dnSpyPath.EndsWith("dnspy.exe", StringComparison.OrdinalIgnoreCase))
                            IsAvailable = true;
                        else
                            RuntimeUnityEditorCore.Logger.Log(LogLevel.Error | LogLevel.Message, "[DnSpyHelper] Invalid dnSpy path. The path has to point to 64bit dnSpy.exe");
                    }

                    _confPath?.Invoke(newValue);
                }
            }
        }

        private static string _dnSpyArgs = string.Empty;
        public static string DnSpyArgs
        {
            get => _dnSpyArgs;
            set
            {
                var newValue = value?.Trim() ?? string.Empty;
                if (newValue != _dnSpyArgs)
                {
                    _dnSpyArgs = newValue;
                    _confArgs?.Invoke(newValue);
                }
            }
        }

        public static bool IsAvailable { get; private set; }

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
                throw new Exception("Unsupported type with generic parameters");
            var refString = $"\"{type.Assembly.Location}\" --select T:{type.FullName}";
            return refString;
        }

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
            switch (entry)
            {
                case MethodInfo m:
                    if (m.ToString().Contains(',') || declaringType.FullName.Contains(','))
                        throw new Exception("Unsupported type or method with generic parameters");
                    return $"\"{declaringType.Assembly.Location}\" --select M:{declaringType.FullName}.{m.ToString().Split(new[] { ' ' }, 2).Last()}";
                case PropertyInfo p:
                    if (declaringType.FullName.Contains(','))
                        throw new Exception("Unsupported type with generic parameters");
                    return $"\"{declaringType.Assembly.Location}\" --select P:{declaringType.FullName}.{p.Name}";
                case FieldInfo f:
                    if (declaringType.FullName.Contains(','))
                        throw new Exception("Unsupported type with generic parameters");
                    return $"\"{declaringType.Assembly.Location}\" --select F:{declaringType.FullName}.{f.Name}";
                case EventInfo e:
                    if (declaringType.FullName.Contains(','))
                        throw new Exception("Unsupported type with generic parameters");
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
    }
}