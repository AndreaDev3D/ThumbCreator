using System;
using UnityEngine;

[Serializable, CreateAssetMenu(fileName = "FillEffect", menuName = "ThumbEditor/FillEffect", order = 1)]
public class FillEffect : TextureEffect, ITextureEffect
{
    [SerializeField]
    public Color FillColor;
    public Texture2D ApplyEffect(Texture2D baseImage)
    {
        for (int x = 0; x < baseImage.width; x++)
        {
            for (int y = 0; y < baseImage.height; y++)
            {
                var result = baseImage.GetPixel(x, y);
                if (result.a == 0)
                {
                    baseImage.SetPixel(x, y, FillColor);

                }
            }
        }
        baseImage.Apply();

        return baseImage;
    }
}
