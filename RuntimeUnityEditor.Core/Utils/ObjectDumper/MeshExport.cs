using System;
using System.IO;
using System.Linq;
using System.Text;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils.ObjectDumper
{
    /// <summary>
    /// Contains methods for exporting the renderer data in various formats
    /// https://github.com/IllusionMods/KK_Plugins/blob/master/src/MaterialEditor.Base/Export.Obj.cs
    /// </summary>
    internal static class MeshExport
    {
        public static bool CanExport(Renderer rend)
        {
            return rend is MeshRenderer || rend is SkinnedMeshRenderer;
        }

        /// <summary>
        /// Exports the mesh of the SkinnedMeshRenderer or MeshRenderer
        /// </summary>
        public static bool ExportObj(Renderer rend, bool bakedMesh, bool bakedWorldPosition)
        {
            try
            {
                if (rend == null) throw new ArgumentNullException(nameof(rend));

                var filename = OpenFileDialog.ShowDialog("Export mesh...", rend.name + "_export" + (bakedMesh ? (bakedWorldPosition ? "_baked_worldpos" : "_baked") : "") + ".obj", ".obj file|*.obj", ".obj",
                                                         OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER | OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES |
                                                         OpenFileDialog.OpenSaveFileDialgueFlags.OFN_OVERWRITEPROMPT).FirstOrDefault();

                if (filename != null)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, "Exporting mesh to: " + filename);

                    using (var sw = new StreamWriter(filename, false))
                    {
                        var mesh = MeshToObjString(rend, bakedMesh, bakedWorldPosition);

                        sw.Write(mesh);
                        //Utilities.OpenFileInExplorer(filename);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, "Could not export: " + ex.Message);
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, ex);
            }

            return false;
        }

        private static Mesh GetMeshFromRenderer(Renderer rend, bool baked)
        {
            switch (rend)
            {
                case MeshRenderer meshRenderer:
                    return meshRenderer.GetComponent<MeshFilter>().mesh;
                case SkinnedMeshRenderer skinnedMeshRenderer:
                    return baked ? BakeMesh(skinnedMeshRenderer) : skinnedMeshRenderer.sharedMesh;
                default:
                    throw new ArgumentException("Unsupported Renderer type: " + rend.GetType().FullName);
            }
        }

        private static Mesh BakeMesh(SkinnedMeshRenderer mesh)
        {
            var bakedMesh = new Mesh();
            mesh.BakeMesh(bakedMesh);
            return bakedMesh;
        }

        private static string NameFormatted(this Renderer go) => go == null ? "" : go.name.Replace("(Instance)", "").Replace(" Instance", "").Trim();
            
        private static string MeshToObjString(Renderer rend, bool bakedMesh, bool bakedWorldPosition)
        {
            if (rend == null) throw new ArgumentNullException(nameof(rend));

            var mesh = GetMeshFromRenderer(rend, bakedMesh);
            var sb = new StringBuilder();
            var hasData = false;
            
            hasData |= AppendVertices(sb, mesh, rend, bakedMesh && bakedWorldPosition);
            hasData |= AppendTextureCoordinates(sb, mesh);
            hasData |= AppendNormals(sb, mesh, rend, bakedMesh && bakedWorldPosition);
            hasData |= AppendFaces(sb, mesh, rend);

            if(!hasData)
                throw new InvalidOperationException("No mesh data found or mesh is set as not readable");
            return sb.ToString();
        }

        /// <summary>
        /// Appends the vertex data of a mesh to a StringBuilder in a specific format.
        /// </summary>
        /// <param name="builder">The StringBuilder object to append the vertex data to.</param>
        /// <param name="mesh">The Mesh object containing the vertex data to export.</param>
        /// <param name="renderer">The Renderer associated with the mesh, used for transformations.</param>
        /// <param name="baked">Indicates whether the mesh is baked.</param>
        /// <returns>Returns true if vertex data was successfully appended, otherwise false.</returns>
        private static bool AppendVertices(StringBuilder builder, Mesh mesh, Renderer renderer, bool baked)
        {
            if (mesh.vertices.Length == 0)
                return false;

            foreach (var v in mesh.vertices)
            {
                var transformedVertex = baked ? renderer.transform.TransformPoint(v) : v;
                builder.AppendLine($"v {-transformedVertex.x} {transformedVertex.y} {transformedVertex.z}");
            }

            return true;
        }

        /// <summary>
        /// Appends the texture coordinates (UV mapping) of a mesh to the provided StringBuilder.
        /// </summary>
        /// <param name="builder">The StringBuilder instance to append the texture coordinates to.</param>
        /// <param name="mesh">The mesh whose texture coordinates will be appended.</param>
        /// <returns>True if the mesh has texture coordinates; otherwise, false.</returns>
        private static bool AppendTextureCoordinates(StringBuilder builder, Mesh mesh)
        {
            if (mesh.uv.Length == 0)
                return false;

            foreach (var uv in mesh.uv)
            {
                builder.AppendLine($"vt {uv.x} {uv.y}");
            }

            return true;
        }

        /// <summary>
        /// Appends vertex normal data from a mesh to the provided StringBuilder in the Wavefront .obj format.
        /// </summary>
        /// <param name="builder">The StringBuilder to which the normal data will be appended.</param>
        /// <param name="mesh">The mesh whose normals will be processed and appended.</param>
        /// <param name="renderer">The renderer associated with the mesh, used for transforming normals if needed.</param>
        /// <param name="baked">Indicates if the mesh should be treated as baked.</param>
        /// <returns>True if normals are successfully appended; false if the mesh contains no normals.</returns>
        private static bool AppendNormals(StringBuilder builder, Mesh mesh, Renderer renderer, bool baked)
        {
            if (mesh.normals.Length == 0)
                return false;

            foreach (var normal in mesh.normals)
            {
                var transformedNormal = baked ? renderer.transform.TransformDirection(normal) : normal;
                builder.AppendLine($"vn {-transformedNormal.x} {transformedNormal.y} {transformedNormal.z}");
            }

            return true;
        }

        /// <summary>
        /// Appends face data from a mesh to the specified StringBuilder in the Wavefront OBJ format.
        /// </summary>
        /// <param name="builder">The StringBuilder to which the face data will be appended.</param>
        /// <param name="mesh">The mesh containing face data to be exported.</param>
        /// <param name="renderer">The Renderer associated with the mesh, used for naming and formatting purposes.</param>
        /// <returns>Returns true if the mesh contains sub-mesh data and face data was successfully appended; otherwise, false.</returns>
        private static bool AppendFaces(StringBuilder builder, Mesh mesh, Renderer renderer)
        {
            if (mesh.subMeshCount == 0) return false;
    
            for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                builder.AppendLine($"g {renderer.NameFormatted()}_{subMeshIndex}");
                var triangles = mesh.GetTriangles(subMeshIndex);
        
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var v1 = triangles[i] + 1;
                    var v2 = triangles[i + 2] + 1;
                    var v3 = triangles[i + 1] + 1;
                    builder.AppendLine($"f {v1}/{v1}/{v1} {v2}/{v2}/{v2} {v3}/{v3}/{v3}");
                }
            }
            return true;
        }
    }
}
