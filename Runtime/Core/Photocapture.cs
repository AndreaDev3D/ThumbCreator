#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// TODO: Have this use the least amount of Unity stuff as possible. Then can ported to Godot easier. 
// TODO: State machine with two states: free camera & Target camera

namespace PhotocaptureFromCamera
{
    /// <summary>
    /// Attach this to a Camera in your scene. Choose a <see cref="Filename"/> and <see cref="SaveDirectory"/>,
    /// then click "Save Image". 
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

        private MeshRenderer previousLockTarget;
        private GameObject lockTargetCenter;
        public MeshRenderer LockTarget;

        public bool OnlyRenderTarget = false;
        public bool UseTargetAsFilename = false;
        public bool TransparentBackground = false;
        public bool UseUnlitShader = false;
        public Vector3 Offset = Vector3.zero;
        public float Distance = 0f;

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

            CreatePreviewImageGameObjectInfrastructure();

            void CreatePreviewImageGameObjectInfrastructure()
            {
                canvas = FindObjectOfType<Canvas>();
                if (canvas == null)
                {
                    var canvasGO = new GameObject("Photocapture Preview Canvas");
                    canvas = canvasGO.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasGO.AddComponent<CanvasScaler>();
                }

                float previewSize = 300f;

                var rawImageGameObject = new GameObject("Photocapture Preview Image");
                rawImageGameObject.transform.SetParent(canvas.transform, false);
                var rawImageTransform = rawImageGameObject.AddComponent<RectTransform>();
                PlaceInCorner(rawImageTransform, previewSize);
                previewImage = rawImageGameObject.AddComponent<RawImage>();

                var textGameObject = new GameObject("Photocapture Preview Text");
                textGameObject.transform.SetParent(canvas.transform, false);
                var textRectTransform = textGameObject.AddComponent<RectTransform>();
                PlaceInCorner(textRectTransform, previewSize);

                EditorApplication.delayCall += () =>
                {
                    var textComponent = textGameObject.AddComponent<Text>();
                    textComponent.fontSize = 48;
                    textComponent.text = "Preview:";
                };

                static void PlaceInCorner(RectTransform rectTransform, float size)
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
            DestroyPreviewImageGameObjectInfrastructure();

            void DestroyPreviewImageGameObjectInfrastructure()
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

            string photoRenderLayerName = "Photo Render";
            UpdatePreviewImage();

            void UpdatePreviewImage()
            {
                CameraClearFlags? originalClearFlags = null; // Test this with Skybox & Background (make sure background transparency gets reset).
                Color? originalBackgroundColor = null;

                if (LockTarget != null && TransparentBackground)
                {
                    SetBackgroundTransparent();

                    void SetBackgroundTransparent()
                    {
                        originalClearFlags = camera.clearFlags;
                        originalBackgroundColor = camera.backgroundColor;
                        camera.clearFlags = CameraClearFlags.SolidColor;
                        camera.backgroundColor = new Color(camera.backgroundColor.r, camera.backgroundColor.g, camera.backgroundColor.b, 0);
                    }
                }

                int? originalLayer = null;
                int? originalCullingMask = null;

                if (LockTarget != null && OnlyRenderTarget)
                {
                    CullBackground();

                    void CullBackground()
                    {
                        originalLayer = LockTarget.gameObject.layer;
                        originalCullingMask = camera.cullingMask;
                        int photoRenderLayer = LayerMask.NameToLayer(photoRenderLayerName);
                        if (photoRenderLayer == -1)
                            photoRenderLayer = CreateNewLayer(photoRenderLayerName);

                        LockTarget.gameObject.layer = photoRenderLayer;
                        int photoRenderLayerMask = 1 << photoRenderLayer;
                        camera.cullingMask = photoRenderLayerMask;

                        static int CreateNewLayer(string layerName)
                        {
                            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                            SerializedProperty layers = tagManager.FindProperty("layers");

                            for (int i = 8; i < layers.arraySize; i++)
                            {
                                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                                if (layer.stringValue == "")
                                {
                                    layer.stringValue = layerName;
                                    tagManager.ApplyModifiedProperties();
                                    return i;
                                }
                            }

                            Debug.LogError("ERROR: All available layers are already used... please uncheck RenderTargetOnly...");
                            return -1;
                        }
                    }
                }

                // Whether condition is true or not, previewImage.texture is set.
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
                    var renderTexture = new RenderTexture(resolution, resolution, 32);
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

                if (LockTarget != null && TransparentBackground)
                {
                    if (originalClearFlags.HasValue && originalBackgroundColor.HasValue)
                    {
                        camera.clearFlags = originalClearFlags.Value;
                        camera.backgroundColor = originalBackgroundColor.Value;
                    }
                }

                if (LockTarget != null && OnlyRenderTarget)
                {
                    // Reset to original layer, destroy the temporary layer, and reset the cullingMask.
                    if (originalLayer.HasValue)
                        LockTarget.gameObject.layer = originalLayer.Value;
                    DestroyLayer(photoRenderLayerName);
                    if (originalCullingMask.HasValue)
                        camera.cullingMask = originalCullingMask.Value;

                    void DestroyLayer(string layerName)
                    {
                        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                        SerializedProperty layers = tagManager.FindProperty("layers");

                        for (int i = 8; i < layers.arraySize; i++)
                        {
                            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                            if (layer.stringValue == layerName)
                            {
                                layer.stringValue = "";
                                tagManager.ApplyModifiedProperties();
                                break;
                            }
                        }
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

            static void SaveToFile(Texture texture, string pathInAssetsFolder, string filename, FileType filetype,
                bool overwriteFile, string numberingDelimiter)
            {
                var texture2D = texture as Texture2D;
                string assetsPath = Path.Combine("Assets", pathInAssetsFolder);
                PhotoImporter.SaveDirectory = pathInAssetsFolder;
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

        private static GameObject CreateCenterObjectIfDoesntExist(MeshRenderer target, GameObject center)
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

        private static void DestroyCenterObjectIfExists(MeshRenderer target, bool delayDestruction)
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
        private bool showUsageTips = false;
        private const string usageTips =
            "- You may want to set up a new 'photobooth' scene with manually placed background/foreground props.\n" +
            "- If you have a target, you can rotate it around to take photos from different angles.\n" +
            "- Adjust the Camera's FieldOfView to achieve the desired perspective.\n" +
            "- For transparent icons, combine both OnlyRenderTarget and TransparentBackground.\n" +
            "- Enable UseUnlitShader if you are generating icons for items and don't want to mess with lighting.\n";

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
        private SerializedProperty onlyRenderTarget;
        private SerializedProperty transparentBackground;
        private SerializedProperty useUnlitShader;
        private SerializedProperty offset;
        private SerializedProperty distance;

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

            EditorGUILayout.PropertyField(saveDirectory, new GUIContent("Save Directory", "The directory (relative to Assets folder) to save the captured images. Highly recommended that you have a dedicated folder, because a custom AssetPostprocessor is ran in that folder to overwrite transparencies."));

            if (photoCapture.LockTarget == null || !useTargetAsFilename.boolValue)
                EditorGUILayout.PropertyField(filename, new GUIContent("Filename", "The name of the captured image file, not including extension."));

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced:", true);
            if (showAdvanced)
            {
                EditorGUILayout.PropertyField(filenamePostfix, new GUIContent("Filename Postfix", "The postfix appended to the end of the filename, e.g., _icon, -icon, etc."));
                EditorGUILayout.PropertyField(overwriteFile, new GUIContent("Overwrite File", "If enabled, it overwrites the existing file with the same name."));

                if (!overwriteFile.boolValue)
                    EditorGUILayout.PropertyField(numberingDelimiter, new GUIContent("Numbering Delimiter", "The delimiter to append to the filename for numbering scheme, e.g. name, name_1, name_2, or name, name-1, name-2, etc."));

                EditorGUILayout.PropertyField(photoResolution, new GUIContent("Photo Resolution", "The resolution of the captured image."));
                EditorGUILayout.PropertyField(fileType, new GUIContent("File Type", "The file format of the captured image."));
            }

            EditorGUILayout.Space();

            GUIStyle sectionStyle = new GUIStyle(EditorStyles.helpBox);
            sectionStyle.normal.background = Texture2D.grayTexture;
            sectionStyle.margin = new RectOffset(10, 10, 5, 5);
            EditorGUILayout.BeginVertical(sectionStyle);

            EditorGUILayout.ObjectField(lockTarget, new GUIContent("Target", "The target the camera is set to focus on. Must have a MeshRenderer."));

            if (photoCapture.LockTarget != null)
            {
                EditorGUILayout.PropertyField(useTargetAsFilename, new GUIContent("Use Target as Filename", "If enabled, the target's name will be used as the Filename."));
                EditorGUILayout.PropertyField(onlyRenderTarget, new GUIContent("Only Render Target", "If enabled, only the target will be rendered during photo. Often combined with Transparent Background."));
                EditorGUILayout.PropertyField(transparentBackground, new GUIContent("Transparent Background", "If enabled, the saved image will have a transparent background. May need to enable 'Only Render Target' first."));
                EditorGUILayout.PropertyField(useUnlitShader, new GUIContent("Use Unlit Shader", "If enabled, the saved image will use the Unlit shader for the Target object. Scriptable Render Pipeline not supported."));
                EditorGUILayout.PropertyField(offset, new GUIContent("Offset", "The amount that the camera is offset from the target."));
                EditorGUILayout.Slider(distance, 0, 2f, new GUIContent("Target Distance", "The distance the camera is away from the target."));
            }

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Save Image"))
            {
                bool useTargetName = photoCapture.LockTarget != null && photoCapture.UseTargetAsFilename;
                photoCapture.CapturePhoto(useTargetName ? photoCapture.LockTarget.name : photoCapture.Filename);
            }

            EditorGUILayout.Space();
            showUsageTips = EditorGUILayout.Foldout(showUsageTips, "Usage Tips:", true);
            if (showUsageTips)
                EditorGUILayout.HelpBox(usageTips, MessageType.Info);
        }
    }

    /// <summary>
    /// This class is for setting alpha to act as transparency for saved images. 
    /// It does not need to be manually instantiated or attached to anything to be used.
    /// </summary>
    public class PhotoImporter : AssetPostprocessor
    {
        // Initial value is hack to prevent accidental re-import of entire project. Overwritten on "Save Image" click.
        public static string SaveDirectory = "DEFAULT_PHOTOCAPTURE_SAVE_LOCATION";

        private void OnPreprocessTexture()
        {
            string assetFullPath = Path.GetDirectoryName(Path.GetFullPath(assetPath)).Replace('\\', '/');
            string saveDirectoryFullPath = Path.Combine(Application.dataPath, SaveDirectory).Replace('\\', '/');
            bool assetIsInSaveDirectory = assetFullPath == saveDirectoryFullPath;
            if (assetIsInSaveDirectory)
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
#endif