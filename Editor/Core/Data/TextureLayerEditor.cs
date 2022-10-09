using ThumbCreator.Core;
using UnityEditor;

[CustomEditor(typeof(TextureLayer))]

public class TextureLayerEditor : Editor
{
    TextureLayer _target;
    void OnEnable()
    {
        //_target = (TextureLayer)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
