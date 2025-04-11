// Based on ObjectDumper 1.0.0.12 by Lasse V. Karlsen
// http://objectdumper.codeplex.com/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RuntimeUnityEditor.Core.Utils.Abstractions;

namespace RuntimeUnityEditor.Core.Utils.ObjectDumper
{
    /// <summary>
    /// Tries to dump contents of objects into a text form.
    /// </summary>
    public static class Dumper
    {
        /// <summary>
        /// Dumps the object to a temporary file and opens it. Requires name of the object.
        /// </summary>
        public static string DumpToTempFile(object value, string name)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (name == null) throw new ArgumentNullException(nameof(name));

            var fname = Path.GetTempFileName() + ".txt";
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, $"Dumping to {fname}");
            using (var f = File.OpenWrite(fname))
            using (var sw = new StreamWriter(f, Encoding.UTF8))
            {
                value.Dump(name, sw);
            }
            var pi = new ProcessStartInfo(fname) { UseShellExecute = true };
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, $"Opening {fname}");
            Process.Start(pi);
            return fname;
        }

        /// <summary>
        /// Dumps the object to a TextWriter. Requires name of the object.
        /// </summary>
        public static void Dump(object value, string name, TextWriter writer)
        {
            if (ObjectDumperExtensions.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            InternalDump(0, name, value, writer, new Dictionary<object, int>(), new List<MemberInfo>(), true);
        }

        private static void InternalDump(int indentationLevel, string name, object value, TextWriter writer, Dictionary<object, int> referenceLookup, List<MemberInfo> structValueLookup, bool recursiveDump)
        {
            var indent = new string(' ', indentationLevel * 3);
            if (value == null)
            {
                writer.WriteLine("{0}{1} = <null>", indent, name);
                return;
            }
            else if (indentationLevel > 2 * 70) // indentation increases by 2 for 1 level
            {
                writer.WriteLine("{0}{1} = (Depth is too deep, possible infinite recursion)", indent, name);
                return;
            }

            var type = value.GetType();
            var existingReference = string.Empty;
            var newReference = string.Empty;
            if (!type.IsValueType)
            {
                if (referenceLookup.TryGetValue(value, out var referenceId))
                {
                    existingReference = string.Format(CultureInfo.InvariantCulture, " (see #{0})", referenceId);
                }
                else
                {
                    referenceId = referenceLookup.Count + 1;
                    referenceLookup[value] = referenceId;
                    newReference = string.Format(CultureInfo.InvariantCulture, "#{0}: ", referenceId);
                }
            }

            var isString = value is string;
            var typeName = value.GetType().GetSourceCodeRepresentation();
            var stringValue = value.ToString();
            if (value is Exception ex)
                stringValue = ex.GetType().Name + ": " + ex.Message;

            if (stringValue == typeName)
            {
                stringValue = string.Empty;
            }
            else
            {
                stringValue = stringValue.Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
                //todo
                //var length = stringValue.Length;
                //if (length > 80) stringValue = stringValue.Substring(0, 80);
                if (isString) stringValue = string.Format(CultureInfo.InvariantCulture, "\"{0}\"", stringValue);
                //if (length > 80)
                //{
                //    object obj = stringValue;
                //    stringValue = string.Concat(obj, " (+", length - 80, " chars)");
                //}

                stringValue = " = " + stringValue;
            }

            writer.WriteLine("{0}{1}{2}{3} [{4}]{5}", indent, newReference, name, stringValue, value.GetType(), existingReference);

            if (existingReference.Length > 0) return;
            if (isString) return;
            if (type.IsValueType && (type.FullName == "System." + type.Name || type.FullName == "UnityEngine." + type.Name)) return;
            if (!recursiveDump) return;

            var instanceProperties = (from property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                      where property.GetIndexParameters().Length == 0 && property.CanRead
                                      select property).ToArray();

            var instanceFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToArray();

            if (instanceProperties.Length == 0 && instanceFields.Length == 0) return;

            writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}{{", indent));
            if (instanceProperties.Length > 0)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}   properties {{", indent));
                foreach (var propertyInfo in instanceProperties)
                {
                    var structValueLookupCopy = structValueLookup;
                    if (type.IsValueType)
                    {
                        if (structValueLookupCopy.Contains(propertyInfo))
                        {
                            writer.WriteLine("{0}{1} = (This member was already called, possible infinite recursion!)", indent + "      ", name);
                            continue;
                        }
                        else
                        {
                            structValueLookupCopy = structValueLookup.AddItem(propertyInfo).ToList();
                        }
                    }

                    try
                    {
                        var propValue = propertyInfo.GetValue(value, null);
                        InternalDump(indentationLevel + 2, propertyInfo.Name, propValue, writer, referenceLookup, structValueLookupCopy, true);
                    }
                    catch (TargetInvocationException invocationException)
                    {
                        InternalDump(indentationLevel + 2, propertyInfo.Name, invocationException, writer, referenceLookup, structValueLookupCopy, false);
                    }
                }

                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}   }}", indent));
            }

            if (instanceFields.Length > 0)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}   fields {{", indent));
                foreach (var fieldInfo in instanceFields)
                {
                    var structValueLookupCopy = structValueLookup;
                    if (type.IsValueType)
                    {
                        if (structValueLookupCopy.Contains(fieldInfo))
                        {
                            writer.WriteLine("{0}{1} = (This member was already called, possible infinite recursion!)", indent + "      ", name);
                            continue;
                        }
                        else
                        {
                            structValueLookupCopy = structValueLookup.AddItem(fieldInfo).ToList();
                        }
                    }

                    try
                    {
                        var fieldValue = fieldInfo.GetValue(value);
                        InternalDump(indentationLevel + 2, fieldInfo.Name, fieldValue, writer, referenceLookup, structValueLookupCopy, true);
                    }
                    catch (TargetInvocationException invocationException)
                    {
                        InternalDump(indentationLevel + 2, fieldInfo.Name, invocationException, writer, referenceLookup, structValueLookupCopy, false);
                    }
                }

                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}   }}", indent));
            }

            if (value is System.Collections.IEnumerable en)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}   IEenumerable {{", indent));
                var index = 0;
                foreach (object obj in en)
                {
                    InternalDump(indentationLevel + 2, $"index={index++:D2}", obj, writer, referenceLookup, structValueLookup, true);
                }

                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}   }}", indent));
            }

            writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}}}", indent));
        }
    }
}
