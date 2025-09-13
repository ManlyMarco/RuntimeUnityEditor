using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.CSharp;
using RuntimeUnityEditor.Core.Utils.Abstractions;

namespace RuntimeUnityEditor.Core.REPL.MCS
{
    internal class ScriptEvaluator : Evaluator, IDisposable
    {
        private static readonly HashSet<string> StdLib =
                new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) {"mscorlib", "System.Core", "System", "System.Xml"};

        private readonly TextWriter _logger;

        public ScriptEvaluator(TextWriter logger) : base(BuildContext(logger))
        {
            _logger = logger;

            ImportAppdomainAssemblies(ReferenceAssembly);            
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            _logger.Dispose();
        }

        private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            string name = args.LoadedAssembly.GetName().Name;
            if (StdLib.Contains(name))
                return;
            ReferenceAssembly(args.LoadedAssembly);
        }

        private static CompilerContext BuildContext(TextWriter tw)
        {
            var reporter = new StreamReportPrinter(tw);

            var settings = new CompilerSettings
            {
                    Version = LanguageVersion.Experimental,
                    GenerateDebugInfo = false,
                    StdLib = true,
                    Target = Target.Library,
                    WarningLevel = 0,
                    EnhancedWarnings = false
            };

            return new CompilerContext(settings, reporter);
        }

        private static void ImportAppdomainAssemblies(Action<Assembly> import)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName();
                var shortName = assemblyName.Name;
                if (StdLib.Contains(shortName))
                    continue;
                try
                {
                    import(assembly);
                }
                catch (Exception e)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, $"[REPL] Failed to import [{assemblyName.FullName}]: {e.Message}");
                }
            }
        }
    }
}