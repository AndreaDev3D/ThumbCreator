using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PhotocaptureFromCamera
{
    /// <summary>
    /// Attach this to a Camera in your scene. Choose a <see cref="Filename"/> and <see cref="SaveDirectory"/>, 
    /// then click "Capture & Save Image". Consider setting up a new "photobooth" scene to do this all in, manually 
    /// placing background (or foreground) props. For transparent backgrounds, change the Camera component fields: 
    /// <see cref="CameraClearFlags.Color"/>  and <see cref="Camera.backgroundColor"/> to maximum alpha.
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

        [SerializeField]
        private Transform _lockTarget;
        public Transform LockTarget
        {
            get { return _lockTarget; }
            set
            {
                if (_lockTarget != value)
                {
                    DestroyCenterObjectIfNecessary(_lockTarget);
                    if (LockToTarget)
                        CenterCamera = false;
                }
                _lockTarget = value;
            }
        }

        public bool UseTargetAsFilename = false;
        public float Distance = 5f;
        public float HorizontalOrbit = 0f;
        public float VerticalOrbit = 0f;
        public bool UseUnlitShader = false;

        private bool _centerCamera = false;
        public bool CenterCamera
        {
            get => _centerCamera;
            private set
            {
                if (value)
                    CreateCenterObjectIfNecessary(LockTarget);
                else
                    DestroyCenterObjectIfNecessary(LockTarget);
                _centerCamera = value;
            }
        }

        private void OnValidate() => LockTarget = _lockTarget;

        private void OnEnable()
        {
            CreateCenterObjectIfNecessary(LockTarget);
            ValidateCameraAndSetTargetIfNeeded();
        }

        private void OnDisable() => DestroyCenterObjectIfNecessary(LockTarget);

        private void Update() => ValidateCameraAndSetTargetIfNeeded();

        private void ValidateCameraAndSetTargetIfNeeded()
        {
            if (!TryGetComponent<Camera>(out Camera camera))
            {
                Debug.Log("ERROR: CameraPhotoCapture component is not attached to a Camera... please fix this...");
                return;
            }

            if (LockToTarget && LockTarget != null)
            {
                SetCameraPosition(camera,
                    CenterCamera ? GameObject.Find(LockTarget.name + " Center").transform : LockTarget,
                    Distance, HorizontalOrbit, VerticalOrbit);
            }
        }

        private static void SetCameraPosition(Camera camera, Transform target, float distance, float horizontalOrbit, float verticalOrbit)
        {
            if (camera == null || target == null)
            {
                Debug.Log("ERROR: Camera or target is null... setting camera position failed...");
                return;
            }

            camera.transform.position = target.position + new Vector3(0f, 0f, distance);
            camera.transform.RotateAround(target.position, Vector3.up, horizontalOrbit);
            camera.transform.RotateAround(target.position, target.right, verticalOrbit);
            camera.transform.LookAt(target.position);
        }

        public void CapturePhoto(string filename)
        {
            if (filename.Length == 0)
            {
                Debug.Log("ERROR: Filename is empty... image generation failed...");
                return;
            }

            if (LockToTarget && LockTarget == null)
            {
                Debug.Log("ERROR: No LockTarget despite LockToTarget being enabled... image generation failed...");
                return;
            }

            if (!isActiveAndEnabled)
            {
                Debug.Log("ERROR: Component is disabled... image generation failed...");
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
                    // Pixels are read from RenderTexture.active.
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
            if (LockTarget == null)
            {
                Debug.Log("No camera target... toggle to center focus failed...");
                return;
            }

            if (!isActiveAndEnabled)
            {
                Debug.Log("ERROR: Component is disabled... toggle to center focus failed...");
                return;
            }

            CenterCamera = !CenterCamera;
            SceneView.RepaintAll();
        }

        public static void CreateCenterObjectIfNecessary(Transform target)
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
                    }
                }
                else
                    Debug.Log("Target's MeshRenderer was null! Failed to create a \"Center Object\" to focus on...");
            }
        }

        public static void DestroyCenterObjectIfNecessary(Transform target)
        {
            if (target != null)
            {
                var centerObject = GameObject.Find(target.name + " Center");
                if (centerObject != null)
                    DestroyImmediate(centerObject);
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
            AssignFieldsAccordingToName();

            void AssignFieldsAccordingToName()
            {
                string[] fieldNames = Array.ConvertAll(typeof(Photocapture).GetFields(), field => field.Name);
                foreach (var fieldName in fieldNames)
                {
                    string fieldNameCamelCase = char.ToLower(fieldName[0]) + fieldName.Substring(1);
                    FieldInfo fieldInfo = typeof(PhotocaptureEditor).GetField(fieldNameCamelCase, BindingFlags.NonPublic | BindingFlags.Instance);
                    fieldInfo?.SetValue(this, serializedObject.FindProperty(fieldName));
                }
                // _lockTarget is a private field, so it's missed by the loop.
                lockTarget = serializedObject.FindProperty("_lockTarget");
            }
        }

        public override void OnInspectorGUI()
        {
            var photoCapture = target as Photocapture;
            serializedObject.Update();

            EditorGUILayout.PropertyField(saveDirectory, new GUIContent("Save Directory", "The directory (relative to Assets folder) to save the captured images."));

            if (lockToTarget.boolValue == false || useTargetAsFilename.boolValue == false)
                EditorGUILayout.PropertyField(filename, new GUIContent("Filename", "The name of the captured image file, not including extension."));

            EditorGUILayout.PropertyField(overwriteFile, new GUIContent("Overwrite File", "If enabled, it overwrites the existing file with the same name."));

            if (!overwriteFile.boolValue)
                EditorGUILayout.PropertyField(postfixDelimiter, new GUIContent("Postfix Delimiter", "The delimiter to append to the filename when generating a unique filename."));

            EditorGUILayout.PropertyField(photoResolution, new GUIContent("Photo Resolution", "The resolution of the captured image."));
            EditorGUILayout.PropertyField(fileType, new GUIContent("File Type", "The file format of the captured image."));

            EditorGUILayout.PropertyField(lockToTarget, new GUIContent("Lock To Target", "If enabled, the camera will be locked orbit and focus on a target, which you set below."));

            if (lockToTarget.boolValue)
                ShowInInspector(serializedObject, lockTarget);

            if (lockToTarget.boolValue && photoCapture.LockTarget != null)
            {
                EditorGUILayout.PropertyField(useTargetAsFilename);
                EditorGUILayout.Slider(distance, 0, 10, new GUIContent("Target Distance", "The distance the camera is away from the target."));
                EditorGUILayout.Slider(horizontalOrbit, -180f, 180f, new GUIContent("Horizontal Orbit Angle", "The angle to orbit around the target horizontally."));
                EditorGUILayout.Slider(verticalOrbit, -180f, 180f, new GUIContent("Vertical Orbit Angle", "The angle to orbit around the target vertically."));
                EditorGUILayout.PropertyField(useUnlitShader, new GUIContent("Use Unlit Shader", "If enabled, the saved image will use the Unlit shader for the Target object. Scriptable Render Pipeline not supported."));

                if (GUILayout.Button("Center Focus Toggle (Recommended)"))
                    photoCapture.ToggleFocusBetweenPivotAndCenter();

                EditorGUILayout.LabelField("Locked to: ",
                    !photoCapture.CenterCamera ? photoCapture.LockTarget.name : photoCapture.LockTarget.name + " Center");
            }
            else
                EditorGUILayout.LabelField("Locked to: ", "Nothing");

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Capture & Save Image"))
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