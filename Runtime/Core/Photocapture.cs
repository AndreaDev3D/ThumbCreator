using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PhotocaptureFromCamera
{
    /// <summary>
    /// Attach this to a Camera in your scene. Choose a Filename and SaveDirectory, then click "Generate Image".
    /// </summary>
    [ExecuteInEditMode]
    public class Photocapture : MonoBehaviour
    {
        // The fields are ordered as they appear in the Inspector (look at CameraPhotoCaptureEditor.OnInspectorGUI).
        public string SaveDirectory;
        public string Filename;
        public bool OverwriteFile = false;
        public string PostfixDelimiter;
        public Resolution PhotoResolution = Resolution.Res128;
        public FileType FileType = FileType.png;

        public bool LockToTarget = false;
        public Transform LockTarget;
        public bool UseTargetAsFilename = false;
        public float Distance = 5f;
        public float HorizontalOrbit = 0f;
        public float VerticalOrbit = 0f;
        public bool UseUnlitShader = false;

        private bool _centerCamera = false;
        public bool CenterCamera => _centerCamera;

        private void OnEnable() => ValidateCameraAndSetTargetIfNeeded(); 

        private void Update() => ValidateCameraAndSetTargetIfNeeded();

        private void ValidateCameraAndSetTargetIfNeeded()
        {
            if (!TryGetComponent<Camera>(out Camera camera))
            {
                Debug.Log("CameraPhotoCapture component is not attached to a Camera... please fix this...");
                return;
            }

            if (LockToTarget && LockTarget != null)
            {
                SetCameraPosition(camera,
                    _centerCamera ? GameObject.Find(LockTarget.name + " Center").transform : LockTarget,
                    Distance, HorizontalOrbit, VerticalOrbit);
            }
        }

        private static void SetCameraPosition(Camera camera, Transform target, float distance, float horizontalOrbit, float verticalOrbit)
        {
            if (camera != null && target != null)
            {
                camera.transform.position = target.position + new Vector3(0f, 0f, -distance);
                camera.transform.RotateAround(target.position, target.up, horizontalOrbit);
                camera.transform.RotateAround(target.position, target.right, verticalOrbit);
                camera.transform.LookAt(target.position);
            }
        }

        public void CapturePhoto(string filename)
        {
            if (filename.Length == 0)
            {
                Debug.Log("Filename is empty! Image generation failed...");
                return;
            }

            Texture2D image;
            if (LockToTarget && UseUnlitShader)
            {
                Shader unlit = GetUnlitShader();
                var targetsRenderer = LockTarget.GetComponent<MeshRenderer>();
                Shader original = targetsRenderer.sharedMaterial.shader;
                targetsRenderer.material.shader = unlit;

                image = GenerateImage((int)PhotoResolution);

                targetsRenderer.sharedMaterial.shader = original;

                static Shader GetUnlitShader()
                {
                    string shaderName = "HDRP/UnLit";
                    Shader shader = Shader.Find(shaderName);
                    if (shader == null)
                    {
                        shaderName = "Universal Render Pipeline/Unlit";
                        shader = Shader.Find(shaderName);
                        if (shader == null)
                        {
                            shaderName = "Unlit/Texture";
                            shader = Shader.Find(shaderName);
                        }
                    }
                    return shader;
                }
            }
            else
                image = GenerateImage((int)PhotoResolution);

            SaveToFile(image, SaveDirectory, filename, FileType, OverwriteFile, PostfixDelimiter);

            static Texture2D GenerateImage(int resolution)
            {
                var renderTexture = new RenderTexture(resolution, resolution, 32);
                RenderCameraToTexture(renderTexture);
                Texture2D image = ReadPixelsToTexture(resolution);
                RenderCameraToTexture(null);
                DestroyImmediate(renderTexture);
                return image;

                static void RenderCameraToTexture(RenderTexture texture)
                {
                    // If texture is null, it renders to Main Window.
                    Camera.main.targetTexture = texture;
                    RenderTexture.active = texture;
                    Camera.main.Render();
                }

                static Texture2D ReadPixelsToTexture(int res)
                {
                    var texture = new Texture2D(res, res);
                    texture.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                    texture.Apply();
                    return texture;
                }
            }

            static void SaveToFile(Texture2D texture, string path, string filename, FileType filetype,
                bool overwriteFile, string postfixDelimiter)
            {
                string unityPath = Path.Combine("Assets", path);
                string fullPath = Path.Combine(unityPath, filename + "." + filetype.ToString());
                byte[] bytes = texture.EncodeToPNG();
                Directory.CreateDirectory(unityPath);
                if (File.Exists(fullPath) && !overwriteFile)
                {
                    int counter = 1;
                    while (File.Exists(fullPath))
                    {
                        fullPath = Path.Combine(unityPath, filename + postfixDelimiter + counter + "." + filetype);
                        counter++;
                    }
                }
                File.WriteAllBytes(fullPath, bytes);
                Debug.Log(bytes.Length / 1024 + "Kb was saved as: " + fullPath);
                AssetDatabase.Refresh();
            }
        }

        public void ToggleFocusBetweenPivotAndCenter()
        {
            if (!CenterCamera)
            {
                if (LockTarget.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    var newTarget = new GameObject(LockTarget.name + " Center");
                    newTarget.transform.position = renderer.bounds.center;
                    SetCameraPosition(GetComponent<Camera>(), newTarget.transform, Distance, HorizontalOrbit, VerticalOrbit);
                }
                else
                    Debug.Log("MeshRenderer was null! Failed to ToggleFocusPivotToCenter...");
            }
            else
            {
                if (LockTarget != null)
                {
                    DestroyImmediate(GameObject.Find(LockTarget.name + " Center"));
                    SetCameraPosition(GetComponent<Camera>(), LockTarget.transform, Distance, HorizontalOrbit, VerticalOrbit);
                }
            }
            _centerCamera = !_centerCamera;
            SceneView.RepaintAll();
        }
    }

    [CustomEditor(typeof(Photocapture))]
    public class PhotocaptureEditor : Editor
    {
        private SerializedProperty saveDirectory;
        private SerializedProperty filename;
        private SerializedProperty overwriteFile;
        private SerializedProperty postfixDelimiter;
        private SerializedProperty photoResolution;
        private SerializedProperty fileType;

        private SerializedProperty lockToTarget;
        private SerializedProperty lockTarget;
        private SerializedProperty useTargetAsFilename;

        private SerializedProperty distance;
        private SerializedProperty horizontalOrbit;
        private SerializedProperty verticalOrbit;
        private SerializedProperty useUnlitShader;

        private void OnEnable()
        {
            string[] fieldNames = Array.ConvertAll(typeof(Photocapture).GetFields(), field => field.Name);
            foreach (var fieldName in fieldNames)
            {
                string fieldNameCamelCase = char.ToLower(fieldName[0]) + fieldName.Substring(1);
                FieldInfo fieldInfo = typeof(PhotocaptureEditor).GetField(fieldNameCamelCase, BindingFlags.NonPublic | BindingFlags.Instance);
                fieldInfo?.SetValue(this, serializedObject.FindProperty(fieldName));
            }
        }

        public override void OnInspectorGUI()
        {
            var photoCapture = target as Photocapture;
            serializedObject.Update();

            EditorGUILayout.PropertyField(saveDirectory);

            if (lockToTarget.boolValue == false || useTargetAsFilename.boolValue == false)
                EditorGUILayout.PropertyField(filename);

            EditorGUILayout.PropertyField(overwriteFile);
            if (!overwriteFile.boolValue)
                EditorGUILayout.PropertyField(postfixDelimiter);

            EditorGUILayout.PropertyField(photoResolution);
            EditorGUILayout.PropertyField(fileType);

            EditorGUILayout.PropertyField(lockToTarget);
            if (lockToTarget.boolValue)
                ShowInInspector(serializedObject, lockTarget);

            if (lockToTarget.boolValue && photoCapture.LockTarget != null)
            {
                EditorGUILayout.PropertyField(useTargetAsFilename);
                EditorGUILayout.Slider(distance, 0, 10, new GUIContent("Camera Distance To Target"));
                EditorGUILayout.Slider(horizontalOrbit, -180f, 180f, new GUIContent("Horizontal Orbit Angle"));
                EditorGUILayout.Slider(verticalOrbit, -180f, 180f, new GUIContent("Vertical Orbit Angle"));
                EditorGUILayout.PropertyField(useUnlitShader);

                if (GUILayout.Button("Center Focus Toggle (Recommended)"))
                    photoCapture.ToggleFocusBetweenPivotAndCenter();

                EditorGUILayout.LabelField("Locked to: ",
                    !photoCapture.CenterCamera ? photoCapture.LockTarget.name : photoCapture.LockTarget?.name + " Center");
            }
            else
                EditorGUILayout.LabelField("Locked to: ", "Nothing");

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Generate Image"))
            {
                photoCapture.CapturePhoto(photoCapture.UseTargetAsFilename ? photoCapture.LockTarget.name
                    : photoCapture.Filename);
            }
        }

        private static void ShowInInspector(SerializedObject serializedObject, SerializedProperty property,
            bool includeChildren = true, GUIContent label = null)
        {
            label ??= new GUIContent(property.displayName);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, label, includeChildren);
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();
        }
    }

    public enum Resolution : short
    {
        Res8 = 8,
        Res32 = 32,
        Res64 = 64,
        Res128 = 128,
        Res512 = 512,
        Res1024 = 1024,
        Res2048 = 2048,
        Res4096 = 4096,
        Res8192 = 8192
    }

    public enum FileType : short
    {
        png,
        jpg
    }
}