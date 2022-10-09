using System.ComponentModel;

namespace ThumbCreator
{
    public class Enumerators
    {
        public enum Resolution
        {
            [Description("8x8")]
            res8 = 8,
            [Description("32x32")]
            res32 = 32,
            [Description("64x64")]
            res64 = 64,
            [Description("128x128")]
            res128 = 128,
            [Description("512x512")]
            res512 = 512,
            [Description("1024x1024")]
            res1024 = 1024,
            [Description("2048x2048")]
            res2048 = 2048,
            [Description("4096x4096")]
            res4096 = 4096,
            [Description("8192x8192")]
            res8192 = 8192
        }

        public enum FileType
        {
            [Description("PNG Image")]
            Png,
            [Description("Animated Sprite")]
            Sprite,
            [Description("GIF Image")]
            Gif,
            [Description("MP4 Video")]
            Mp4,
            [Description("AVI Video")]
            Avi,
            [Description("MOV Video")]
            Mov
        }
    }
}
