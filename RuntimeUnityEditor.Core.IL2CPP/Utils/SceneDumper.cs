﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using RuntimeUnityEditor.Core.Utils.Abstractions;
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
            if (!obj) return;

            var pad1 = new string(' ', 3 * d);
            var pad2 = new string(' ', 3 * (d + 1));
            var pad3 = new string(' ', 3 * (d + 2));
            sw.WriteLine(pad1 + obj.name + "--" + obj.GetType().GetSourceCodeRepresentation());

            foreach (var c in obj.GetAllComponentsCasted())
            {
                if(!c) continue; // Sometimes they can be null for some reason

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
            foreach (var t in obj.transform.GetChildren())
                PrintRecursive(sw, t.gameObject, d + 1);
        }
    }
}