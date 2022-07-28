using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ThumbCreator.Helpers;
using UnityEditor;
using UnityEngine;
using static ThumbCreator.Enumerators;
using Debug = UnityEngine.Debug;
using Resolution = ThumbCreator.Enumerators.Resolution;

namespace ThumbCreator.Core
{
    [ExecuteInEditMode]
    public class ThumbManager : MonoBehaviour
    {
        [Header("Target Settings")]
        [Range(0, 360)]
        public int RotationX;
        [Range(0, 360)]
        public int RotationY;
        [Range(0, 360)]
        public int RotationZ;
        [Header("Camera Settings")]
        public bool isCameraOrthographic;
        //public bool isCameraBackgroundTransparent;
        public Color CameraBackgroundColor;
        [Range(-20, 20)]
        public int CameraX;
        [Range(-20, 20)]
        public int CameraY;
        [Range(0, -100)]
        public int CameraZ = -8;
        [Header("Export Settings")]
        public string Filename = "Image";
        public FileType ExportFile = FileType.Png;
        public Resolution ResolutionWidth = Resolution.res128;
        private int m_width => (int)ResolutionWidth;
        public Resolution ResolutionHeight = Resolution.res128;
        private int m_height => (int)ResolutionHeight;
        [Header("GIF Settings")]
        [Range(4, 360)]
        public int FrameResolution = 16;
        public int FrameRate = 1;

        //public string GetBaseFolderPath => $"{Application.dataPath}/ThumbCreator";
        //public string GetTempFolderPath => $"{GetBaseFolderPath}/_temp";
        //public string GetTempFileName(int width, int height, int frameId) => $"{GetBaseFolderPath}/_temp/pic{frameId}.png";//{System.DateTime.Now.ToString("yyyyMMddHHmmssfff")}.png";
        //public string GetFileName(string name, string folder, string extention, int width, int height) => $"{GetBaseFolderPath}/{folder}/{name}_{width}x{height}_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.{extention}";

        void Update()
        {
            var objRot = transform.rotation.eulerAngles;
            var newRot = new Vector3(RotationX, RotationY, RotationZ);
            if (objRot != newRot)
                transform.localRotation = Quaternion.Euler(RotationX, RotationY, RotationZ);

            var camPos = Camera.main.transform;
            Camera.main.backgroundColor = CameraBackgroundColor;
            var newPos = new Vector3(CameraX, CameraY, CameraZ);
            if (camPos.position != newPos)
            {
                Camera.main.orthographic = isCameraOrthographic;
                if (isCameraOrthographic)
                    Camera.main.orthographicSize = CameraZ * -1;
                camPos.localPosition = newPos;
            }
        }

        public void Take()
        {
            RotateItem();
            GenerateFile();
        }

        public void RotateItem()
        {
            if (ExportFile != FileType.Png)
            {
                var frameCount = 360 / FrameResolution;
                var count = 0;
                for (int i = 0; i < 360; i += frameCount)
                {
                    transform.localRotation = Quaternion.Euler(RotationX, i, RotationZ);
                    Screenshot.GeneratePng(Filename, m_width, m_height, false, count);
                    count++;
                }
            }
        }

        public void GenerateFile()
        {
            switch (ExportFile)
            {
                case FileType.Png:
                    Screenshot.GeneratePng(Filename, m_width, m_height);
                    break;
                case FileType.Sprite:
                    GenerateSprite();
                    break;
                case FileType.Gif:
                    GenerateGif();
                    break;
                case FileType.Mp4:
                    GenerateMp4();
                    break;
                case FileType.Avi:
                    GenerateAvi();
                    break;
                case FileType.Mov:
                    GenerateMov();
                    break;
                default:
                    break;
            }
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        //        public void GeneratePng(bool isPng = true, int i = 0)
        //        {
        //            try
        //            {
        //                var camera = Camera.main;
        //                string filename = isPng ? FileName.GetFileName(Filename, "_Png", "png", (int)ResolutionWidth, (int)ResolutionHeight) : FileName.GetTempFileName((int)ResolutionWidth, (int)ResolutionHeight, i);

        //                var renderTexture = new RenderTexture((int)ResolutionWidth, (int)ResolutionHeight, 24);
        //                camera.targetTexture = renderTexture;
        //                var screenShot = new Texture2D((int)ResolutionWidth, (int)ResolutionHeight, TextureFormat.ARGB32, false);
        //#if UNITY_EDITOR
        //                screenShot.alphaIsTransparency = isCameraBackgroundTransparent;
        //#endif
        //                screenShot.Apply();
        //                camera.Render();
        //                RenderTexture.active = renderTexture;
        //                screenShot.ReadPixels(new Rect(0, 0, (int)ResolutionWidth, (int)ResolutionHeight), 0, 0);
        //                camera.targetTexture = null;
        //                RenderTexture.active = null;
        //                DestroyImmediate(renderTexture);
        //                byte[] bytes = screenShot.EncodeToPNG();
        //                File.WriteAllBytes(filename, bytes);
        //            }
        //            catch (Exception ex)
        //            {
        //                Debug.LogError($"{ex}");
        //            }
        //        }

//        public void GeneratePng1(bool isPng = true, int i = 0)
//        {
//            int width = (int)ResolutionWidth;
//            int height = (int)ResolutionHeight;
//            var cam = Camera.main;
//            string filename = isPng ? GetFileName(Filename, "_Png", "png", (int)ResolutionWidth, (int)ResolutionHeight) : GetTempFileName((int)ResolutionWidth, (int)ResolutionHeight, i);

//            // Depending on your render pipeline, this may not work.
//            var bak_cam_targetTexture = cam.targetTexture;
//            var bak_cam_clearFlags = cam.clearFlags;
//            var bak_RenderTexture_active = RenderTexture.active;

//            var tex_transparent = new Texture2D(width, height, TextureFormat.ARGB32, false);
//            // Must use 24-bit depth buffer to be able to fill background.
//            var render_texture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
//            var grab_area = new Rect(0, 0, width, height);

//            RenderTexture.active = render_texture;
//            cam.targetTexture = render_texture;
//            cam.clearFlags = CameraClearFlags.SolidColor;

//            // Simple: use a clear background
//            cam.backgroundColor = Color.clear;
//            cam.Render();
//            tex_transparent.ReadPixels(grab_area, 0, 0);
//#if UNITY_EDITOR
//            tex_transparent.alphaIsTransparency = true;
//            tex_transparent.Apply();
//            AssetDatabase.Refresh();
//#endif
//            // Encode the resulting output texture to a byte array then write to the file
//            byte[] pngShot = tex_transparent.EncodeToPNG();
//            File.WriteAllBytes(filename, pngShot);

//            cam.clearFlags = bak_cam_clearFlags;
//            cam.targetTexture = bak_cam_targetTexture;
//            RenderTexture.active = bak_RenderTexture_active;
//            RenderTexture.ReleaseTemporary(render_texture);
//            DestroyImmediate(tex_transparent);
//        }

        private void GenerateSprite()
        {
            //$ ffmpeg -i %03d.png -filter_complex scale=120:-1,tile=5x1 output.png
            var picturesFolder = FileName.GetTempFolderPath;
            var filename = FileName.GetFileName(Filename, "_Sprite", "png", m_width, m_height);
            var fileList = Directory.GetFiles(picturesFolder, "*.png").ToList();

            var isGridEven = fileList.Count() % 2 == 0 ? 4 : 3;
            var gridWidth = isGridEven;
            var gridHeight = Math.Ceiling((decimal)fileList.Count() / gridWidth);

            var cmdList = new Dictionary<string, string>();
            cmdList["-i"] = $"{picturesFolder}/pic%0d.png";
            cmdList["-filter_complex"] = $"scale=100:-1,tile={gridWidth}x{gridHeight}";
            cmdList[""] = filename;
            RunCommand(cmdList);
        }

        private void GenerateGif()
        {
            // ffmpeg -y -i E:/App/Unity/TileCityBuilder/Assets/ThumbCreator/_temp/pic%0d.png ../../../_Gif/output.gif
            var picturesFolder = FileName.GetTempFolderPath;
            var filename = FileName.GetFileName(Filename, "_Gif", "gif", m_width, m_height);

            var cmdList = new Dictionary<string, string>();
            cmdList["-r"] = $"{FrameRate}";
            cmdList["-s"] = $"{(int)ResolutionWidth}x{(int)ResolutionHeight}";
            cmdList["-i"] = $"{picturesFolder}/pic%0d.png";
            cmdList[""] = filename;
            RunCommand(cmdList);
        }

        private void GenerateMp4()
        {
            //ffmpeg -r 60 -f image2 -s 1920x1080 -y -i E:/App/Unity/TileCityBuilder/Assets/ThumbCreator/_temp/pic%0d.png -vcodec libx264 -crf 25  -pix_fmt yuv420p ../../../_Video/test.mp4
            var picturesFolder = FileName.GetTempFolderPath;
            var filename = FileName.GetFileName(Filename, "_Video", "mp4", m_width, m_height);

            var cmdList = new Dictionary<string, string>();
            cmdList["-r"] = FrameResolution.ToString();
            cmdList["-f"] = "image2";
            cmdList["-s"] = $"{(int)ResolutionWidth}x{(int)ResolutionHeight}";
            cmdList["-y"] = "";
            cmdList["-i"] = $"{picturesFolder}/pic%0d.png";
            cmdList["-vcodec"] = "libx264";
            cmdList["-crf"] = "25";
            cmdList["-pix_fmt"] = "yuv420p";
            cmdList[""] = filename;
            RunCommand(cmdList);
        }

        private void GenerateAvi()
        {
            //ffmpeg -r 60 -f image2 -s 1920x1080 -y -i E:/App/Unity/TileCityBuilder/Assets/ThumbCreator/_temp/pic%0d.png -vcodec libx264 -crf 25  -pix_fmt yuv420p ../../../_Video/test.mp4
            var picturesFolder = FileName.GetTempFolderPath;
            var filename = FileName.GetFileName(Filename, "_Video", "avi", m_width, m_height);

            var cmdList = new Dictionary<string, string>();
            cmdList["-r"] = (FrameResolution - 1).ToString();
            cmdList["-f"] = "image2";
            cmdList["-s"] = $"{(int)ResolutionWidth}x{(int)ResolutionHeight}";
            cmdList["-y"] = "";
            cmdList["-i"] = $"{picturesFolder}/pic%0d.png";
            cmdList["-vcodec"] = "libx264";
            cmdList["-crf"] = "25";
            cmdList["-pix_fmt"] = "yuv420p";
            cmdList[""] = filename;
            RunCommand(cmdList);
        }

        private void GenerateMov()
        {
            //ffmpeg -r 60 -f image2 -s 1920x1080 -y -i E:/App/Unity/TileCityBuilder/Assets/ThumbCreator/_temp/pic%0d.png -vcodec libx264 -crf 25  -pix_fmt yuv420p ../../../_Video/test.mp4
            var picturesFolder = FileName.GetTempFolderPath;
            var filename = FileName.GetFileName(Filename, "_Video", "mov", m_width, m_height);

            var cmdList = new Dictionary<string, string>();
            cmdList["-r"] = "20";// (FrameResolution - 1).ToString();
            cmdList["-f"] = "image2";
            cmdList["-s"] = $"{(int)ResolutionWidth}x{(int)ResolutionHeight}";
            cmdList["-y"] = "";
            cmdList["-i"] = $"{picturesFolder}/pic%0d.png";
            cmdList["-vframes"] = "100";
            cmdList["-vcodec"] = "libx264";
            cmdList["-crf"] = "25";
            cmdList["-pix_fmt"] = "bgra";
            cmdList[""] = filename;
            RunCommand(cmdList);
        }

        // https://ffmpeg.org/ffmpeg.html
        // https://gist.github.com/tayvano/6e2d456a9897f55025e25035478a3a50
        private async void RunCommand(Dictionary<string, string> commandList)
        {
            var cmdArgument = string.Join(" ", commandList.Select(x => x.Key + " " + x.Value).ToArray());
            Debug.Log(cmdArgument);
            var converter = new ProcessStartInfo($"{FileName.GetBaseFolderPath}/Plugins/ffmpeg/bin/ffmpeg.exe");
            converter.UseShellExecute = false;
            converter.Arguments = cmdArgument;
            Process correctionProcess = new Process();
            correctionProcess.StartInfo = converter;
            correctionProcess.StartInfo.CreateNoWindow = true;
            correctionProcess.StartInfo.UseShellExecute = false;
            correctionProcess.Start();
            while (!correctionProcess.HasExited)
            {
                Console.WriteLine("ffmpeg is busy");
                await System.Threading.Tasks.Task.Delay(25);
            }

            CleanTempFolder();
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        private void CleanTempFolder()
        {
            try
            {  
                string[] files = Directory.GetFiles(FileName.GetTempFolderPath);
                Debug.Log($"{files.Length} files has been deleted.");
                foreach (string file in files)
                {
                    File.Delete(file.Replace("\\", "/"));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Delete Error : {ex}");
            }
        }
    }
}