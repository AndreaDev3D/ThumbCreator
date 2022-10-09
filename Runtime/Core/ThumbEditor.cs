using System.Collections;
using System.Collections.Generic;
using ThumbCreator.Helpers;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using static log4net.Appender.ColoredConsoleAppender;
using static System.Net.Mime.MediaTypeNames;
using static UnityEngine.EventSystems.EventTrigger;

namespace ThumbCreator
{
    public class ThumbEditor : MonoBehaviour
    {
        public Texture2D InputImage;
        public Texture2D OutputImage;
        public RawImage Image;
        public List<TextureLayer> textureLayers;

        public void Render()
        {
            OutputImage = Tools.DuplicateTexture(InputImage);

            foreach (var texture in textureLayers)
            {
                if (texture.IsRendering)
                    OutputImage = texture.GetLayerPostEffect(OutputImage);
            }
            Image.texture = OutputImage;
        }

        public void Save()
        {
            if (OutputImage != null)
            {
                Tools.SaveTexture(OutputImage);
            }
        }
    }
}