using System.Collections.Generic;
using System;
using UnityEngine;

[Serializable]
public class TextureLayer
{
    [field: SerializeField]
    public string Name { get; set; }

    [field: SerializeField]
    public bool IsRendering { get; set; } = true;

    [field: SerializeField]
    public List<TextureEffect> textureEffects;

    public Texture2D GetLayerPostEffect(Texture2D baseImage)
    {
        foreach (var effect in textureEffects)
        {
            Debug.Log($"Applying : {effect.name}");
            baseImage = ((ITextureEffect)effect).ApplyEffect(baseImage);
        }
        return baseImage;
    }
}
