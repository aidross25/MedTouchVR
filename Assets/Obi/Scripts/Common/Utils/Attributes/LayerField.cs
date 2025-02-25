using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Obi{

	[AttributeUsage(AttributeTargets.Field)]
	public class LayerField : MultiPropertyAttribute
	{
#if UNITY_EDITOR
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	    {
			property.intValue = EditorGUI.LayerField(position, label, property.intValue);
        }
#endif
    }

}

