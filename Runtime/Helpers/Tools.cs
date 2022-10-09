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

        public static void SaveTexture(Texture2D texture, string extention = "png")
        {
            byte[] bytes = texture.EncodeToPNG();
            var fullDir = GetNextName("Output", "Screenshot", extention);

            File.WriteAllBytes(fullDir, bytes);
            Debug.Log(bytes.Length / 1024 + "Kb was saved as: " + fullDir);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

        public static string GetNextName(string folderName = "Output", string fileName = "Screenshot", string extention = "png")
        {
            var dirPath = Path.Combine(Application.dataPath, folderName);
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            FileInfo[] Files = dirInfo.GetFiles($"{fileName}_*.{extention}");
            int maxIndex = -1;
            foreach (FileInfo file in Files)
            {
                int str = int.Parse(file.Name.Split('_')[1].Replace($".{extention}", ""));
                if (str > maxIndex)
                {
                    maxIndex = str;
                }
            }

            return Path.Combine(dirPath, $"{fileName}_{(maxIndex + 1).ToString()}.{extention}");
        }
    }
}