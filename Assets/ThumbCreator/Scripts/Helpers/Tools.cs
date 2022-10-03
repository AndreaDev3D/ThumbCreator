using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ThumbCreator.Helpers
{
    public class Tools
    {
        public static Texture2D DuplicateTexture(Texture2D source)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            renderTex.name = source.name + "_output";
            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            Texture2D readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }

        public static void SaveTexture(Texture2D texture)
        {
            byte[] bytes = texture.EncodeToPNG();
            var dirPath = Path.Combine(Application.dataPath, "Output");
            var fileName = "R_" + Random.Range(0, 100000) + ".png";
            var fullDir = Path.Combine(dirPath, fileName);
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }
            File.WriteAllBytes(fullDir, bytes);
            Debug.Log(bytes.Length / 1024 + "Kb was saved as: " + fullDir);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
    }
}