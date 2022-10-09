using System.Collections;
using System.Collections.Generic;
using ThumbCreator;
using ThumbCreator.Core;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ThumbEditor))]
public class ThumbEditorEditor : Editor
{
    ThumbEditor _target;
    void OnEnable()
    {
        _target = (ThumbEditor)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(20);
        if (GUILayout.Button($"Render", GUILayout.Height(30)))
        {
            _target.Render();
        }
        if (GUILayout.Button($"Save", GUILayout.Height(30)))
        {
            _target.Save();
        }
    }
}
