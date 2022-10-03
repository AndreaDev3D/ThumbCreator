using System;
using ThumbCreator.Helpers;
using UnityEngine;

[Serializable, CreateAssetMenu(fileName = "StampOutlineEffect", menuName = "ThumbEditor/StampOutlineEffect", order = 1)]
public class StampOutlineEffect : TextureEffect, ITextureEffect
{
    [SerializeField]
    public Texture2D Stamp;
    [SerializeField]
    public int StampOutlineWidth = 5;
    [SerializeField]
    public Color StampOutlineColor = Color.black;
    public Texture2D ApplyEffect(Texture2D baseImage)
    {
        var stampCopy = Tools.DuplicateTexture(Stamp);
        for (int x = 0; x < baseImage.width; x++)
        {
            for (int y = 0; y < baseImage.height; y++)
            {
                var result = stampCopy.GetPixel(x, y);
                if (result.a != 0)
                {
                    for (int lx = 0; lx < StampOutlineWidth; lx++)
                    {
                        // left
                        if (stampCopy.GetPixel(x - lx, y).a == 0)
                        {
                            baseImage.SetPixel(x - lx, y, StampOutlineColor);
                        }
                        // right
                        if (stampCopy.GetPixel(x + lx, y).a == 0)
                        {
                            baseImage.SetPixel(x + lx, y, StampOutlineColor);
                        }
                        // up
                        if (stampCopy.GetPixel(x, y + lx).a == 0)
                        {
                            baseImage.SetPixel(x, y + lx, StampOutlineColor);
                        }
                        // down
                        if (stampCopy.GetPixel(x, y - lx).a == 0)
                        {
                            baseImage.SetPixel(x, y - lx, StampOutlineColor);
                        }
                        // left-up
                        if (stampCopy.GetPixel(x - lx, y + lx).a == 0)
                        {
                            baseImage.SetPixel(x - lx, y + lx, StampOutlineColor);
                        }
                        // right-up
                        if (stampCopy.GetPixel(x + lx, y + lx).a == 0)
                        {
                            baseImage.SetPixel(x + lx, y + lx, StampOutlineColor);
                        }
                        // left-down
                        if (stampCopy.GetPixel(x - lx, y - lx).a == 0)
                        {
                            baseImage.SetPixel(x - lx, y - lx, StampOutlineColor);
                        }
                        // right-down
                        if (stampCopy.GetPixel(x + lx, y - lx).a == 0)
                        {
                            baseImage.SetPixel(x + lx, y - lx, StampOutlineColor);
                        }
                    }
                    baseImage.SetPixel(x, y, result);
                }
            }
        }
        baseImage.Apply();

        return baseImage;
    }
}