using System;
using UnityEngine;

[Serializable, CreateAssetMenu(fileName = "BorderEffect", menuName = "ThumbEditor/BorderEffect", order = 1)]
public class BorderEffect : TextureEffect, ITextureEffect
{
    [SerializeField]
    public int BorderWidth;
    [SerializeField]
    public Color BorderColor;
    public Texture2D ApplyEffect(Texture2D baseImage)
    {
        for (int x = 0; x < baseImage.width; x++)
        {
            for (int y = 0; y < baseImage.height; y++)
            {
                if (x < BorderWidth || x > baseImage.width - 1 - BorderWidth)
                {
                    baseImage.SetPixel(x, y, BorderColor);
                }
                else if (y < BorderWidth || y > baseImage.height - 1 - BorderWidth)
                {
                    baseImage.SetPixel(x, y, BorderColor);
                }
            }
        }
        baseImage.Apply();

        return baseImage;
    }
}
