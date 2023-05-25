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

        private static string MeshToObjString(Renderer rend, bool bakedMesh, bool bakedWorldPosition)
        {
            if (rend == null) throw new ArgumentNullException(nameof(rend));

            Mesh mesh;
            if (rend is MeshRenderer meshRenderer)
                mesh = meshRenderer.GetComponent<MeshFilter>().mesh;
            else if (rend is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                if (bakedMesh)
                {
                    mesh = new Mesh();
                    skinnedMeshRenderer.BakeMesh(mesh);
                }
                else
                {
                    mesh = skinnedMeshRenderer.sharedMesh;
                }
            }
            else throw new ArgumentException("Unsupported Renderer type: " + rend.GetType().FullName);

            var scale = rend.transform.lossyScale;
            var inverseScale = Matrix4x4.Scale(scale).inverse;

            var sb = new StringBuilder();
            var any = false;
            for (var x = 0; x < mesh.subMeshCount; x++)
            {
                var subMesh = MeshExtensions.Submesh(mesh, x);

                sb.AppendLine($"g {rend.name.Replace("(Instance)", "").Replace(" Instance", "").Trim()}_{x}");

                for (var i = 0; i < subMesh.vertices.Length; i++)
                {
                    var v = subMesh.vertices[i];
                    if (bakedMesh && bakedWorldPosition)
                        v = rend.transform.TransformPoint(inverseScale.MultiplyPoint(v));
                    sb.AppendLine($"v {-v.x} {v.y} {v.z}");
                    any = true;
                }

                for (var i = 0; i < subMesh.uv.Length; i++)
                {
                    Vector3 v = subMesh.uv[i];
                    sb.AppendLine($"vt {v.x} {v.y}");
                    any = true;
                }

                for (var i = 0; i < subMesh.normals.Length; i++)
                {
                    var v = subMesh.normals[i];
                    sb.AppendLine($"vn {-v.x} {v.y} {v.z}");
                    any = true;
                }

                var triangles = subMesh.GetTriangles(x);
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    sb.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", triangles[i] + 1, triangles[i + 2] + 1, triangles[i + 1] + 1);
                    any = true;
                }
            }
            if (!any) throw new InvalidOperationException("No mesh data found or mesh is set as not readable");
            return sb.ToString();
        }

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
            }
        }
    }
}
