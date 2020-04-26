using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ThumbManager))]
public class ThumbManagerEditor : Editor
{
    ThumbManager _target;
    void OnEnable()
    {
        _target = (ThumbManager)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(20);
        if (GUILayout.Button($"Generate {_target.ExportFile}", GUILayout.Height(30)))
        {
            _target.Take();
        }
    }
}
