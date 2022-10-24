using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThumbCreator.Helpers
{
    public static class Screenshot
    {
        [MenuItem("Tools/ThumbCreator/Take Screenshot with transparency")]
        static void TakeScreenshot()
        {
            GeneratePng(Tools.GetNextName(), Screen.width, Screen.height);
        }

        [MenuItem("Tools/ThumbCreator/Take Screenshot with UI")]
        static void TakeScreenshotUI()
        {
            ScreenCapture.CaptureScreenshot(Tools.GetNextName(), 1);
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        [MenuItem("Tools/ThumbCreator/Take Screenshot with UI x2 resolution")]
        static void TakeScreenshotUISuperRes()
        {
            ScreenCapture.CaptureScreenshot(Tools.GetNextName(), 2);
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        public static void GeneratePng(string fileName, int width, int height, bool isPng = true, int index = 0)
        {
            try
            {
                var camera = Camera.main;

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
    }
}