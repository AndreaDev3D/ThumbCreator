using System;
using ThumbCreator.Helpers;
using UnityEngine;

[Serializable, CreateAssetMenu(fileName = "StampEffect", menuName = "ThumbEditor/StampEffect", order = 1)]
public class StampEffect : TextureEffect, ITextureEffect
{
    [SerializeField]
    public Texture2D Stamp;
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
                    baseImage.SetPixel(x, y, result);

                }
            }
        }
        baseImage.Apply();

        return baseImage;
    }
}
