using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThumbCreator.Helpers
{
    public static class Screenshot
    {
        [MenuItem("Tools/ThumbCreator/Take Screenshot")]
        static void TakeScreenshot()
        {
            Debug.Log(Tools.GetNextName());

            //Screen.SetResolution(1024, 1024, Screen.fullScreen);
            var filename = Path.Combine(Application.dataPath, "Output", "test.png");
            GeneratePng(Tools.GetNextName(), Screen.width, Screen.height);
        }

        public static void GeneratePng(string fileName, int width, int height, bool isPng = true, int index = 0)
        {
            try
            {
                var camera = Camera.main;
                //string filename = isPng ? FileName.GetFileName(fileName, "_Png", "png", (int)width, (int)height) : FileName.GetTempFileName((int)width, (int)height, index);

                var renderTexture = new RenderTexture((int)width, (int)height, 24);
                camera.targetTexture = renderTexture;
                var screenShot = new Texture2D((int)width, (int)height, TextureFormat.ARGB32, false);
#if UNITY_EDITOR
                screenShot.alphaIsTransparency = true;
#endif
                screenShot.Apply();
                camera.Render();
                RenderTexture.active = renderTexture;
                screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                camera.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(renderTexture);

                Tools.SaveTexture(screenShot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{ex}");
            }
        }

        public static void GeneratePngNew(string fileName, int width, int height, bool isPng = true, int index = 0)
        {
            var cam = Camera.main;
            string filename = isPng ? FileName.GetFileName(fileName, "_Png", "png", width, height) : FileName.GetTempFileName(width, height, index);

            // Depending on your render pipeline, this may not work.
            var bak_cam_targetTexture = cam.targetTexture;
            var bak_cam_clearFlags = cam.clearFlags;
            var bak_RenderTexture_active = RenderTexture.active;

            var tex_transparent = new Texture2D(width, height, TextureFormat.ARGB32, false);
            // Must use 24-bit depth buffer to be able to fill background.
            var render_texture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var grab_area = new Rect(0, 0, width, height);

            RenderTexture.active = render_texture;
            cam.targetTexture = render_texture;
            cam.clearFlags = CameraClearFlags.SolidColor;

            // Simple: use a clear background
            cam.backgroundColor = Color.clear;
            cam.Render();
            tex_transparent.ReadPixels(grab_area, 0, 0);
#if UNITY_EDITOR
            tex_transparent.alphaIsTransparency = true;
            tex_transparent.Apply();
            AssetDatabase.Refresh();
#endif
            // Encode the resulting output texture to a byte array then write to the file
            byte[] pngShot = tex_transparent.EncodeToPNG();
            System.IO.File.WriteAllBytes(filename, pngShot);

            cam.clearFlags = bak_cam_clearFlags;
            cam.targetTexture = bak_cam_targetTexture;
            RenderTexture.active = bak_RenderTexture_active;
            RenderTexture.ReleaseTemporary(render_texture);
            UnityEngine.RenderTexture.DestroyImmediate(tex_transparent);
        }
    }
}