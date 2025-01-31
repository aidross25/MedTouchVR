using UnityEditor;
using UnityEngine;

namespace Obi
{

    [CustomEditor(typeof(ObiFoamGenerator)), CanEditMultipleObjects]
    public class ObiFoamGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            DrawPropertiesExcluding(serializedObject, "m_Script");

            // Apply changes to the serializedProperty
            if (GUI.changed)
                serializedObject.ApplyModifiedProperties();
        }

    }

}

