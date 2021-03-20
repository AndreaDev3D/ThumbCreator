
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
    public bool isCameraBackgroundTransparent;
    public Color CameraBackgroundColor;
    [Range(-20, 20)]
    public int CameraX;
    [Range(-20, 20)]
    public int CameraY;
    [Range(0, -100)]
    public int CameraZ=-8;
    [Header("Export Settings")]
    public string FileName="Image";
    public FileType ExportFile = FileType.Png;
    public Resolution ResolutionWidth = Resolution.res128;
    public Resolution ResolutionHeight = Resolution.res128;
    [Header("GIF Settings")]
    [Range(8, 360)]
    public int FrameResolution = 16;
    public float FrameDuration = 1;

    // Assets/ThumbCreator
    public static string GetBaseFolderPath => $"{Application.dataPath}/ThumbCreator";
    public static string GetTempFolderPath => $"{GetBaseFolderPath}/_temp";
    //public static string GetPngTempFileName(int width, int height, int frameId) => $"{GetBaseFolderPath}/temp/screen_{frameId}_{width}x{height}_{System.DateTime.Now.ToString("yyyyMMddHHmmssfff")}.png";
    //public static string GetPngFileName(int width, int height) => $"{GetBaseFolderPath}/_Screenshot/screen_{width}x{height}_{System.DateTime.Now.ToString("yyyyMMddHHmmssfff")}.png";
    //public static string GetSpriteFileName(string name, int spriteCount, int width, int height) => $"{GetBaseFolderPath}/_Sprite/{name}_{spriteCount}_{width}x{height}.png";//_{System.DateTime.Now.ToString("yyyyMMddHHmmssfff")}.png";
    //public static string GetGifFileName(string name, int width, int height, int frameResolution) => $"{GetBaseFolderPath}/_Gif/gif_{width}x{height}_{frameResolution}_{System.DateTime.Now.ToString("yyyyMMddHHmmssfff")}.gif";
    //public static string GetMp4FileName(string name, int width, int height) => $"{GetBaseFolderPath}/_Video/video_{width}x{height}_{System.DateTime.Now.ToString("yyyyMMddHHmmssfff")}.mp4";

    public static string GetTempFileName(int width, int height, int frameId) => $"{GetBaseFolderPath}/_temp/pic{frameId}.png";//{System.DateTime.Now.ToString("yyyyMMddHHmmssfff")}.png";
    public static string GetFileName(string name, string folder, string extention, int width, int height) => $"{GetBaseFolderPath}/{folder}/{name}_{width}x{height}_{System.DateTime.Now.ToString("yyyyMMddHHmmssfff")}.{extention}";

    // Update is called once per frame
    void Update()
    {
        var objRot = transform.rotation.eulerAngles;
        var newRot = new Vector3(RotationX, RotationY, RotationZ);
        if (objRot != newRot)
            transform.localRotation = Quaternion.Euler(RotationX, RotationY, RotationZ);

        var camPos = Camera.main.transform;
        Camera.main.backgroundColor = new Color(CameraBackgroundColor.r, CameraBackgroundColor.g, CameraBackgroundColor.b, isCameraBackgroundTransparent ? 0.0f : CameraBackgroundColor.a);
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
        if (ExportFile != FileType.Png)
        {
            var frameCount = 360 / (int)FrameResolution;
            var count = 0;
            for (int i = 0; i < 360; i += frameCount)
            {
                transform.localRotation = Quaternion.Euler(RotationX, i, RotationZ);
                GeneratePng(false, count);
                count++;
            }
        }
        switch (ExportFile)
        {
            case FileType.Png:
                GeneratePng();
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
        AssetDatabase.Refresh();
    }

    public void GeneratePng(bool isPng = true, int i = 0)
    {
        try
        {
            var camera = Camera.main;
            string filename = isPng ? GetFileName(FileName, "_Png", "png",(int)ResolutionWidth, (int)ResolutionHeight) : GetTempFileName((int)ResolutionWidth, (int)ResolutionHeight, i);
            //Debug.Log($"Getting screenshot at {ResolutionWidth}x{ResolutionHeight}");
            
            var renderTexture = new RenderTexture((int)ResolutionWidth, (int)ResolutionHeight, 24);
            camera.targetTexture = renderTexture;
            var screenShot = new Texture2D((int)ResolutionWidth, (int)ResolutionHeight, TextureFormat.ARGB32, false);
            screenShot.alphaIsTransparency = true;
            camera.Render();
            RenderTexture.active = renderTexture;
            screenShot.ReadPixels(new Rect(0, 0, (int)ResolutionWidth, (int)ResolutionHeight), 0, 0);
            camera.targetTexture = null;
            RenderTexture.active = null; // JC: added to avoid errors
            DestroyImmediate(renderTexture);
            byte[] bytes = screenShot.EncodeToPNG();
            System.IO.File.WriteAllBytes(filename, bytes);
            //Debug.Log(string.Format("Took screenshot to: {0}", filename));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"{ex}");
        }
    }

    private void GenerateSprite()
    {
        var picturesFolder = GetTempFolderPath;
        var filename = GetFileName(FileName, "_Sprite", "png", (int)ResolutionHeight, (int)ResolutionHeight);
        var fileList = Directory.GetFiles(picturesFolder, "*.png").ToList();

        //List<Bitmap> imageList = new List<Bitmap>();
        //fileList.ForEach(file => imageList.Add(new Bitmap(Image.FromFile(file)) ));// new Bitmap(Image.FromFile(fileList[0]));

        var isGridEven = fileList.Count() % 2 == 0 ? 4 : 3;
        var gridWidth = isGridEven;
        var gridHeight = Math.Ceiling((decimal)fileList.Count() / gridWidth);
        //var filename = GetFileName(FileName, "_Sprite", "png", (int)ResolutionWidth, (int)ResolutionHeight);

        //var target = new Bitmap((int)ResolutionWidth * gridWidth, (int)ResolutionHeight * (int)gridHeight, PixelFormat.Format32bppArgb);
        //var graphics = System.Drawing.Graphics.FromImage(target);
        //graphics.CompositingMode = CompositingMode.SourceOver; // this is the default, but just to be clear

        //var row = -1;
        //var col = 0;
        //for (int i = 0; i < imageList.Count; i++)
        //{
        //    row = i % isGridEven == 0 ? row + 1 : row;
        //    col = i % isGridEven;
        //    //Debug.Log($"i : {i}({col}-{row})");
        //    graphics.DrawImage(imageList[i], col * (int)ResolutionWidth, row * (int)ResolutionHeight, (int)ResolutionWidth, (int)ResolutionHeight);
        //}

        //target.Save(filename, ImageFormat.Png);

        //$ ffmpeg -i %03d.png -filter_complex scale=120:-1,tile=5x1 output.png
        var cmdList = new Dictionary<string, string>();
        //cmdList["-r"] = (FrameResolution - 1).ToString();
        //cmdList["-s"] = $"{(int)ResolutionWidth}x{(int)ResolutionHeight}";
        //cmdList["-y"] = "";
        cmdList["-i"] = $"{picturesFolder}/pic%0d.png";
        cmdList["-filter_complex"] = $"scale=120:-1,tile={gridWidth}x{gridHeight}";
        cmdList[""] = filename;
        RunCommand(cmdList);
    }

    private void GenerateGif()
    {
        // ffmpeg -y -i E:/App/Unity/TileCityBuilder/Assets/ThumbCreator/_temp/pic%0d.png ../../../_Gif/output.gif
        var picturesFolder = GetTempFolderPath;
        var filename = GetFileName(FileName, "_Gif", "gif", (int)ResolutionHeight, (int)ResolutionHeight);
        var fileList = Directory.GetFiles(picturesFolder, "*.png").ToList();

        var cmdList = new Dictionary<string, string>();
        cmdList["-r"] = (FrameResolution - 1).ToString();
        cmdList["-s"] = $"{(int)ResolutionWidth}x{(int)ResolutionHeight}";
        cmdList["-y"] = "";
        cmdList["-i"] = $"{picturesFolder}/pic%0d.png";
        cmdList[""] = filename;
        RunCommand(cmdList);
        //var e = new AnimatedGifEncoder();
        //e.Start(filename);
        //e.SetTransparent(System.Drawing.Color.FromArgb(0,0,0,0));
        //e.SetDelay((int)FrameDuration * 100);
        //e.SetRepeat(0); //-1:no repeat,0:always repeat 
        //int counter = 0;

        //foreach (var item in fileList)
        //{
        //    var img = Image.FromFile(item);
        //    e.SetTransparent(System.Drawing.Color.FromArgb(0, 0, 0, 0));
        //    e.AddFrame(img);
        //    counter++;
        //}
        //e.Finish();
    }
    
    private void GenerateMp4()
    {
        //ffmpeg -r 60 -f image2 -s 1920x1080 -y -i E:/App/Unity/TileCityBuilder/Assets/ThumbCreator/_temp/pic%0d.png -vcodec libx264 -crf 25  -pix_fmt yuv420p ../../../_Video/test.mp4
        var picturesFolder = GetTempFolderPath;
        var filename = GetFileName(FileName, "_Video", "mp4", (int)ResolutionWidth, (int)ResolutionHeight);

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

    private void GenerateAvi()
    {
        //ffmpeg -r 60 -f image2 -s 1920x1080 -y -i E:/App/Unity/TileCityBuilder/Assets/ThumbCreator/_temp/pic%0d.png -vcodec libx264 -crf 25  -pix_fmt yuv420p ../../../_Video/test.mp4
        var picturesFolder = GetTempFolderPath;
        var filename = GetFileName(FileName, "_Video", "avi", (int)ResolutionWidth, (int)ResolutionHeight);

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
        var picturesFolder = GetTempFolderPath;
        var filename = GetFileName(FileName, "_Video", "mov", (int)ResolutionWidth, (int)ResolutionHeight);

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

    private async void RunCommand(Dictionary<string, string> commandList)
    {
        var cmdArgument = string.Join(" ", commandList.Select(x => x.Key + " " + x.Value).ToArray());
        Debug.Log(cmdArgument);
        var converter = new ProcessStartInfo($"{GetBaseFolderPath}/Plugins/ffmpeg/bin/ffmpeg.exe");
        converter.UseShellExecute = false;
        converter.Arguments = cmdArgument;
        Process correctionProcess = new Process();
        correctionProcess.StartInfo = converter;
        correctionProcess.StartInfo.CreateNoWindow = true;
        correctionProcess.StartInfo.UseShellExecute = false;
        correctionProcess.Start();
        while(!correctionProcess.HasExited)
        {
            Console.WriteLine("Excel is busy");
            await System.Threading.Tasks.Task.Delay(25);
        }

        CleanTempFolder();
        AssetDatabase.Refresh();

    }

    private void CleanTempFolder() 
    {
        try
        {
            // Delete all files in a directory    
            string[] files = Directory.GetFiles(GetTempFolderPath);
            foreach (string file in files)
            {
                File.Delete(file.Replace("\\", "/"));
                Debug.Log($"{file} is deleted.");
            }
        }
        catch(Exception ex)
        {
            Debug.LogError($"Delete Error : {ex}");
        }
    }
}

public enum Resolution
{
    res8 = 8,
    res32 =32,
    res64=64,
    res128=128,
    res512=512,
    res1024=1024,
    res2048=2048,
    res4096=4096,
    res8192 = 8192
}

public enum FileType
{
    Png,
    Sprite,
    Gif,
    Mp4,
    Avi,
    Mov
}
