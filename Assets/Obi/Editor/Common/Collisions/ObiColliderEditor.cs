using UnityEditor;
using UnityEngine;

namespace Obi{
	
	[CustomEditor(typeof(ObiColliderBase), true), CanEditMultipleObjects] 
	public class ObiColliderEditor : Editor
	{

        ObiColliderBase collider;
        SerializedProperty collisionFilter;

        public void OnEnable()
        {
            collider = (ObiColliderBase)target;
            collisionFilter = serializedObject.FindProperty("filter");
        }

        protected void NonReadableMeshWarning(Mesh mesh)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Texture2D icon = EditorGUIUtility.Load("icons/console.erroricon.png") as Texture2D;
            EditorGUILayout.LabelField(new GUIContent("The input mesh is not readable. Read/Write must be enabled in the mesh import settings.", icon), EditorStyles.wordWrappedMiniLabel);

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

        public override void OnInspectorGUI()
        {

            serializedObject.UpdateIfRequiredOrScript();

            foreach (ObiColliderBase t in targets)
            {
                ObiMeshShapeTracker meshTracker = t.Tracker as ObiMeshShapeTracker;
                if (meshTracker != null)
                {
                    if (meshTracker.targetMesh != null && !meshTracker.targetMesh.isReadable)
                        NonReadableMeshWarning(meshTracker.targetMesh);
                }
            }

            var rect = EditorGUILayout.GetControlRect();
            var label = EditorGUI.BeginProperty(rect, new GUIContent("Collision category"), collisionFilter);
            rect = EditorGUI.PrefixLabel(rect, label);

            EditorGUI.BeginChangeCheck();
            var newCategory = EditorGUI.Popup(rect, ObiUtils.GetCategoryFromFilter(collider.Filter), ObiUtils.categoryNames);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (ObiColliderBase t in targets)
                {
                    Undo.RecordObject(t, "Set collision category");
                    t.Filter = ObiUtils.MakeFilter(ObiUtils.GetMaskFromFilter(t.Filter), newCategory);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                }
            }
            EditorGUI.EndProperty();

            rect = EditorGUILayout.GetControlRect();
            label = EditorGUI.BeginProperty(rect, new GUIContent("Collides with"), collisionFilter);
            rect = EditorGUI.PrefixLabel(rect, label);

            EditorGUI.BeginChangeCheck();
            var newMask = EditorGUI.MaskField(rect, ObiUtils.GetMaskFromFilter(collider.Filter), ObiUtils.categoryNames);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (ObiColliderBase t in targets)
                {
                    Undo.RecordObject(t, "Set collision mask");
                    t.Filter = ObiUtils.MakeFilter(newMask, ObiUtils.GetCategoryFromFilter(t.Filter));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                }
            }
            EditorGUI.EndProperty();

            DrawPropertiesExcluding(serializedObject, "m_Script", "CollisionMaterial", "filter", "Thickness", "Inverted");


            // Apply changes to the serializedProperty
            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }

        }

    }
}


