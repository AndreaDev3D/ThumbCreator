using System;
using UnityEngine;

namespace ThumbCreator.Helpers
{
    public class FileName
    {
        public static string GetBaseFolderPath => $"{Application.dataPath}/ThumbCreator";
        public static string GetTempFolderPath => $"{GetBaseFolderPath}/_temp";
        public static string GetTempFileName(int width, int height, int frameId) => $"{GetBaseFolderPath}/_temp/pic{frameId}.png";//{System.DateTime.Now.ToString("yyyyMMddHHmmssfff")}.png";
        public static string GetFileName(string name, string folder, string extention, int width, int height) => $"{GetBaseFolderPath}/{folder}/{name}_{width}x{height}_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.{extention}";
    }
}