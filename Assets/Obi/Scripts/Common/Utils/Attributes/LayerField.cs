using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Obi{

	#if UNITY_EDITOR
	[System.AttributeUsage(System.AttributeTargets.Field)]
	public class LayerField : MultiPropertyAttribute
	{
	    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	    {
			property.intValue = EditorGUI.LayerField(position, label, property.intValue);
        }
	}
	#endif
}

