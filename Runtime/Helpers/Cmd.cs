using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ThumbCreator.Helpers
{
    public class Cmd
    {
        // https://ffmpeg.org/ffmpeg.html
        // https://gist.github.com/tayvano/6e2d456a9897f55025e25035478a3a50
        public static async void RunCommand(Dictionary<string, string> commandList)
        {
            var cmdArgument = string.Join(" ", commandList.Select(x => x.Key + " " + x.Value).ToArray());
            UnityEngine.Debug.Log(cmdArgument);
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

        private static void CleanTempFolder()
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