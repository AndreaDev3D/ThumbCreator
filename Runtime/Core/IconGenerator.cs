#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// TODO: State management via state machine with "Free" and "Target" states?

namespace LightweightIconGenerator
{
    /// <summary>
    /// Attach this to a Camera in your scene. Choose a <see cref="Filename"/> and <see cref="SaveDirectory"/>,
    /// then click "Save Image". 
    /// </summary> 
    [ExecuteInEditMode]
    public class IconGenerator : MonoBehaviour
    {
        // Must-have:
        public string SaveDirectory;
        public string Filename;

        // Advanced:
        public string FilenamePostfix;
        public bool OverwriteFile = false;
        public string NumberingDelimiter;
        public Resolution PhotoResolution = Resolution.Res128;
        public FileType FileType = FileType.png;

        // Target Mode:
        public MeshRenderer LockTarget;
        public bool OnlyRenderTarget = false;
        public bool UseTargetAsFilename = false;
        public bool TransparentBackground = false;
        public bool UseUnlitShader = false;
        public Vector3 Offset = Vector3.zero;
        public float Distance = 0f;

        // Target Mode (not serialized):
        private MeshRenderer previousLockTarget;
        private GameObject lockTargetCenter;
        public const string CenterNamePostfixConvention = " Center";

        // Preview Image (not serialized):
        private RawImage previewImage;
        private Canvas canvas;

        // Stuff in OnValidate is a hack because I can't display properties nicely (setters won't get called).
        // Maybe I should be just manually calling a property setter instead of doing all this crazy stuff...
        private void OnValidate()
        {
            bool userSwitchedTargets = LockTarget != previousLockTarget;
            if (userSwitchedTargets)
            {
                DestroyCenterObjectIfExists(previousLockTarget, lockTargetCenter, true);
                EditorApplication.delayCall += () =>
                    lockTargetCenter = CreateCenterObjectIfDoesntExist(LockTarget, lockTargetCenter);

                bool newTargetIsNull = LockTarget == null;
                if (newTargetIsNull)
                {
                    Offset = Vector3.zero;
                    Distance = 0f;
                }

                previousLockTarget = LockTarget;
            }
        }

        private void OnEnable()
        {
            lockTargetCenter = CreateCenterObjectIfDoesntExist(LockTarget, lockTargetCenter);
            CreateImagePreviewInfrastructure();

            void CreateImagePreviewInfrastructure()
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
                    if (textGameObject != null)
                    {
                        var textComponent = textGameObject.AddComponent<Text>();
                        textComponent.fontSize = 48;
                        textComponent.text = "Preview:";
                    }
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
            DestroyCenterObjectIfExists(LockTarget, lockTargetCenter, false);
            DestroyImagePreviewInfrastructure();

            void DestroyImagePreviewInfrastructure()
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
                Debug.Log("ERROR: Component is not attached to a Camera... please fix this...");
                return;
            }

            bool hasLockTarget = LockTarget != null && lockTargetCenter != null;

            if (hasLockTarget)
            {
                LockToTarget();
                void LockToTarget()
                {
                    camera.transform.position = lockTargetCenter.transform.position
                        + new Vector3(0f, 0f, Distance + LockTarget.bounds.size.magnitude);
                    camera.transform.LookAt(lockTargetCenter.transform.position);
                    camera.transform.position += Offset;
                }
            }

            // Local variables for undoing temporary changes. They should only get assigned if needed.
            string photoRenderLayerName = "Photo Render";
            CameraClearFlags? originalClearFlags = null;
            Color? originalBackgroundColor = null; 
            int? originalLayer = null;
            int? originalCullingMask = null;
            Shader originalShader = null;

            if (hasLockTarget)
            {
                if (TransparentBackground)
                {
                    // Temporarily set camera clear fields.
                    originalClearFlags = camera.clearFlags;
                    originalBackgroundColor = camera.backgroundColor;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor =
                        new Color(camera.backgroundColor.r, camera.backgroundColor.g, camera.backgroundColor.b, 0);
                }
                if (OnlyRenderTarget)
                {
                    // Temporarily create a new layer and use it for the LockTarget and camera.cullingMask.
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
                        var tagManager = new SerializedObject(AssetDatabase
                            .LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
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

                        Debug.LogError("ERROR: All available layers are already used... "
                            + "please uncheck RenderTargetOnly...");
                        return -1;
                    }
                }
                if (UseUnlitShader)
                {
                    // Temporarily switch the LockTarget's renderer.
                    Shader unlit = GetUnlitShader();
                    originalShader = LockTarget.sharedMaterial.shader;
                    LockTarget.sharedMaterial.shader = unlit;

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
            }

            previewImage.texture = GenerateImage(camera, (int)PhotoResolution);
            static Texture2D GenerateImage(Camera camera, int resolution)
            {
                var tempRenderTexture = new RenderTexture(resolution, resolution, 32);
                RenderCameraToTexture(camera, tempRenderTexture);
                Texture2D image = ReadPixelsToTexture(resolution);
                RenderCameraToTexture(camera, null);
                DestroyImmediate(tempRenderTexture);
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

            UndoTemporaryChanges();
            void UndoTemporaryChanges()
            {
                camera.clearFlags = originalClearFlags ?? camera.clearFlags;
                camera.backgroundColor = originalBackgroundColor ?? camera.backgroundColor;
                if (LockTarget != null)
                {
                    LockTarget.gameObject.layer = originalLayer ?? LockTarget.gameObject.layer;
                    LockTarget.sharedMaterial.shader = originalShader ?? LockTarget.sharedMaterial.shader;
                }
                DestroyLayer(photoRenderLayerName);
                camera.cullingMask = originalCullingMask ?? camera.cullingMask;

                void DestroyLayer(string layerName)
                {
                    var tagManager = new SerializedObject(AssetDatabase
                        .LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
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

            SaveToFile(previewImage.texture, SaveDirectory, filename + FilenamePostfix, FileType, OverwriteFile,
                NumberingDelimiter);

            static void SaveToFile(Texture texture, string pathInAssetsFolder, string filename, FileType filetype,
                bool overwriteFile, string numberingDelimiter)
            {
                var texture2D = texture as Texture2D;
                string assetsPath = Path.Combine("Assets", pathInAssetsFolder);
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
                PhotoImporter.FullSavePath = fullPath;
                File.WriteAllBytes(fullPath, bytes);
                Debug.Log(bytes.Length / 1024 + "Kb was saved as: " + fullPath);
                AssetDatabase.Refresh();
            }
        }

        // Usage: Assign lockTarget to this (perhaps this shouldn't be static... or maybe I can use out keyword...)
        private static GameObject CreateCenterObjectIfDoesntExist(MeshRenderer target, GameObject center)
        {
            if (target == null)
                return null;

            if (center == null || center.name != target.name + CenterNamePostfixConvention)
            {
                center = GameObject.Find(target.name + CenterNamePostfixConvention);
                if (center == null)
                {
                    center = new GameObject(target.name + CenterNamePostfixConvention);
                    center.transform.position = target.bounds.center;
                    center.transform.parent = target.transform;
                }
            }

            return center;
        }

        private static void DestroyCenterObjectIfExists(MeshRenderer target, GameObject center, bool delayDestruction)
        {
            if (target == null || center == null)
                return;

            if (center.name == target.name + CenterNamePostfixConvention)
            {
                GameObject[] objectsToDestroy = FindObjectsOfType<GameObject>()
                    .Where(obj => obj.name == center.name)
                    .ToArray();

                if (delayDestruction)
                {
                    foreach (var gameObject in objectsToDestroy)
                        EditorApplication.delayCall += () => DestroyImmediate(gameObject);
                }
                else
                {
                    foreach (var gameObject in objectsToDestroy)
                        DestroyImmediate(gameObject);
                }
            }
        }
    }

    /// <summary>
    /// This class is for making <see cref="IconGenerator"/> fields editable by game developers in a pleasant way. 
    /// It does not need to be manually instantiated or attached to anything to be used.
    /// </summary>
    [CustomEditor(typeof(IconGenerator))]
    public class IconGeneratorEditor : Editor
    {
        private bool showInstructions = true;
        private const string instructions = "Instructions:\n" +
            "- Attach this to a Camera in your scene.\n" +
            "- Choose a 'Filename' and 'SaveDirectory'.\n" +
            "- Click 'Save Image'.";
        private const string warningMessage = "Disable or remove this component before building!\n" +
            "(Otherwise, the preview canvas will be left in the build.)";

        private bool showUsageTips = false;
        private const string usageTips =
            "- You may want to set up a new 'photobooth' scene with manually placed background/foreground props.\n" +
            "- If you have a target, you can rotate it around to take photos from different angles.\n" +
            "- Adjust the Camera's FieldOfView to achieve the desired perspective.\n" +
            "- For transparent icons, enable both OnlyRenderTarget and TransparentBackground.\n" +
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
                string[] fieldNames = Array.ConvertAll(typeof(IconGenerator).GetFields(), field => field.Name);
                foreach (var fieldName in fieldNames)
                {
                    string fieldNameCamelCase = char.ToLower(fieldName[0]) + fieldName.Substring(1);
                    FieldInfo fieldInfo = typeof(IconGeneratorEditor).GetField(fieldNameCamelCase,
                        bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
                    fieldInfo?.SetValue(this, serializedObject.FindProperty(fieldName));
                }
            }
        }

        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), GetType(), false);

            showInstructions = EditorGUILayout.Foldout(showInstructions, "Instructions:", true);
            if (showInstructions)
            {
                EditorGUILayout.HelpBox(instructions, MessageType.Info);
                EditorGUILayout.HelpBox(warningMessage, MessageType.Warning);
            }

            var photoCapture = target as IconGenerator;
            serializedObject.Update();

            EditorGUILayout.PropertyField(saveDirectory,
                new GUIContent("Save Directory", "The directory (relative to Assets folder) to save " +
                "the captured images."));

            if (photoCapture.LockTarget == null || !useTargetAsFilename.boolValue)
                EditorGUILayout.PropertyField(filename,
                    new GUIContent("Filename", "The name of the captured image file, not including extension."));

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced:", true);
            if (showAdvanced)
            {
                EditorGUILayout.PropertyField(filenamePostfix,
                    new GUIContent("Filename Postfix", "The postfix appended to the end of the filename, " +
                    "e.g., _icon, -icon, etc."));
                EditorGUILayout.PropertyField(overwriteFile,
                    new GUIContent("Overwrite File", "If enabled, it overwrites the existing file with the " +
                    "same name."));

                if (!overwriteFile.boolValue)
                    EditorGUILayout.PropertyField(numberingDelimiter,
                        new GUIContent("Numbering Delimiter", "The delimiter to append to the filename for " +
                        "numbering scheme, e.g. name, name_1, name_2, or name, name-1, name-2, etc."));

                EditorGUILayout.PropertyField(photoResolution,
                    new GUIContent("Photo Resolution", "The resolution of the captured image."));
                EditorGUILayout.PropertyField(fileType,
                    new GUIContent("File Type", "The file format of the captured image."));
            }

            EditorGUILayout.Space();

            GUIStyle sectionStyle = new GUIStyle(EditorStyles.helpBox);
            sectionStyle.normal.background = Texture2D.grayTexture;
            sectionStyle.margin = new RectOffset(10, 10, 5, 5);
            EditorGUILayout.BeginVertical(sectionStyle);

            EditorGUILayout.ObjectField(lockTarget,
                new GUIContent("Target", "The target the camera is set to focus on. Must have a MeshRenderer."));

            if (photoCapture.LockTarget != null)
            {
                EditorGUILayout.PropertyField(useTargetAsFilename,
                    new GUIContent("Use Target as Filename", "If enabled, the target's name will be used as the " +
                    "Filename."));
                EditorGUILayout.PropertyField(onlyRenderTarget,
                    new GUIContent("Render Only Target", "If enabled, only the target will be rendered. " +
                    "Often combined with Transparent Background."));
                EditorGUILayout.PropertyField(transparentBackground,
                    new GUIContent("Transparent Background", "If enabled, the saved image will have a transparent " +
                    "background. Often combined with 'Render Only Target'."));
                EditorGUILayout.PropertyField(useUnlitShader,
                    new GUIContent("Use Unlit Shader", "If enabled, the saved image will use the Unlit shader" +
                    " for the Target object. Scriptable Render Pipeline not supported."));
                EditorGUILayout.PropertyField(offset,
                    new GUIContent("Offset", "The amount that the camera is offset from the target."));
                EditorGUILayout.Slider(distance, 0, 2f,
                    new GUIContent("Target Distance", "The distance the camera is away from the target."));
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
        // Initial value is hack to prevent re-import of entire project. Overwritten on "Save Image" click.
        public static string FullSavePath = "DO/NOT/CHANGE/THIS!!.xyxz";

        private void OnPreprocessTexture()
        {
            if (assetPath.Replace('\\', '/') == FullSavePath.Replace('\\', '/'))
            {
                var textureImporter = assetImporter as TextureImporter;
                textureImporter.alphaIsTransparency = true;
            }
        }
    }

    /// <summary>
    /// The supported resolutions that <see cref="IconGenerator"/> supports for image generation.
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
    /// The supported filetype (extensions) that <see cref="IconGenerator"/> supports for image generation.
    /// </summary>
    public enum FileType : short
    {
        png,
        jpg
    }
}
#endif