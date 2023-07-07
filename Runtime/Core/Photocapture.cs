using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
        public string NumberingDelimiter;
        public Resolution PhotoResolution = Resolution.Res128;
        public FileType FileType = FileType.png;

        private Transform previousLockTarget;
        public Transform LockTarget;

        public bool UseTargetAsFilename = false;
        public Vector3 Offset;
        public float Distance = 1f;
        public bool UseUnlitShader = false;

        private bool cameraFocusedOnCenter = false;
        public bool GetCameraFocusedOnCenter() => cameraFocusedOnCenter;
        private void SetCameraFocusOnCenter(bool value, bool delayDestruction)
        {
            if (value)
                CreateCenterObjectIfDoesntExist(LockTarget);
            else
                DestroyCenterObjectIfExists(LockTarget, delayDestruction);
            cameraFocusedOnCenter = value;
        }

        // These are for showing a preview image.
        private RawImage previewImage;
        private Canvas canvas;

        private void OnValidate()
        {
            // This is basically a hack to run logic whenever lockTarget changes. Simply using a property or custom 
            // setter won't work, because they don't work nicely with with EditorGUILayout updates... (I think).
            // Maybe I could be doing this instead:
            // https://stackoverflow.com/questions/37958136/unity-c-how-script-know-when-public-variablenot-property-changed
            bool userSwitchedTargets = LockTarget != previousLockTarget;
            if (userSwitchedTargets)
            {
                DestroyCenterObjectIfExists(previousLockTarget, true);
                bool previousTargetExisted = previousLockTarget != null;
                if (previousTargetExisted)
                    SetCameraFocusOnCenter(false, true);

                bool newTargetIsNull = LockTarget == null;
                if (newTargetIsNull)
                    ResetTargetDependentState();

                previousLockTarget = LockTarget;

                void ResetTargetDependentState()
                {
                    UseTargetAsFilename = false;
                    Offset = Vector3.zero;
                    Distance = 1f;
                    SetCameraFocusOnCenter(false, true);
                }
            }
        }

        private void OnEnable()
        {
            if (cameraFocusedOnCenter)
                CreateCenterObjectIfDoesntExist(LockTarget);

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

                GameObject rawImageGO = new("Photocapture Preview Image");
                rawImageGO.transform.SetParent(canvas.transform, false);
                var rawImageTransform = rawImageGO.AddComponent<RectTransform>();
                // Use bottom right corner of image as anchor.
                rawImageTransform.anchorMin = new Vector2(1f, 0f);
                rawImageTransform.anchorMax = new Vector2(1f, 0f);
                rawImageTransform.pivot = new Vector2(1f, 0f);
                rawImageTransform.anchoredPosition = new Vector2(-10f, 10f); // Adjust the position as desired.
                rawImageTransform.sizeDelta = new Vector2(250f, 250f); // Adjust the size as desired.
                previewImage = rawImageGO.AddComponent<RawImage>();
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

            if (LockTarget != null)
                LockToAndOrbitTarget();

            UpdatePreviewImage();

            void LockToAndOrbitTarget()
            {
                Transform target = cameraFocusedOnCenter ? GameObject.Find(LockTarget.name + " Center").transform : LockTarget;

                if (LockTarget.TryGetComponent<Renderer>(out var renderer))
                    camera.transform.position = target.position + new Vector3(0f, 0f, Distance + renderer.bounds.extents.magnitude);
                else
                    camera.transform.position = target.position + new Vector3(0f, 0f, Distance);

                camera.transform.LookAt(target.position);
                camera.transform.position += Offset;
            }

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
                }
                else
                    previewImage.texture = GenerateImage(camera, (int)PhotoResolution);
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

        private static Texture2D GenerateImage(Camera camera, int resolution)
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

        private static Shader GetUnlitShader()
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
            // SMELL: Doing a bunch of GameObject.Find instead of just having a field reference to a centerObject....
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

        private static void DestroyCenterObjectIfExists(Transform target, bool delayDestruction)
        {
            if (target != null)
            {
                var centerObject = GameObject.Find(target.name + " Center");
                if (centerObject != null)
                {
                    if (delayDestruction)
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

            EditorGUILayout.PropertyField(filenamePostfix, new GUIContent("Filename Postfix", "The postfix appended to the end of the filename, e.g., _icon, -icon, etc."));

            EditorGUILayout.PropertyField(overwriteFile, new GUIContent("Overwrite File", "If enabled, it overwrites the existing file with the same name."));

            if (!overwriteFile.boolValue)
                EditorGUILayout.PropertyField(numberingDelimiter, new GUIContent("Numbering Delimiter", "The delimiter to append to the filename for numbering scheme."));

            EditorGUILayout.PropertyField(photoResolution, new GUIContent("Photo Resolution", "The resolution of the captured image."));
            EditorGUILayout.PropertyField(fileType, new GUIContent("File Type", "The file format of the captured image."));

            ShowInInspector(serializedObject, lockTarget, true, new GUIContent("Target", "The target the camera is set to focus and orbit around."));

            if (photoCapture.LockTarget != null)
            {
                EditorGUILayout.PropertyField(useTargetAsFilename);
                EditorGUILayout.PropertyField(offset, new GUIContent("Offset", "The amount that the camera is offset from the target."));
                EditorGUILayout.Slider(distance, 0, 2f, new GUIContent("Target Distance", "The distance the camera is away from the target."));
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

        // TODO: Maybe I should just be doing EditorGUILayout.ObjectField (lol).
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