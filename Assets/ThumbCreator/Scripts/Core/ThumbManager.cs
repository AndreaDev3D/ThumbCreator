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
            Cmd.RunCommand(cmdList);
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
            Cmd.RunCommand(cmdList);
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
            Cmd.RunCommand(cmdList);
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
            Cmd.RunCommand(cmdList);
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
            Cmd.RunCommand(cmdList);
        }

        
    }
}