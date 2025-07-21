using System;
using System.Collections.Generic;
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
        /// <param name="bakedMesh">Indicates whether the mesh is baked.</param>
        /// <param name="bakedWorldPosition">Indicates whether to apply world position transformations to the vertices.</param>
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
        /// <param name="bakedMesh">Indicates if the mesh should be treated as baked.</param>
        /// <param name="bakedWorldPosition">Indicates if the normals should be transformed to world position when the mesh is baked.</param>
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


        // Should be possible to be safely removed, I'll leave it here for now as a fallback in case something goes wrong
        private static class MeshExtensions
        {
            public static Mesh Submesh(Mesh mesh, int submeshIndex)
            {
                if (submeshIndex < 0 || submeshIndex >= mesh.subMeshCount)
                    return null;
                var indices = mesh.GetTriangles(submeshIndex);
                var source = new Vertices(mesh);
                var dest = new Vertices();
                var map = new Dictionary<int, int>();
                var newIndices = new int[indices.Length];
                for (var i = 0; i < indices.Length; i++)
                {
                    var o = indices[i];
                    if (!map.TryGetValue(o, out var n))
                    {
                        n = dest.Add(source, o);
                        map.Add(o, n);
                    }

                    newIndices[i] = n;
                }

                var submesh = new Mesh();
                dest.AssignTo(submesh);
                submesh.triangles = newIndices;
                submesh.name = $"{mesh.name.Replace("(Instance)", "").Replace(" Instance", "").Trim()}_{submeshIndex}";
                return submesh;
            }

            private class Vertices
            {
#if IL2CPP
                private Il2CppSystem.Collections.Generic.List<Vector3> verts;
                private Il2CppSystem.Collections.Generic.List<Vector2> uv1;
                private Il2CppSystem.Collections.Generic.List<Vector2> uv2;
                private Il2CppSystem.Collections.Generic.List<Vector2> uv3;
                private Il2CppSystem.Collections.Generic.List<Vector2> uv4;
                private Il2CppSystem.Collections.Generic.List<Vector3> normals;
                private Il2CppSystem.Collections.Generic.List<Vector4> tangents;
                private Il2CppSystem.Collections.Generic.List<Color32> colors;
                private Il2CppSystem.Collections.Generic.List<BoneWeight> boneWeights;

                public Vertices() => verts = new Il2CppSystem.Collections.Generic.List<Vector3>();

                public Vertices(Mesh mesh)
                {
                    verts = mesh.vertices == null || mesh.vertices.Length == 0 ? null : new Il2CppSystem.Collections.Generic.List<Vector3>(mesh.vertices.Pointer);

                    uv1 = mesh.uv == null || mesh.uv.Length == 0 ? null : new Il2CppSystem.Collections.Generic.List<Vector2>(mesh.uv.Pointer);
                    uv2 = mesh.uv2 == null || mesh.uv2.Length == 0 ? null : new Il2CppSystem.Collections.Generic.List<Vector2>(mesh.uv2.Pointer);
                    uv3 = mesh.uv3 == null || mesh.uv3.Length == 0 ? null : new Il2CppSystem.Collections.Generic.List<Vector2>(mesh.uv3.Pointer);
                    uv4 = mesh.uv4 == null || mesh.uv4.Length == 0 ? null : new Il2CppSystem.Collections.Generic.List<Vector2>(mesh.uv4.Pointer);

                    normals = mesh.normals == null || mesh.normals.Length == 0 ? null : new Il2CppSystem.Collections.Generic.List<Vector3>(mesh.normals.Pointer);
                    tangents = mesh.tangents == null || mesh.tangents.Length == 0 ? null : new Il2CppSystem.Collections.Generic.List<Vector4>(mesh.tangents.Pointer);
                    colors = mesh.colors32 == null || mesh.colors32.Length == 0 ? null : new Il2CppSystem.Collections.Generic.List<Color32>(mesh.colors32.Pointer);
                    boneWeights = mesh.boneWeights == null || mesh.boneWeights.Length == 0 ? null : new Il2CppSystem.Collections.Generic.List<BoneWeight>(mesh.boneWeights.Pointer);
                }

                private static void Copy<T>(ref Il2CppSystem.Collections.Generic.List<T> dest, Il2CppSystem.Collections.Generic.List<T> source, int index)
                {
                    if (source == null)
                        return;
                    if (dest == null)
                        dest = new Il2CppSystem.Collections.Generic.List<T>();

                    dest.Add(source._items[index]);
                }

                public int Add(Vertices other, int index)
                {
                    var i = verts.Count;
                    Copy(ref verts, other.verts, index);
                    Copy(ref uv1, other.uv1, index);
                    Copy(ref uv2, other.uv2, index);
                    Copy(ref uv3, other.uv3, index);
                    Copy(ref uv4, other.uv4, index);
                    Copy(ref normals, other.normals, index);
                    Copy(ref tangents, other.tangents, index);
                    Copy(ref colors, other.colors, index);
                    Copy(ref boneWeights, other.boneWeights, index);
                    return i;
                }

                public void AssignTo(Mesh target)
                {
                    target.SetVertices(verts);
                    if (uv1 != null) target.SetUVs(0, uv1);
                    if (uv2 != null) target.SetUVs(1, uv2);
                    if (uv3 != null) target.SetUVs(2, uv3);
                    if (uv4 != null) target.SetUVs(3, uv4);
                    if (normals != null) target.SetNormals(normals);
                    if (tangents != null) target.SetTangents(tangents);
                    if (colors != null) target.SetColors(colors);
                    if (boneWeights != null) target.boneWeights = ( Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<BoneWeight>)boneWeights.ToArray();
                }
#else
                private List<Vector3> verts;
                private List<Vector2> uv1;
                private List<Vector2> uv2;
                private List<Vector2> uv3;
                private List<Vector2> uv4;
                private List<Vector3> normals;
                private List<Vector4> tangents;
                private List<Color32> colors;
                private List<BoneWeight> boneWeights;

                public Vertices() => verts = new List<Vector3>();

                public Vertices(Mesh mesh)
                {
                    verts = CreateList(mesh.vertices);
                    uv1 = CreateList(mesh.uv);
                    uv2 = CreateList(mesh.uv2);
                    uv3 = CreateList(mesh.uv3);
                    uv4 = CreateList(mesh.uv4);
                    normals = CreateList(mesh.normals);
                    tangents = CreateList(mesh.tangents);
                    colors = CreateList(mesh.colors32);
                    boneWeights = CreateList(mesh.boneWeights);
                }

                private static List<T> CreateList<T>(T[] source)
                {
                    if (source == null || source.Length == 0)
                        return null;
                    return new List<T>(source);
                }

                private static void Copy<T>(ref List<T> dest, List<T> source, int index)
                {
                    if (source == null)
                        return;
                    if (dest == null)
                        dest = new List<T>();
                    dest.Add(source[index]);
                }

                public int Add(Vertices other, int index)
                {
                    var i = verts.Count;
                    Copy(ref verts, other.verts, index);
                    Copy(ref uv1, other.uv1, index);
                    Copy(ref uv2, other.uv2, index);
                    Copy(ref uv3, other.uv3, index);
                    Copy(ref uv4, other.uv4, index);
                    Copy(ref normals, other.normals, index);
                    Copy(ref tangents, other.tangents, index);
                    Copy(ref colors, other.colors, index);
                    Copy(ref boneWeights, other.boneWeights, index);
                    return i;
                }

                public void AssignTo(Mesh target)
                {
                    target.SetVertices(verts);
                    if (uv1 != null) target.SetUVs(0, uv1);
                    if (uv2 != null) target.SetUVs(1, uv2);
                    if (uv3 != null) target.SetUVs(2, uv3);
                    if (uv4 != null) target.SetUVs(3, uv4);
                    if (normals != null) target.SetNormals(normals);
                    if (tangents != null) target.SetTangents(tangents);
                    if (colors != null) target.SetColors(colors);
                    if (boneWeights != null) target.boneWeights = boneWeights.ToArray();
                }
#endif
            }
        }
    }
}
