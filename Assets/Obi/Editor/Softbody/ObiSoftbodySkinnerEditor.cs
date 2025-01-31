using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Collections;
using System.IO;
using System;

namespace Obi{
	
	[CustomEditor(typeof(ObiSoftbodySkinner)), CanEditMultipleObjects] 
	public class ObiSoftbodySkinnerEditor : Editor
	{
		
		public ObiSoftbodySkinner skinner;
		protected IEnumerator routine;

        SerializedProperty softbody;
        SerializedProperty skinMap;
        SerializedProperty softbodyInfluence;
        SerializedProperty radius;
        SerializedProperty falloff;
        SerializedProperty maxInfluences;

        private ObiRaycastBrush paintBrush;
        private ObiSoftbodyInfluenceChannel currentProperty = null;
        private Material paintMaterial;
        private Material textureExportMaterial;
        private Material paddingMaterial;
        private Mesh visualizationMesh;

        private bool editInfluences = false;

        BooleanPreference bindFoldout;

        [MenuItem("CONTEXT/ObiSoftbodySkinner/Bake mesh")]
        static void Bake(MenuCommand command)
        {
            ObiSoftbodySkinner renderer = (ObiSoftbodySkinner)command.context;

            if (renderer.actor.isLoaded)
            {
                var system = renderer.actor.solver.GetRenderSystem<ObiSoftbodySkinner>() as ObiSoftbodyRenderSystem;

                if (system != null)
                {
                    var mesh = new Mesh();
                    system.BakeMesh(renderer, ref mesh, true);
                    ObiEditorUtils.SaveMesh(mesh, "Save softbody mesh", "softbody mesh");
                    GameObject.DestroyImmediate(mesh);
                }
            }
        }

        public void OnEnable()
        {
            bindFoldout = new BooleanPreference($"{target.GetType()}.bindFoldout", true);

            skinner = (ObiSoftbodySkinner)target;
            softbody = serializedObject.FindProperty("softbody");
            skinMap = serializedObject.FindProperty("customSkinMap");
            softbodyInfluence = serializedObject.FindProperty("softbodyInfluence");
            radius = serializedObject.FindProperty("radius");
            falloff = serializedObject.FindProperty("falloff");
            maxInfluences = serializedObject.FindProperty("maxInfluences");

            paintBrush = new ObiRaycastBrush(null,
                                                         () =>
                                                         {
                                                             // As RecordObject diffs with the end of the current frame,
                                                             // and this is a multi-frame operation, we need to use RegisterCompleteObjectUndo instead.
                                                             Undo.RegisterCompleteObjectUndo(target, "Paint influences");
                                                         },
                                                         () =>
                                                         {
                                                             SceneView.RepaintAll();
                                                         },
                                                         () =>
                                                         {
                                                             EditorUtility.SetDirty(target);
                                                         });

            currentProperty = new ObiSoftbodyInfluenceChannel(this);

            if (paintMaterial == null)
                paintMaterial = Resources.Load<Material>("PropertyGradientMaterial");

            if (textureExportMaterial == null)
                textureExportMaterial = Resources.Load<Material>("UVSpaceColorMaterial");

            if (paddingMaterial == null)
                paddingMaterial = Resources.Load<Material>("PaddingMaterial");
        }
		
		public void OnDisable()
        {
			EditorUtility.ClearProgressBar();
        }

        protected void NonReadableMeshWarning(Mesh mesh)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Texture2D icon = EditorGUIUtility.Load("icons/console.erroricon.png") as Texture2D;
            EditorGUILayout.LabelField(new GUIContent("The renderer mesh is not readable. Read/Write must be enabled in the mesh import settings.", icon), EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Fix now", GUILayout.MaxWidth(100), GUILayout.MinHeight(32)))
            {
                string assetPath = AssetDatabase.GetAssetPath(mesh);
                ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (modelImporter != null)
                {
                    modelImporter.isReadable = true;
                }
                modelImporter.SaveAndReimport();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        protected bool ValidateRendererMesh()
        {
            SkinnedMeshRenderer skin = skinner.GetComponent<SkinnedMeshRenderer>();

            if (skin != null && skin.sharedMesh != null)
            {
                if (!skin.sharedMesh.isReadable)
                {
                    NonReadableMeshWarning(skin.sharedMesh);
                    return false;
                }
                return true;
            }
            return false;
        }

        public bool ReadInfluenceFromTexture(Texture2D source)
        {
            if (source == null || skinner.sourceMesh == null)
                return false;

            Vector2[] uvs = skinner.sourceMesh.uv;

            // Iterate over all vertices in the mesh reading back colors from the texture:
            for (int i = 0; i < skinner.sourceMesh.vertexCount; ++i)
            {
                try
                {
                    currentProperty.Set(i, source.GetPixelBilinear(uvs[i].x, uvs[i].y).r);
                }
                catch (UnityException e)
                {
                    Debug.LogException(e);
                    return false;
                }
            }

            return true;
        }

        public bool WriteInfluenceToTexture(string path, int width, int height, int padding)
        {

            if (skinner.sourceMesh == null || path == null || textureExportMaterial == null || !textureExportMaterial.SetPass(0))
                return false;

            if (visualizationMesh == null)
            {
                visualizationMesh = GameObject.Instantiate(skinner.sourceMesh);
            }

            RenderTexture tempRT = RenderTexture.GetTemporary(width, height, 0);
            RenderTexture paddingRT = RenderTexture.GetTemporary(width, height, 0);

            RenderTexture old = RenderTexture.active;
            RenderTexture.active = tempRT;

            GL.PushMatrix();
            GL.LoadProjectionMatrix(Matrix4x4.Ortho(0, 1, 0, 1, -1, 1));

            Color[] colors = new Color[skinner.sourceMesh.vertexCount];
            for (int i = 0; i < colors.Length; i++)
            {
                float val = currentProperty.Get(i);
                colors[i] = new Color(val, val, val, 1);
            }

            visualizationMesh.colors = colors;
            Graphics.DrawMeshNow(visualizationMesh, Matrix4x4.identity);

            GL.PopMatrix();

            // Perform padding/edge dilation
            paddingMaterial.SetFloat("_Padding", padding);
            Graphics.Blit(tempRT, paddingRT, paddingMaterial);

            // Read result into our Texture2D.
            RenderTexture.active = paddingRT;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);

            RenderTexture.active = old;
            RenderTexture.ReleaseTemporary(paddingRT);
            RenderTexture.ReleaseTemporary(tempRT);

            byte[] png = texture.EncodeToPNG();
            GameObject.DestroyImmediate(texture);

            try
            {
                File.WriteAllBytes(path, png);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }

            AssetDatabase.Refresh();

            return true;
        }

        public override void OnInspectorGUI() {
			
			serializedObject.Update();

            GUI.enabled = ValidateRendererMesh();

            EditorGUILayout.PropertyField(softbody);

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(skinMap);

            if (skinner.customSkinMap == null)
            {
                if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.MaxWidth(80)))
                {
                    string path = EditorUtility.SaveFilePanel("Save skinmap", "Assets/", "SoftbodySkinmap", "asset");
                    if (!string.IsNullOrEmpty(path))
                    {
                        path = FileUtil.GetProjectRelativePath(path);
                        ObiSkinMap asset = ScriptableObject.CreateInstance<ObiSkinMap>();

                        AssetDatabase.CreateAsset(asset, path);
                        AssetDatabase.SaveAssets();

                        skinner.skinMap = asset;
                    }
                }
            }
            GUILayout.EndHorizontal();

            if (skinner.customSkinMap != null)
            {

                var color = GUI.color;
                if (!Application.isPlaying)
                {
                    if (!skinner.ValidateRenderer())
                        GUI.color = Color.red;
                }

                if (GUILayout.Button("Bind", GUI.skin.FindStyle("LargeButton"), GUILayout.Height(32)))
                {
                    skinner.Bind();
                    EditorUtility.SetDirty(skinner.skinMap);
                    AssetDatabase.SaveAssets();
                }
                GUI.color = color;

                bindFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(bindFoldout, "Bind parameters");
                if (bindFoldout)
                {
                    if (skinner.customSkinMap != null)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(radius);
                        EditorGUILayout.PropertyField(falloff);
                        EditorGUILayout.PropertyField(maxInfluences);
                        EditorGUILayout.PropertyField(softbodyInfluence);

                        var reservedSpace = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
                        if (GUI.Button(reservedSpace, "Import influence from texture"))
                        {
                            Undo.RecordObject(skinner.skinMap, "Import particle property");
                            var path = EditorUtility.OpenFilePanel("Open texture", "", "png");
                            if (path.Length > 0)
                            {
                                var fileContent = File.ReadAllBytes(path);

                                Texture2D texture = new Texture2D(1, 1);
                                texture.LoadImage(fileContent);
                                if (!ReadInfluenceFromTexture(texture))
                                {
                                    EditorUtility.DisplayDialog("Invalid texture", "The texture is either null or not readable.", "Ok");
                                }
                                SceneView.RepaintAll();
                            }
                        }

                        reservedSpace = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
                        if (GUI.Button(reservedSpace, "Export influence to texture"))
                        {
                            var path = EditorUtility.SaveFilePanel("Save texture as PNG",
                                                                    "",
                                                                    "property.png",
                                                                    "png");
                            if (path.Length > 0)
                            {
                                if (!WriteInfluenceToTexture(path, 512, 512, 64))
                                {
                                    EditorUtility.DisplayDialog("Invalid path", "Could not write a texture to that location.", "Ok");
                                }
                            }
                        }

                        EditorGUI.BeginChangeCheck();
                        reservedSpace = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
                        editInfluences = GUI.Toggle(reservedSpace, editInfluences, new GUIContent("Paint influence"), "Button");
                        if (EditorGUI.EndChangeCheck())
                            SceneView.RepaintAll();

                        if (editInfluences && paintBrush != null)
                        {
                            currentProperty.BrushModes(paintBrush);

                            if (paintBrush.brushMode.needsInputValue)
                                currentProperty.PropertyField();

                            paintBrush.radius = EditorGUILayout.Slider("Brush size", paintBrush.radius, 0.0001f, 0.5f);
                            paintBrush.innerRadius = EditorGUILayout.Slider("Brush inner size", paintBrush.innerRadius, 0, 1);
                            paintBrush.opacity = EditorGUILayout.Slider("Brush opacity", paintBrush.opacity, 0, 1);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }

            GUI.enabled = true;

            // Apply changes to the serializedProperty
            if (GUI.changed){
				serializedObject.ApplyModifiedProperties();
			}
		}

        public void OnSceneGUI()
        {
            if (editInfluences)
            {
                skinner.InitializeInfluences();

                SkinnedMeshRenderer skin = skinner.GetComponent<SkinnedMeshRenderer>();

                if (skin != null && skin.sharedMesh != null)
                {
                    var bakedMesh = new Mesh();
                    skin.BakeMesh(bakedMesh);

                    if (Event.current.type == EventType.Repaint)
                        DrawMesh(bakedMesh);

                    if (Camera.current != null)
                    {
                        paintBrush.raycastTarget = bakedMesh; 
                        paintBrush.raycastTransform = skin.transform.localToWorldMatrix;

                        // TODO: do better.
                        var v = bakedMesh.vertices;
                        Vector3[] worldSpace = new Vector3[v.Length];
                        for (int i = 0; i < worldSpace.Length; ++i)
                            worldSpace[i] = paintBrush.raycastTransform.MultiplyPoint3x4(v[i]);

                        paintBrush.DoBrush(worldSpace);
                    }

                    DestroyImmediate(bakedMesh);
                }

            }
        }

        private void DrawMesh(Mesh mesh)
        {
            if (paintMaterial.SetPass(0))
            {
                Color[] colors = new Color[mesh.vertexCount];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color(skinner.m_softbodyInfluences[i], skinner.m_softbodyInfluences[i], skinner.m_softbodyInfluences[i]);
                }

                mesh.colors = colors;
                Graphics.DrawMeshNow(mesh, paintBrush.raycastTransform);

                // For the time being, DrawMeshNow is broken in Unity 2021 and up:
                // https://forum.unity.com/threads/drawmeshnow-broken-in-2021.1299723/
                /*if (paintMaterial.SetPass(1))
                {
                    Color wireColor = ObiEditorSettings.GetOrCreateSettings().brushWireframeColor;
                    for (int i = 0; i < paintBrush.weights.Length; i++)
                    {
                        colors[i] = wireColor * paintBrush.weights[i];
                    }

                    mesh.colors = colors;
                    GL.wireframe = true;
                    Graphics.DrawMeshNow(mesh, paintBrush.raycastTransform);
                    GL.wireframe = false;
                }*/
            }
        }
	}
}


