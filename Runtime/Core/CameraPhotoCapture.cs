using System.IO;
using UnityEditor;
using UnityEngine;

// TODO: Needs to show screenshot GUI bounds, and should be adjustable.
// GUI bounds should be created whenever attached to camera.

namespace abmarnie
{
    /// <summary>
    /// Attach this as a component to a Camera in your scene.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class CameraPhotoCapture : MonoBehaviour
    {
        public string SaveDirectory;

        public bool FocusOnTarget = false;
        public Transform Target;
        public float Distance = 5f;
        public float HorizontalOrbit = 0f;
        public float VerticalOrbit = 0f;
        public bool UseUnlitShader = false;
        public bool CenterCamera = false;
        public bool UseTargetAsFilename = false;

        public string Filename;

        public bool OverwriteFile = false;
        public string PostfixDelimiter;

        public Resolution PhotoResolution = Resolution.Res128;
        public FileType FileType = FileType.png;

        private void OnEnable()
        {
            if (FocusOnTarget)
            {
                SetCameraPosition(GetComponent<Camera>(),
                    !CenterCamera ? Target : GameObject.Find(Target.name + " Center").transform,
                    Distance, HorizontalOrbit, VerticalOrbit);
            }
        }

        private void Update()
        {
            if (FocusOnTarget)
            {
                SetCameraPosition(GetComponent<Camera>(),
                    !CenterCamera ? Target : GameObject.Find(Target.name + " Center").transform,
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

            Texture2D photo;
            if (FocusOnTarget && UseUnlitShader)
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
                var targetsRenderer = Target.GetComponent<MeshRenderer>();
                Shader original = targetsRenderer.sharedMaterial.shader;
                targetsRenderer.material.shader = shader;

                var renderTexture = new RenderTexture((int)PhotoResolution, (int)PhotoResolution, 32);
                RenderCameraToTexture(renderTexture);
                photo = ReadPixelsToTexture((int)PhotoResolution);
                RenderCameraToTexture(null);
                DestroyImmediate(renderTexture);

                targetsRenderer.sharedMaterial.shader = original;
            }
            else
            {
                var renderTexture = new RenderTexture((int)PhotoResolution, (int)PhotoResolution, 32);
                RenderCameraToTexture(renderTexture);
                photo = ReadPixelsToTexture((int)PhotoResolution);
                RenderCameraToTexture(null);
                DestroyImmediate(renderTexture);
            }

            SaveToFile(photo, SaveDirectory, filename, FileType, OverwriteFile, PostfixDelimiter);

            void RenderCameraToTexture(RenderTexture texture)
            {
                // If texture is null, it renders to Main Window.
                var camera = GetComponent<Camera>();
                camera.targetTexture = texture;
                RenderTexture.active = texture;
                camera.Render();
            }

            static Texture2D ReadPixelsToTexture(int res)
            {
                var texture = new Texture2D(res, res);
                texture.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                texture.Apply();
                return texture;
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
            if (CenterCamera == false)
            {
                if (Target.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    var newTarget = new GameObject(Target.name + " Center");
                    newTarget.transform.position = renderer.bounds.center;
                    SetCameraPosition(GetComponent<Camera>(), newTarget.transform, Distance, HorizontalOrbit, VerticalOrbit);
                }
                else
                    Debug.Log("MeshRenderer was null! Failed to ToggleFocusPivotToCenter...");
            }
            else
            {
                DestroyImmediate(GameObject.Find(Target.name + " Center"));
                SetCameraPosition(GetComponent<Camera>(), Target.transform, Distance, HorizontalOrbit, VerticalOrbit);
            }
            CenterCamera = !CenterCamera;
            SceneView.RepaintAll();
        }
    }

    [CustomEditor(typeof(CameraPhotoCapture))]
    public class CameraPhotoCaptureEditor : Editor
    {
        private SerializedProperty saveDirectory;

        private SerializedProperty focusOnTarget;
        private SerializedProperty cameraTarget;
        private SerializedProperty distance;
        private SerializedProperty horizontalOrbit;
        private SerializedProperty verticalOrbit;
        private SerializedProperty useUnlitShader;
        private SerializedProperty useTargetAsFilename;

        private SerializedProperty filename;

        private SerializedProperty overwriteFile;
        private SerializedProperty postfixDelimiter;

        private SerializedProperty photoResolution;
        private SerializedProperty fileType;

        private void OnEnable()
        {
            saveDirectory = serializedObject.FindProperty("SaveDirectory");

            focusOnTarget = serializedObject.FindProperty("FocusOnTarget");
            cameraTarget = serializedObject.FindProperty("Target");
            distance = serializedObject.FindProperty("Distance");
            horizontalOrbit = serializedObject.FindProperty("HorizontalOrbit");
            verticalOrbit = serializedObject.FindProperty("VerticalOrbit");
            useUnlitShader = serializedObject.FindProperty("UseUnlitShader");
            useTargetAsFilename = serializedObject.FindProperty("UseTargetAsFilename");

            filename = serializedObject.FindProperty("Filename");

            overwriteFile = serializedObject.FindProperty("OverwriteFile");
            postfixDelimiter = serializedObject.FindProperty("PostfixDelimiter");

            photoResolution = serializedObject.FindProperty("PhotoResolution");
            fileType = serializedObject.FindProperty("FileType");
        }

        public override void OnInspectorGUI()
        {
            var photoCapture = target as CameraPhotoCapture;
            serializedObject.Update();

            EditorGUILayout.PropertyField(saveDirectory);

            if (focusOnTarget.boolValue == false || useTargetAsFilename.boolValue == false)
                EditorGUILayout.PropertyField(filename);

            EditorGUILayout.PropertyField(overwriteFile);
            if (!overwriteFile.boolValue)
                EditorGUILayout.PropertyField(postfixDelimiter);


            EditorGUILayout.PropertyField(focusOnTarget);
            if (focusOnTarget.boolValue)
            {
                ShowInInspector(cameraTarget);
                EditorGUILayout.PropertyField(useTargetAsFilename);
                EditorGUILayout.Slider(distance, 0, 10, new GUIContent("Camera Distance To Target"));
                EditorGUILayout.Slider(horizontalOrbit, -180f, 180f, new GUIContent("Horizontal Orbit Angle"));
                EditorGUILayout.Slider(verticalOrbit, -180f, 180f, new GUIContent("Vertical Orbit Angle"));
                EditorGUILayout.PropertyField(useUnlitShader);
                if (GUILayout.Button("Focus Centering Toggle"))
                    photoCapture.ToggleFocusBetweenPivotAndCenter();
                if (focusOnTarget.boolValue)
                {
                    EditorGUILayout.LabelField("Focus: ",
                        !photoCapture.CenterCamera ? photoCapture.Target.name : photoCapture.Target.name + " Center");
                }
            }

            EditorGUILayout.PropertyField(photoResolution);
            EditorGUILayout.PropertyField(fileType);

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Generate Image"))
            {
                photoCapture.CapturePhoto(photoCapture.UseTargetAsFilename ? photoCapture.Target.name
                    : photoCapture.Filename);
            }

        }

        private void ShowInInspector(SerializedProperty property, bool includeChildren = true, GUIContent label = null)
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