﻿using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PhotocaptureFromCamera
{
    /// <summary>
    /// Attach this to a Camera in your scene. Choose a <see cref="Filename"/> and <see cref="SaveDirectory"/>, 
    /// then click "Capture & Save Image". 
    /// 
    /// Usage Tips:
    /// - Consider setting up a new "photobooth" scene with manually placed background (or foreground) props.
    /// - If you have a custom background or foreground, it's usually easier to rotate your target manually than to orbit the camera around.
    /// - To get transparent backgrounds, while in an empty scene set <see cref="Camera.clearFlags"/> to <see cref="CameraClearFlags.Color"/> and then set <see cref="Camera.backgroundColor"/> to black with max alpha.
    /// - Enable <see cref="UseUnlitShader"/> if you are generating icons for items and don't want to mess with lighting.
    /// - You can doubleclick image files in the Unity Project Explorerer inside the Unity Editor to check the results.
    /// - If you want to unlock the camera from a Target, simply click the circle symbol to the right of Target, and select "None".
    /// - Set the Game View resolution to be whatever your target is to preview your image (not counting transparency).
    /// - Consider adjusting the <see cref="Camera.fieldOfView"/> to achieve the desired perspective.
    /// </summary>    
    [ExecuteInEditMode]
    public class Photocapture : MonoBehaviour
    {
        // The fields are ordered as they appear in the Inspector (look at CameraPhotoCaptureEditor.OnInspectorGUI).
        public string SaveDirectory;
        public string Filename;
        public string FilenamePostfix;
        public bool OverwriteFile = false;
        public string PostfixDelimiter; // TODO: Input validation on PostfixDelimiter (no . / or \)
        public Resolution PhotoResolution = Resolution.Res128;
        public FileType FileType = FileType.png;

        private Transform previousLockTarget;
        public Transform LockTarget;

        public bool UseTargetAsFilename = false;
        public Vector3 Offset;
        public float Distance = 1f;
        public float HorizontalOrbit = 0f;
        public float VerticalOrbit = 0f;
        public bool UseUnlitShader = false;

        private bool cameraFocusedOnCenter = false;
        public bool GetCameraFocusedOnCenter() => cameraFocusedOnCenter;
        private void SetCameraFocusOnCenter(bool value, bool delay)
        {
            if (value)
                CreateCenterObjectIfDoesntExist(LockTarget);
            else
                DestroyCenterObjectIfExists(LockTarget, delay);
            cameraFocusedOnCenter = value;
        }

        private void OnValidate()
        {
            // This is basically a hack to run logic whenever lockTarget changes.
            // Simply using a property won't work, because they can't be serialized...
            bool userSwitchedTargets = LockTarget != previousLockTarget;
            if (userSwitchedTargets)
            {
                DestroyCenterObjectIfExists(previousLockTarget, true);
                bool previousTargetExisted = previousLockTarget != null;
                if (previousTargetExisted)
                    SetCameraFocusOnCenter(false, true);

                bool newTargetIsNull = LockTarget == null;
                if (newTargetIsNull)
                {
                    UseTargetAsFilename = false;
                    Offset = Vector3.zero;
                    Distance = 1f;
                    HorizontalOrbit = 0f;
                    VerticalOrbit = 0f;
                    SetCameraFocusOnCenter(false, true);
                }

                previousLockTarget = LockTarget;
            }
        }

        private void OnEnable()
        {
            if (cameraFocusedOnCenter)
                CreateCenterObjectIfDoesntExist(LockTarget);
        }

        private void OnDisable() => DestroyCenterObjectIfExists(LockTarget, false);

        private void Update()
        {
            if (!TryGetComponent(out Camera camera))
            {
                Debug.Log("ERROR: CameraPhotoCapture component is not attached to a Camera... please fix this...");
                return;
            }

            if (LockTarget != null)
            {
                Transform target = cameraFocusedOnCenter ? GameObject.Find(LockTarget.name + " Center").transform : LockTarget;

                if (LockTarget.TryGetComponent<Renderer>(out var renderer))
                    camera.transform.position = target.position + new Vector3(0f, 0f, Distance + renderer.bounds.extents.magnitude);
                else
                    camera.transform.position = target.position + new Vector3(0f, 0f, Distance);

                camera.transform.RotateAround(target.position, Vector3.up, HorizontalOrbit);
                camera.transform.RotateAround(target.position, target.right, VerticalOrbit);
                camera.transform.LookAt(target.position);
                camera.transform.position += Offset;
            }
        }

        public void CapturePhoto(string filename)
        {
            if (filename.Length == 0)
            {
                Debug.Log("ERROR: Filename is empty... image generation failed...");
                return;
            }

            if (!isActiveAndEnabled)
            {
                Debug.Log("ERROR: Component is disabled... image generation failed...");
                return;
            }

            Texture2D image;

            if (LockTarget != null && UseUnlitShader)
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

            SaveToFile(image, SaveDirectory, filename + FilenamePostfix, FileType, OverwriteFile, PostfixDelimiter);

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

                static Texture2D ReadPixelsToTexture(int resolution)
                {
                    // Pixels are read from RenderTexture.active.
                    var texture = new Texture2D(resolution, resolution);
                    texture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                    texture.Apply();
                    return texture;
                }
            }

            static void SaveToFile(Texture2D texture, string path, string filename, FileType filetype,
                bool overwriteFile, string postfixDelimiter)
            {
                PhotoImporter.SaveDirectory = "Assets/" + path;
                string assetsPath = Path.Combine("Assets", path);
                string fullPath = Path.Combine(assetsPath, filename + "." + filetype.ToString());
                byte[] bytes = texture.EncodeToPNG();
                Directory.CreateDirectory(assetsPath);
                if (File.Exists(fullPath) && !overwriteFile)
                {
                    int counter = 1;
                    while (File.Exists(fullPath))
                    {
                        fullPath = Path.Combine(assetsPath, filename + postfixDelimiter + counter + "." + filetype);
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
            if (LockTarget == null)
            {
                Debug.Log("No camera target... Center Toggle Focus failed...");
                return;
            }

            if (!isActiveAndEnabled)
            {
                Debug.Log("ERROR: Component is disabled... Center Toggle Focus failed...");
                return;
            }

            SetCameraFocusOnCenter(!cameraFocusedOnCenter, false);
        }

        private static void CreateCenterObjectIfDoesntExist(Transform target)
        {
            if (target != null)
            {
                if (target.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    var centerObject = GameObject.Find(target.name + " Center");
                    if (centerObject == null)
                    {
                        centerObject = new GameObject(target.name + " Center");
                        centerObject.transform.position = renderer.bounds.center;
                        centerObject.transform.parent = target.transform;
                    }
                }
                else
                    Debug.Log("Target's MeshRenderer was null! Failed to create a \"Center Object\" to focus on...");
            }
        }

        private void DestroyCenterObjectIfExists(Transform target, bool delay)
        {
            if (target != null)
            {
                var centerObject = GameObject.Find(target.name + " Center");
                if (centerObject != null)
                {
                    if (delay)
                        EditorApplication.delayCall += () => DestroyImmediate(centerObject);
                    else
                        DestroyImmediate(centerObject);
                }
            }
        }
    }

    /// <summary>
    /// This class is for making <see cref="Photocapture"/> fields editable by game developers in a pleasant way. 
    /// It does not need to be manually instantiated or attached to anything to be used.
    /// </summary>
    [CustomEditor(typeof(Photocapture))]
    public class PhotocaptureEditor : Editor
    {
        private SerializedProperty saveDirectory;
        private SerializedProperty filename;
        private SerializedProperty filenamePostfix;
        private SerializedProperty overwriteFile;
        private SerializedProperty postfixDelimiter;
        private SerializedProperty photoResolution;
        private SerializedProperty fileType;

        private SerializedProperty lockTarget;
        private SerializedProperty useTargetAsFilename;

        private SerializedProperty offset;
        private SerializedProperty distance;
        private SerializedProperty horizontalOrbit;
        private SerializedProperty verticalOrbit;
        private SerializedProperty useUnlitShader;

        private void OnEnable()
        {
            AssignFieldsAccordingToName();

            void AssignFieldsAccordingToName()
            {
                string[] fieldNames = Array.ConvertAll(typeof(Photocapture).GetFields(), field => field.Name);
                foreach (var fieldName in fieldNames)
                {
                    string fieldNameCamelCase = char.ToLower(fieldName[0]) + fieldName.Substring(1);
                    FieldInfo fieldInfo = typeof(PhotocaptureEditor).GetField(fieldNameCamelCase,
                        bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
                    fieldInfo?.SetValue(this, serializedObject.FindProperty(fieldName));
                }
            }
        }

        public override void OnInspectorGUI()
        {
            var photoCapture = target as Photocapture;
            serializedObject.Update();

            EditorGUILayout.PropertyField(saveDirectory, new GUIContent("Save Directory", "The directory (relative to Assets folder) to save the captured images."));

            if (photoCapture.LockTarget == null || !useTargetAsFilename.boolValue)
                EditorGUILayout.PropertyField(filename, new GUIContent("Filename", "The name of the captured image file, not including extension."));

            EditorGUILayout.PropertyField(filenamePostfix, new GUIContent("Filename Postfix", "The postfix appended to the end of the filename, e.g., _icon, -icon, etc."));

            EditorGUILayout.PropertyField(overwriteFile, new GUIContent("Overwrite File", "If enabled, it overwrites the existing file with the same name."));

            if (!overwriteFile.boolValue)
                EditorGUILayout.PropertyField(postfixDelimiter, new GUIContent("Postfix Delimiter", "The delimiter to append to the filename when generating a unique filename."));

            EditorGUILayout.PropertyField(photoResolution, new GUIContent("Photo Resolution", "The resolution of the captured image."));
            EditorGUILayout.PropertyField(fileType, new GUIContent("File Type", "The file format of the captured image."));

            ShowInInspector(serializedObject, lockTarget, true, new GUIContent("Target", "The target the camera is set to focus and orbit around."));

            if (photoCapture.LockTarget != null)
            {
                EditorGUILayout.PropertyField(useTargetAsFilename);
                EditorGUILayout.PropertyField(offset, new GUIContent("Offset", "The amount that the camera is offset from the target."));
                EditorGUILayout.Slider(distance, 0, 2f, new GUIContent("Target Distance", "The distance the camera is away from the target."));
                EditorGUILayout.Slider(horizontalOrbit, -180f, 180f, new GUIContent("Horizontal Orbit Angle", "The angle to orbit around the target horizontally."));
                EditorGUILayout.Slider(verticalOrbit, -180f, 180f, new GUIContent("Vertical Orbit Angle", "The angle to orbit around the target vertically."));
                EditorGUILayout.PropertyField(useUnlitShader, new GUIContent("Use Unlit Shader", "If enabled, the saved image will use the Unlit shader for the Target object. Scriptable Render Pipeline not supported."));

                if (GUILayout.Button("Center Focus Toggle (Recommended)"))
                    photoCapture.ToggleFocusBetweenPivotAndCenter();

                EditorGUILayout.LabelField("Locked to: ",
                    !photoCapture.GetCameraFocusedOnCenter() ? photoCapture.LockTarget.name : photoCapture.LockTarget.name + " Center");
            }
            else
                EditorGUILayout.LabelField("Locked to: ", "Nothing");

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Capture & Save Image"))
            {
                bool useTargetName = photoCapture.LockTarget != null && photoCapture.UseTargetAsFilename;
                photoCapture.CapturePhoto(useTargetName ? photoCapture.LockTarget.name : photoCapture.Filename);
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

    /// <summary>
    /// This class's SaveDirectory is overwritten by Photocapture. It does not need to be instantiated to be used.
    /// </summary>
    public class PhotoImporter : AssetPostprocessor
    {
        public static string SaveDirectory;

        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(SaveDirectory))
            {
                var textureImporter = assetImporter as TextureImporter;
                textureImporter.alphaIsTransparency = true;
            }
        }
    }

    /// <summary>
    /// The supported resolutions that <see cref="Photocapture"/> supports for image generation.
    /// </summary>
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

    /// <summary>
    /// The supported filetype (extensions) that <see cref="Photocapture"/> supports for image generation.
    /// </summary>
    public enum FileType : short
    {
        png,
        jpg
    }
}