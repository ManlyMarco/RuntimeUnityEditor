using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils
{
    internal static class SceneDumper
    {
        public static void DumpObjects(params GameObject[] objects)
        {
            var fname = Path.GetTempFileName() + ".txt";
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, $"Dumping {objects.Length} GameObjects to {fname}");
            using (var f = File.OpenWrite(fname))
            using (var sw = new StreamWriter(f, Encoding.UTF8))
            {
                foreach (var obj in objects)
                    PrintRecursive(sw, obj);
            }
            var pi = new ProcessStartInfo(fname) { UseShellExecute = true };
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, $"Opening {fname}");
            Process.Start(pi);
        }

        private static void PrintRecursive(TextWriter sw, GameObject obj, int d = 0)
        {
            if (obj == null) return;

            var pad1 = new string(' ', 3 * d);
            var pad2 = new string(' ', 3 * (d + 1));
            var pad3 = new string(' ', 3 * (d + 2));
            sw.WriteLine(pad1 + obj.name + "--" + obj.GetType().FullName);

            foreach (var c in obj.GetComponents<Component>())
            {
                sw.WriteLine(pad2 + "::" + c.GetType().Name);

                var ct = c.GetType();
                var props = ct.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
                foreach (var p in props)
                {
                    try
                    {
                        var v = p.GetValue(c, null);
                        sw.WriteLine(pad3 + "@" + p.Name + "<" + p.PropertyType.Name + "> = " + v);
                    }
                    catch (Exception e)
                    {
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, e);
                    }
                }
            }
            foreach (Transform t in obj.transform)
                PrintRecursive(sw, t.gameObject, d + 1);
        }
    }
}
