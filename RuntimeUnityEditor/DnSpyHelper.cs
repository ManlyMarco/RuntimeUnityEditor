using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;

namespace RuntimeUnityEditor.Core
{
    public static class DnSpyHelper
    {
        private static string _dnSpyPath;
        public static string DnSpyPath
        {
            get { return _dnSpyPath; }
            set
            {
                _dnSpyPath = value?.Trim(' ', '"');

                IsAvailable = false;
                if (!string.IsNullOrEmpty(_dnSpyPath))
                {
                    if (File.Exists(_dnSpyPath) && _dnSpyPath.EndsWith("dnspy.exe", StringComparison.OrdinalIgnoreCase))
                        IsAvailable = true;
                    else
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Error | LogLevel.Message, "[DnSpyHelper] Invalid dnSpy path. The path has to point to 64bit dnSpy.exe");
                }
            }
        }

        private static string _dnSpyArgs;
        public static string DnSpyArgs
        {
            get => _dnSpyArgs;
            set => _dnSpyArgs = value ?? string.Empty;
        }

        public static bool IsAvailable { get; private set; }

        public static void OpenInDnSpy(ICacheEntry entry)
        {
            try { OpenInDnSpy(entry.GetMemberInfo(true)); }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error | LogLevel.Message, "[DnSpyHelper] " + e.Message);
            }
        }

        public static void OpenInDnSpy(Type type)
        {
            try
            {
                if (type == null) throw new ArgumentNullException(nameof(type));

                if (type.ToString().Contains(','))
                    throw new Exception("Unsupported type with generic parameters");
                var refString = $"\"{type.Assembly.Location}\" --select T:{type.FullName}";

                StartDnSpy(refString);
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error | LogLevel.Message, "[DnSpyHelper] " + e.Message);
            }
        }

        public static void OpenInDnSpy(MemberInfo entry)
        {
            string GetDnspyArgs(MemberInfo centry)
            {
                // TODO support for generic types
                switch (centry)
                {
                    case MethodInfo m:
                        if (m.ToString().Contains(',') || m.DeclaringType.FullName.Contains(','))
                            throw new Exception("Unsupported type or method with generic parameters");
                        return $"\"{m.DeclaringType.Assembly.Location}\" --select M:{m.DeclaringType.FullName}.{m.ToString().Split(new[] { ' ' }, 2).Last()}";
                    case PropertyInfo p:
                        if (p.DeclaringType.FullName.Contains(','))
                            throw new Exception("Unsupported type with generic parameters");
                        return $"\"{p.DeclaringType.Assembly.Location}\" --select P:{p.DeclaringType.FullName}.{p.Name}";
                    case FieldInfo f:
                        if (f.DeclaringType.FullName.Contains(','))
                            throw new Exception("Unsupported type with generic parameters");
                        return $"\"{f.DeclaringType.Assembly.Location}\" --select F:{f.DeclaringType.FullName}.{f.Name}";
                    default:
                        throw new Exception("Unknown MemberInfo " + entry.GetType().FullName);
                }
            }

            try
            {
                if (entry == null) throw new ArgumentNullException(nameof(entry));
                var refString = GetDnspyArgs(entry);
                StartDnSpy(refString);
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error | LogLevel.Message, "[DnSpyHelper] " + e.Message);
            }
        }

        private static void StartDnSpy(string refString)
        {
            refString = refString + " " + DnSpyArgs;
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, $"[DnSpyHelper] Opening {DnSpyPath} {refString}");
            Process.Start(DnSpyPath, refString);
        }
    }
}