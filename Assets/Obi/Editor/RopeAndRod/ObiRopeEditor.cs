using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Obi
{

    [CustomEditor(typeof(ObiRope))]
    public class ObiRopeEditor : Editor
    {

        [MenuItem("GameObject/3D Object/Obi/Obi Rope", false, 300)]
        static void CreateObiRope(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("Obi Rope", typeof(ObiRope), typeof(ObiRopeExtrudedRenderer));
            var renderer = go.GetComponent<ObiRopeExtrudedRenderer>();
            renderer.material = ObiEditorUtils.GetDefaultMaterial();
            ObiEditorUtils.PlaceActorRoot(go, menuCommand);
        }

        ObiRope actor;

        SerializedProperty ropeBlueprint;

        SerializedProperty collisionMaterial;
        SerializedProperty selfCollisions;
        SerializedProperty surfaceCollisions;
        SerializedProperty massScale;

        SerializedProperty distanceConstraintsEnabled;
        SerializedProperty stretchingScale;
        SerializedProperty stretchCompliance;
        SerializedProperty maxCompression;

        SerializedProperty bendConstraintsEnabled;
        SerializedProperty bendCompliance;
        SerializedProperty maxBending;
        SerializedProperty plasticYield;
        SerializedProperty plasticCreep;

        SerializedProperty aerodynamicsEnabled;
        SerializedProperty drag;
        SerializedProperty lift;

        SerializedProperty tearingEnabled;
        SerializedProperty tearResistanceMultiplier;
        SerializedProperty tearRate;

        GUIStyle editLabelStyle;

        public void OnEnable()
        {
            actor = (ObiRope)target;

            ropeBlueprint = serializedObject.FindProperty("m_RopeBlueprint");

            collisionMaterial = serializedObject.FindProperty("m_CollisionMaterial");
            selfCollisions = serializedObject.FindProperty("m_SelfCollisions");
            surfaceCollisions = serializedObject.FindProperty("m_SurfaceCollisions");
            massScale = serializedObject.FindProperty("m_MassScale");

            distanceConstraintsEnabled = serializedObject.FindProperty("_distanceConstraintsEnabled");
            stretchingScale = serializedObject.FindProperty("_stretchingScale");
            stretchCompliance = serializedObject.FindProperty("_stretchCompliance");
            maxCompression = serializedObject.FindProperty("_maxCompression");

            bendConstraintsEnabled = serializedObject.FindProperty("_bendConstraintsEnabled");
            bendCompliance = serializedObject.FindProperty("_bendCompliance");
            maxBending = serializedObject.FindProperty("_maxBending");
            plasticYield = serializedObject.FindProperty("_plasticYield");
            plasticCreep = serializedObject.FindProperty("_plasticCreep");

            aerodynamicsEnabled = serializedObject.FindProperty("_aerodynamicsEnabled");
            drag = serializedObject.FindProperty("_drag");
            lift = serializedObject.FindProperty("_lift");

            tearingEnabled = serializedObject.FindProperty("tearingEnabled");
            tearResistanceMultiplier = serializedObject.FindProperty("tearResistanceMultiplier");
            tearRate = serializedObject.FindProperty("tearRate");

        }

        private void DoEditButton()
        {
            using (new EditorGUI.DisabledScope(actor.ropeBlueprint == null))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUIUtility.labelWidth);
                EditorGUI.BeginChangeCheck();
                bool edit = GUILayout.Toggle(ToolManager.activeToolType == typeof(ObiPathEditor), new GUIContent(Resources.Load<Texture2D>("EditCurves")), "Button", GUILayout.MaxWidth(36), GUILayout.MaxHeight(24));
                EditorGUILayout.LabelField("Edit path", editLabelStyle, GUILayout.ExpandHeight(true), GUILayout.MaxHeight(24));
                if (EditorGUI.EndChangeCheck())
                {
                    if (edit)
                        ToolManager.SetActiveTool<ObiPathEditor>();
                    else
                        ToolManager.RestorePreviousPersistentTool();

                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        public override void OnInspectorGUI()
        {
            if (editLabelStyle == null)
            {
                editLabelStyle = new GUIStyle(GUI.skin.label);
                editLabelStyle.alignment = TextAnchor.MiddleLeft;
            }

            serializedObject.UpdateIfRequiredOrScript();

            if (actor.sourceBlueprint != null && actor.ropeBlueprint.path.ControlPointCount < 2)
            {
                actor.ropeBlueprint.GenerateImmediate();
            }

            using (new EditorGUI.DisabledScope(ToolManager.activeToolType == typeof(ObiPathEditor)))
            {
                GUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(ropeBlueprint, new GUIContent("Blueprint"));

                if (actor.ropeBlueprint == null)
                {
                    if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.MaxWidth(80)))
                    {
                        string path = EditorUtility.SaveFilePanel("Save blueprint", "Assets/", "RopeBlueprint", "asset");
                        if (!string.IsNullOrEmpty(path))
                        {
                            path = FileUtil.GetProjectRelativePath(path);
                            ObiRopeBlueprint asset = ScriptableObject.CreateInstance<ObiRopeBlueprint>();

                            AssetDatabase.CreateAsset(asset, path);
                            AssetDatabase.SaveAssets();

                            actor.ropeBlueprint = asset;
                        }
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var t in targets)
                    {
                        (t as ObiRope).RemoveFromSolver();
                        (t as ObiRope).ClearState();
                    }
                    serializedObject.ApplyModifiedProperties();
                    foreach (var t in targets)
                        (t as ObiRope).AddToSolver();
                }

                GUILayout.EndHorizontal();
            }

            GUI.enabled = !Application.isPlaying;
            EditorGUILayout.PropertyField(massScale, new GUIContent("Mass scale"));
            GUI.enabled = true;

            DoEditButton();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Collisions", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(collisionMaterial, new GUIContent("Collision material"));
            EditorGUILayout.PropertyField(selfCollisions, new GUIContent("Self collisions"));
            EditorGUILayout.PropertyField(surfaceCollisions, new GUIContent("Surface-based collisions"));

            EditorGUILayout.Space();
            ObiEditorUtils.DoToggleablePropertyGroup(tearingEnabled, new GUIContent("Tearing"),
            () =>
            {
                EditorGUILayout.PropertyField(tearResistanceMultiplier, new GUIContent("Tear resistance"));
                EditorGUILayout.PropertyField(tearRate, new GUIContent("Tear rate"));
            });
            ObiEditorUtils.DoToggleablePropertyGroup(distanceConstraintsEnabled, new GUIContent("Distance Constraints", Resources.Load<Texture2D>("Icons/ObiDistanceConstraints Icon")),
            () =>
            {
                EditorGUILayout.PropertyField(stretchingScale, new GUIContent("Stretching scale"));
                EditorGUILayout.PropertyField(stretchCompliance, new GUIContent("Stretch compliance"));
                EditorGUILayout.PropertyField(maxCompression, new GUIContent("Max compression"));
            });

            ObiEditorUtils.DoToggleablePropertyGroup(bendConstraintsEnabled, new GUIContent("Bend Constraints", Resources.Load<Texture2D>("Icons/ObiBendConstraints Icon")),
            () =>
            {
                EditorGUILayout.PropertyField(bendCompliance, new GUIContent("Bend compliance"));
                EditorGUILayout.PropertyField(maxBending, new GUIContent("Max bending"));
                EditorGUILayout.PropertyField(plasticYield, new GUIContent("Plastic yield"));
                EditorGUILayout.PropertyField(plasticCreep, new GUIContent("Plastic creep"));
            });

            ObiEditorUtils.DoToggleablePropertyGroup(aerodynamicsEnabled, new GUIContent("Aerodynamics", Resources.Load<Texture2D>("Icons/ObiAerodynamicConstraints Icon")),
            () => {
                EditorGUILayout.PropertyField(drag, new GUIContent("Drag"));
                EditorGUILayout.PropertyField(lift, new GUIContent("Lift"));
            });

            if (GUI.changed)
                serializedObject.ApplyModifiedProperties();

        }

        [DrawGizmo(GizmoType.Selected)]
        private static void DrawGizmos(ObiRope actor, GizmoType gizmoType)
        {
            Handles.color = Color.white;
            if (actor.ropeBlueprint != null)
                ObiPathHandles.DrawPathHandle(actor.ropeBlueprint.path, actor.transform.localToWorldMatrix, actor.ropeBlueprint.thickness, 20, false);
        }

    }

}


