using System;
using System.IO;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuntimeUnityEditor.Core.Utils
{
    /// <summary>
    /// Utility methods for working with texture objects.
    /// </summary>
    public static class TextureUtils
    {
        /// <summary>
        /// Copy this texture inside a new editable Texture2D.
        /// </summary>
        /// <param name="tex">Texture to copy</param>
        /// <param name="format">Format of the copy</param>
        /// <param name="mipMaps">Copy has mipmaps</param>
        public static Texture2D ToTexture2D(this Texture tex, TextureFormat format = TextureFormat.ARGB32, bool mipMaps = false)
        {
            var rt = RenderTexture.GetTemporary(tex.width, tex.height);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            GL.Clear(true, true, Color.clear);

            Graphics.Blit(tex, rt);

            var t = new Texture2D(tex.width, tex.height, format, mipMaps);
            t.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            t.Apply(false);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        /// <summary>
        /// EncodeToPNG that doesn't care about what kind of texture you give it, or what unity version is currently running.
        /// </summary>
        public static byte[] BasedEncodeToPNG(this Texture tex)
        {
            var t2d = tex.ToTexture2D();
            try
            {
                var m = typeof(Texture2D).GetMethod("EncodeToPNG", BindingFlags.Instance | BindingFlags.Public);
                if (m != null)
                {
                    return (byte[])m.Invoke(t2d, new object[0]);
                }
                else
                {
                    var t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", false);
                    var m2 = t?.GetMethod("EncodeToPNG", BindingFlags.Static | BindingFlags.Public);
                    if (m2 != null)
                        return (byte[])m2.Invoke(null, new object[] { t2d });

                    throw new Exception("Could not find method EncodeToPNG, can't save to file.");
                }
            }
            finally
            {
                Object.Destroy(t2d);
            }
        }

        /// <summary>
        /// Show file save dialog and write PNG version of the texture to selected filename.
        /// </summary>
        public static void SaveTextureToFileWithDialog(this Texture texture)
        {
            const OpenFileDialog.OpenSaveFileDialgueFlags saveFileFlags = OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES |
                                                                          OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER |
                                                                          OpenFileDialog.OpenSaveFileDialgueFlags.OFN_OVERWRITEPROMPT;
            try
            {
                var data = texture?.BasedEncodeToPNG();
                if (data != null)
                {
                    var filename = OpenFileDialog.ShowDialog("Export texture to file...", null, ".PNG file|*.png", ".png", saveFileFlags, "export.png");
                    if (filename != null && filename.Length > 0)
                    {
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, "Writing encoded texture data to file at " + filename[0]);
                        File.WriteAllBytes(filename[0], data);
                    }
                }
                else
                {
                    throw new Exception("Failed to encode texture");
                }
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error | LogLevel.Message,
                    "Could not encode texture to PNG. Reason: " + e.Message);
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e.StackTrace);
            }
        }

        /// <summary>
        /// Gets texture as it is shown by this sprite. If it's not packed then returns the original texture.
        /// If it's packed then this tries to crop out the part that the sprite is supposed to show and return only that.
        /// </summary>
        public static Texture2D GetVisibleTexture(this Sprite spr)
        {
            if (spr.packed && spr.packingMode != SpritePackingMode.Tight)
            {
                // Make a copy we can read from
                var tempTex = spr.texture.ToTexture2D();
                var width = (int)spr.textureRect.width;
                var height = (int)spr.textureRect.height;
                var pixels = tempTex.GetPixels((int)spr.textureRect.x, (int)spr.textureRect.y, width, height);
                GameObject.Destroy(tempTex);
                var outTex = new Texture2D(width, height);
                outTex.SetPixels(pixels);
                outTex.Apply();
                return outTex;
            }

            return spr.texture;
        }
    }
}
