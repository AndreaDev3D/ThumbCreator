using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PhotocaptureFromCamera
{
    /// <summary>
    /// Attach this to a Camera in your scene. Choose a <see cref="Filename"/> and <see cref="SaveDirectory"/>, then click "Capture & Save Image". 
    /// </summary> 
    [ExecuteInEditMode]
    public class Photocapture : MonoBehaviour
    {
        public const string CenterNamePostfixConvention = " Center";

        // The fields are ordered as they appear in the Inspector (look at CameraPhotoCaptureEditor.OnInspectorGUI).
        public string SaveDirectory;
        public string Filename;
        public string FilenamePostfix;
        public bool OverwriteFile = false;
        public string NumberingDelimiter;
        public Resolution PhotoResolution = Resolution.Res128;
        public FileType FileType = FileType.png;

        private Transform previousLockTarget;
        private GameObject lockTargetCenter;
        public Transform LockTarget;

        public bool UseTargetAsFilename = false;
        public Vector3 Offset = Vector3.zero;
        public float Distance = 0f;
        public bool UseUnlitShader = false;

        private RawImage previewImage;
        private Canvas canvas;

        private void OnValidate()
        {
            // Maybe I could be doing this instead:
            // https://stackoverflow.com/questions/37958136/unity-c-how-script-know-when-public-variablenot-property-changed
            bool userSwitchedTargets = LockTarget != previousLockTarget;
            if (userSwitchedTargets)
            {
                UpdateLockTargetCenter();

                void UpdateLockTargetCenter()
                {
                    DestroyCenterObjectIfExists(previousLockTarget, true);
                    EditorApplication.delayCall += () =>
                        lockTargetCenter = CreateCenterObjectIfDoesntExist(LockTarget, lockTargetCenter);
                }

                bool newTargetIsNull = LockTarget == null;
                if (newTargetIsNull)
                    ResetTargetDependentState();

                void ResetTargetDependentState()
                {
                    UseTargetAsFilename = false;
                    Offset = Vector3.zero;
                    Distance = 0f;
                }

                previousLockTarget = LockTarget;
            }
        }

        private void OnEnable()
        {
            if (LockTarget != null)
                lockTargetCenter = CreateCenterObjectIfDoesntExist(LockTarget, lockTargetCenter);

            CreatePreviewImageInfrastructure();

            void CreatePreviewImageInfrastructure()
            {
                canvas = FindObjectOfType<Canvas>();
                if (canvas == null)
                {
                    GameObject canvasGO = new("Photocapture Preview Canvas");
                    canvas = canvasGO.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasGO.AddComponent<CanvasScaler>();
                }

                float previewSize = 300f;

                GameObject rawImageGO = new GameObject("Photocapture Preview Image");
                rawImageGO.transform.SetParent(canvas.transform, false);
                var rawImageTransform = rawImageGO.AddComponent<RectTransform>();
                SetLocation(rawImageTransform, previewSize);
                previewImage = rawImageGO.AddComponent<RawImage>();

                GameObject textGO = new GameObject("Photocapture Preview Text");
                textGO.transform.SetParent(canvas.transform, false);
                var textRectTransform = textGO.AddComponent<RectTransform>();
                SetLocation(textRectTransform, previewSize);

                EditorApplication.delayCall += () =>
                {
                    var textComponent = textGO.AddComponent<Text>();
                    textComponent.fontSize = 24;
                    textComponent.text = "Photocapture Preview:";
                };

                static void SetLocation(RectTransform rectTransform, float size)
                {
                    rectTransform.anchorMin = Vector2.right;
                    rectTransform.anchorMax = Vector2.right;
                    rectTransform.pivot = Vector2.right;
                    rectTransform.sizeDelta = size * Vector2.one;
                }
            }
        }

        private void OnDisable()
        {
            DestroyCenterObjectIfExists(LockTarget, false);
            DestroyPreviewImageInfrastructureIfExists();

            void DestroyPreviewImageInfrastructureIfExists()
            {
                if (canvas != null)
                {
                    if (previewImage != null)
                    {
                        DestroyImmediate(previewImage.gameObject);
                        previewImage = null;
                    }
                    if (canvas.gameObject.name == "Photocapture Preview Canvas")
                    {
                        DestroyImmediate(canvas.gameObject);
                        canvas = null;
                    }

                }
            }
        }

        private void Update()
        {
            if (!TryGetComponent(out Camera camera))
            {
                Debug.Log("ERROR: CameraPhotoCapture component is not attached to a Camera... please fix this...");
                return;
            }

            if (LockTarget != null && lockTargetCenter != null)
                LockToAndOrbitTarget();

            void LockToAndOrbitTarget()
            {
                if (LockTarget.TryGetComponent<Renderer>(out var renderer))
                {
                    camera.transform.position = lockTargetCenter.transform.position
                        + new Vector3(0f, 0f, Distance + renderer.bounds.size.magnitude);
                }
                else
                {
                    camera.transform.position = lockTargetCenter.transform.position
                        + new Vector3(0f, 0f, Distance);
                }
                camera.transform.LookAt(lockTargetCenter.transform.position);
                camera.transform.position += Offset;
            }

            UpdatePreviewImage();

            void UpdatePreviewImage()
            {
                if (LockTarget != null && UseUnlitShader)
                {
                    Shader unlit = GetUnlitShader();
                    var targetsRenderer = LockTarget.GetComponent<MeshRenderer>();
                    Shader original = targetsRenderer.sharedMaterial.shader;
                    targetsRenderer.sharedMaterial.shader = unlit;
                    previewImage.texture = GenerateImage(Camera.main, (int)PhotoResolution);
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
                    previewImage.texture = GenerateImage(camera, (int)PhotoResolution);

                static Texture2D GenerateImage(Camera camera, int resolution)
                {
                    RenderTexture renderTexture = new(resolution, resolution, 32);
                    RenderCameraToTexture(camera, renderTexture);
                    Texture2D image = ReadPixelsToTexture(resolution);
                    RenderCameraToTexture(camera, null);
                    DestroyImmediate(renderTexture);
                    return image;

                    static Texture2D ReadPixelsToTexture(int resolution)
                    {
                        // Pixels are read from RenderTexture.active.
                        var texture = new Texture2D(resolution, resolution);
                        texture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                        texture.Apply();
                        return texture;
                    }

                    static void RenderCameraToTexture(Camera camera, RenderTexture texture)
                    {
                        // If texture is null, it renders to Main Window.
                        camera.targetTexture = texture;
                        RenderTexture.active = texture;
                        camera.Render();
                    }
                }
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

            SaveToFile(previewImage.texture, SaveDirectory, filename + FilenamePostfix, FileType, OverwriteFile, NumberingDelimiter);

            static void SaveToFile(Texture texture, string path, string filename, FileType filetype,
                bool overwriteFile, string numberingDelimiter)
            {
                var texture2D = texture as Texture2D;
                PhotoImporter.SaveDirectory = "Assets/" + path;
                string assetsPath = Path.Combine("Assets", path);
                string fullPath = Path.Combine(assetsPath, filename + "." + filetype.ToString());
                byte[] bytes = texture2D.EncodeToPNG();
                Directory.CreateDirectory(assetsPath);
                if (File.Exists(fullPath) && !overwriteFile)
                {
                    int counter = 1;
                    while (File.Exists(fullPath))
                    {
                        fullPath = Path.Combine(assetsPath, filename + numberingDelimiter + counter + "." + filetype);
                        counter++;
                    }
                }
                File.WriteAllBytes(fullPath, bytes);
                Debug.Log(bytes.Length / 1024 + "Kb was saved as: " + fullPath);
                AssetDatabase.Refresh();
            }
        }

        private static GameObject CreateCenterObjectIfDoesntExist(Transform target, GameObject center)
        {
            if (target == null)
                return null;

            if (target.TryGetComponent<MeshRenderer>(out var renderer))
            {
                if (center == null || center.name != target.name + CenterNamePostfixConvention)
                {
                    center = new GameObject(target.name + CenterNamePostfixConvention);
                    center.transform.position = renderer.bounds.center;
                    center.transform.parent = target.transform;
                }
            }
            else
            {
                Debug.Log("Target's MeshRenderer was null! Failed to create a \"Center Object\" to focus on...");
                return null;
            }
            return center;
        }

        private static void DestroyCenterObjectIfExists(Transform target, bool delayDestruction)
        {
            if (target == null)
                return;

            var center = GameObject.Find(target.name + CenterNamePostfixConvention);
            if (center != null)
                if (delayDestruction)
                    EditorApplication.delayCall += () => DestroyImmediate(center);
                else
                    DestroyImmediate(center);
        }
    }

    /// <summary>
    /// This class is for making <see cref="Photocapture"/> fields editable by game developers in a pleasant way. 
    /// It does not need to be manually instantiated or attached to anything to be used.
    /// </summary>
    [CustomEditor(typeof(Photocapture))]
    public class PhotocaptureEditor : Editor
    {
        private bool showInstructions = false;
        private const string instructions =
            "- Consider setting up a new 'photobooth' scene with manually placed background (or foreground) props.\n" +
            "- If you have a target, you can rotate it around to take photos from different angles.\n" +
            "- To get transparent backgrounds, while in an empty scene set Camera's ClearFlag to Color, then set color to black with max alpha.\n" +
            "- Enable UseUnlitShader if you are generating icons for items and don't want to mess with lighting.\n" +
            "- Consider adjusting the Camera's FieldOfView to achieve the desired perspective.";

        private SerializedProperty saveDirectory;
        private SerializedProperty filename;

        private bool showAdvanced = false;
        private SerializedProperty filenamePostfix;
        private SerializedProperty overwriteFile;
        private SerializedProperty numberingDelimiter;
        private SerializedProperty photoResolution;
        private SerializedProperty fileType;

        private SerializedProperty lockTarget;
        private SerializedProperty useTargetAsFilename;

        private SerializedProperty offset;
        private SerializedProperty distance;
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

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced:", true);
            if (showAdvanced)
            {
                EditorGUILayout.PropertyField(filenamePostfix, new GUIContent("Filename Postfix", "The postfix appended to the end of the filename, e.g., _icon, -icon, etc."));
                EditorGUILayout.PropertyField(overwriteFile, new GUIContent("Overwrite File", "If enabled, it overwrites the existing file with the same name."));

                if (!overwriteFile.boolValue)
                    EditorGUILayout.PropertyField(numberingDelimiter, new GUIContent("Numbering Delimiter", "The delimiter to append to the filename for numbering scheme."));

                EditorGUILayout.PropertyField(photoResolution, new GUIContent("Photo Resolution", "The resolution of the captured image."));
                EditorGUILayout.PropertyField(fileType, new GUIContent("File Type", "The file format of the captured image."));
            }

            EditorGUILayout.Space();

            GUIStyle sectionStyle = new(EditorStyles.helpBox);
            sectionStyle.normal.background = Texture2D.grayTexture;
            sectionStyle.margin = new RectOffset(10, 10, 5, 5);
            EditorGUILayout.BeginVertical(sectionStyle);

            EditorGUILayout.ObjectField(lockTarget, new GUIContent("Target", "The target the camera is set to focus on. Must have a MeshRenderer."));

            if (photoCapture.LockTarget != null)
            {
                EditorGUILayout.PropertyField(useTargetAsFilename);
                EditorGUILayout.PropertyField(offset, new GUIContent("Offset", "The amount that the camera is offset from the target."));
                EditorGUILayout.Slider(distance, 0, 2f, new GUIContent("Target Distance", "The distance the camera is away from the target."));
                EditorGUILayout.PropertyField(useUnlitShader, new GUIContent("Use Unlit Shader", "If enabled, the saved image will use the Unlit shader for the Target object. Scriptable Render Pipeline not supported."));
            }

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Capture & Save Image"))
            {
                bool useTargetName = photoCapture.LockTarget != null && photoCapture.UseTargetAsFilename;
                photoCapture.CapturePhoto(useTargetName ? photoCapture.LockTarget.name : photoCapture.Filename);
            }

            EditorGUILayout.Space();
            showInstructions = EditorGUILayout.Foldout(showInstructions, "Usage Tips:", true);
            if (showInstructions)
                EditorGUILayout.HelpBox(instructions, MessageType.Info);
        }
    }

    /// <summary>
    /// This <see cref="SaveDirectory"/> is overwritten by <see cref="Photocapture.SaveDirectory"/> on image save.
    /// It does not need to be instantiated or attached to anything to be used, all it does is make sure
    /// transparency is saved. 
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