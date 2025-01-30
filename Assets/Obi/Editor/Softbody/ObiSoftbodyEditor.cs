using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;


namespace Obi
{

    /**
 * Custom inspector for ObiCloth components.
 * Allows particle selection and constraint edition. 
 * 
 * Selection:
 * 
 * - To select a particle, left-click on it. 
 * - You can select multiple particles by holding shift while clicking.
 * - To deselect all particles, click anywhere on the object except a particle.
 * 
 * Constraints:
 * 
 * - To edit particle constraints, select the particles you wish to edit.
 * - Constraints affecting any of the selected particles will appear in the inspector.
 * - To add a new pin constraint to the selected particle(s), click on "Add Pin Constraint".
 * 
 */
    [CustomEditor(typeof(ObiSoftbody)), CanEditMultipleObjects]
    public class ObiSoftbodyEditor : Editor
    {

        [MenuItem("GameObject/3D Object/Obi/Obi Softbody", false, 500)]
        static void CreateObiSoftbody(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("Obi Softbody", typeof(SkinnedMeshRenderer),typeof(ObiSoftbody),typeof(ObiSoftbodySkinner));
            var renderer = go.GetComponent<SkinnedMeshRenderer>();
            renderer.sharedMaterial = ObiEditorUtils.GetDefaultMaterial();
            ObiEditorUtils.PlaceActorRoot(go, menuCommand);
        }

        ObiSoftbody actor;

        SerializedProperty softbodyBlueprint;

        SerializedProperty collisionMaterial;
        SerializedProperty selfCollisions;
        SerializedProperty surfaceCollisions;

        SerializedProperty shapeMatchingConstraintsEnabled;
        SerializedProperty deformationResistance;
        SerializedProperty maxDeformation;
        SerializedProperty plasticYield;
        SerializedProperty plasticCreep;
        SerializedProperty plasticRecovery;

        public void OnEnable()
        {
            actor = (ObiSoftbody)target;

            softbodyBlueprint = serializedObject.FindProperty("m_SoftbodyBlueprint");

            collisionMaterial = serializedObject.FindProperty("m_CollisionMaterial");
            selfCollisions = serializedObject.FindProperty("m_SelfCollisions");
            surfaceCollisions = serializedObject.FindProperty("m_SurfaceCollisions");

            shapeMatchingConstraintsEnabled = serializedObject.FindProperty("_shapeMatchingConstraintsEnabled");
            deformationResistance = serializedObject.FindProperty("_deformationResistance");
            maxDeformation = serializedObject.FindProperty("_maxDeformation");
            plasticYield = serializedObject.FindProperty("_plasticYield");
            plasticCreep = serializedObject.FindProperty("_plasticCreep");
            plasticRecovery = serializedObject.FindProperty("_plasticRecovery");
        }

        public override void OnInspectorGUI()
        {

            serializedObject.UpdateIfRequiredOrScript();

            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(softbodyBlueprint, new GUIContent("Blueprint"));

            if (actor.softbodyBlueprint == null)
            {
                if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.MaxWidth(80)))
                {
                    string path = EditorUtility.SaveFilePanel("Save blueprint", "Assets/", "SoftbodyBlueprint", "asset");
                    if (!string.IsNullOrEmpty(path))
                    {
                        path = FileUtil.GetProjectRelativePath(path);
                        ObiSoftbodySurfaceBlueprint asset = ScriptableObject.CreateInstance<ObiSoftbodySurfaceBlueprint>();

                        AssetDatabase.CreateAsset(asset, path);
                        AssetDatabase.SaveAssets();

                        actor.softbodyBlueprint = asset;
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                foreach (var t in targets)
                {
                    (t as ObiSoftbody).RemoveFromSolver();
                    (t as ObiSoftbody).ClearState();
                }
                serializedObject.ApplyModifiedProperties();
                foreach (var t in targets)
                    (t as ObiSoftbody).AddToSolver();
            }

            GUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Collisions", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(collisionMaterial, new GUIContent("Collision material"));
            EditorGUILayout.PropertyField(selfCollisions, new GUIContent("Self collisions"));
            EditorGUILayout.PropertyField(surfaceCollisions, new GUIContent("Surface collisions"));

            EditorGUILayout.Space();
            ObiEditorUtils.DoToggleablePropertyGroup(shapeMatchingConstraintsEnabled, new GUIContent("Shape Matching Constraints", Resources.Load<Texture2D>("Icons/ObiShapeMatchingConstraints Icon")),
            () => {
                EditorGUILayout.PropertyField(deformationResistance, new GUIContent("Deformation resistance"));
                EditorGUILayout.PropertyField(maxDeformation, new GUIContent("Max deformation"));
                EditorGUILayout.PropertyField(plasticYield, new GUIContent("Plastic yield"));
                EditorGUILayout.PropertyField(plasticCreep, new GUIContent("Plastic creep"));
                EditorGUILayout.PropertyField(plasticRecovery, new GUIContent("Plastic recovery"));
            });

            if (GUI.changed)
                serializedObject.ApplyModifiedProperties();

        }
    }

}


