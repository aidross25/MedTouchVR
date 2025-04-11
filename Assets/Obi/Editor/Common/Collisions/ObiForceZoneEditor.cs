using UnityEditor;
using UnityEngine;

namespace Obi
{

    /**
	 * Custom inspector for ObiForceZone component. 
	 */

    [CustomEditor(typeof(ObiForceZone)), CanEditMultipleObjects]
    public class ObiForceZoneEditor : Editor
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

