using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RuntimeUnityEditor.Core.Utils
{
    /// <summary>
    /// Utility methods for working with embedded resources.
    /// </summary>
    public static class ResourceUtils
    {
        /// <summary>
        /// Read all bytes starting at current position and ending at the end of the stream.
        /// </summary>
        public static byte[] ReadAllBytes(this Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    ms.Write(buffer, 0, read);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Get a file set as "Embedded Resource" from the assembly that is calling this code, or optionally from a specified assembly.
        /// The filename is matched to the end of the resource path, no need to give the full path.
        /// If 0 or more than 1 resources match the provided filename, an exception is thrown.
        /// For example if you have a file "ProjectRoot\Resources\icon.png" set as "Embedded Resource", you can use this to load it by
        /// doing <code>GetEmbeddedResource("icon.png"), assuming that no other embedded files have the same name.</code>
        /// </summary>
        public static byte[] GetEmbeddedResource(string resourceFileName, Assembly containingAssembly = null)
        {
            if (containingAssembly == null)
                containingAssembly = Assembly.GetCallingAssembly();

            var resourceName = containingAssembly.GetManifestResourceNames().Single(str => str.EndsWith(resourceFileName));

            using (var stream = containingAssembly.GetManifestResourceStream(resourceName))
                return ReadAllBytes(stream ?? throw new InvalidOperationException($"The resource {resourceFileName} was not found"));
        }
    }
}