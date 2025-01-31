using System;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
 
namespace Obi{

	[System.AttributeUsage(System.AttributeTargets.Field)]
    public class SerializeProperty : PropertyAttribute
    {
        public string PropertyName { get; private set; }
 
        public SerializeProperty(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
 
    #if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(SerializeProperty))]
    public class SerializePropertyAttributeDrawer : PropertyDrawer
    {
        private PropertyInfo propertyFieldInfo = null;
		private object target = null;
 
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
			if (target == null)
            	target = GetSource(property);
 
            // Find the property field using reflection, in order to get access to its getter/setter.
            if (propertyFieldInfo == null)
                propertyFieldInfo = target.GetType().GetProperty(((SerializeProperty)attribute).PropertyName,
                                                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
 
            if (propertyFieldInfo != null)
            {
  				// Retrieve the value using the property getter:
                object value = propertyFieldInfo.GetValue(target,null);
 
	            // Draw the property:
				EditorGUI.BeginProperty(position,label,property);
	            EditorGUI.BeginChangeCheck();
	            value = DrawProperty(position,property.propertyType,propertyFieldInfo.PropertyType,value,label);
	 
	            // If any changes were detected, call the property setter:
	            if (EditorGUI.EndChangeCheck() && propertyFieldInfo != null)
                {
                    foreach (var t in property.serializedObject.targetObjects)
                    {
                        // Record object state for undo:
                        Undo.RecordObject(t, "Inspector");

                        // Call property setter:
                        propertyFieldInfo.SetValue(t, value, null);
                        SetPropertyValue(property, propertyFieldInfo.PropertyType, value);

                        // Record prefab modification:
                        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    }
                }
				EditorGUI.EndProperty();

			}else
            {
				EditorGUI.LabelField(position,"Error: could not retrieve property.");
			}
        }

		private object GetSource(SerializedProperty property)
        {
			object trgt = property.serializedObject.targetObject;
			string[] data = property.propertyPath.Split('.');
			
			if (data.Length == 1) 
				return trgt;
			else{
				for (int i = 0; i < data.Length-1;++i){
					trgt = trgt.GetType().GetField(data[i]).GetValue(trgt);
				}
			}
			
			return trgt;
		} 
 
        private object DrawProperty(Rect position, SerializedPropertyType propertyType, Type type, object value, GUIContent label)
        {
            switch (propertyType)
            {
                case SerializedPropertyType.Integer:
                    return EditorGUI.IntField(position,label,(int)value);
                case SerializedPropertyType.Boolean:
                    return EditorGUI.Toggle(position,label,(bool)value);
                case SerializedPropertyType.Float:
                    return EditorGUI.FloatField(position,label,(float)value);
                case SerializedPropertyType.String:
                    return EditorGUI.TextField(position,label,(string)value);
                case SerializedPropertyType.Color:
                    return EditorGUI.ColorField(position,label,(Color)value);
                case SerializedPropertyType.ObjectReference:
                    return EditorGUI.ObjectField(position,label,(UnityEngine.Object)value,type,true);
                case SerializedPropertyType.ExposedReference:
                    return EditorGUI.ObjectField(position,label,(UnityEngine.Object)value,type,true);
                case SerializedPropertyType.LayerMask:
                    return EditorGUI.LayerField(position,label,(int)value);
                case SerializedPropertyType.Enum:
                    return EditorGUI.EnumPopup(position,label,(Enum)value);
                case SerializedPropertyType.Vector2:
                    return EditorGUI.Vector2Field(position,label,(Vector2)value);
                case SerializedPropertyType.Vector3:
                    return EditorGUI.Vector3Field(position,label,(Vector3)value);
                case SerializedPropertyType.Vector4:
                    return EditorGUI.Vector4Field(position,label,(Vector4)value);
                case SerializedPropertyType.Rect:
                    return EditorGUI.RectField(position,label,(Rect)value);
                case SerializedPropertyType.AnimationCurve:
                    return EditorGUI.CurveField(position,label,(AnimationCurve)value);
                case SerializedPropertyType.Bounds:
                    return EditorGUI.BoundsField(position,label,(Bounds)value);
                default:
                    throw new NotImplementedException("Unimplemented propertyType "+propertyType+".");
            }
        }

        private void SetPropertyValue(SerializedProperty property, Type type, object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: 
                    property.intValue = (int)value; break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = (bool)value; break;
                case SerializedPropertyType.Float:
                    property.floatValue = (float)value; break;
                case SerializedPropertyType.String:
                    property.stringValue = (string)value; break;
                case SerializedPropertyType.Color:
                    property.colorValue = (Color)value; break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = (UnityEngine.Object)value; break;
                case SerializedPropertyType.ExposedReference:
                    property.exposedReferenceValue = (UnityEngine.Object)value; break;
                case SerializedPropertyType.LayerMask:
                    property.intValue = (int)value; break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = (int)value; break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = (Vector2)value; break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = (Vector3)value; break;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = (Vector4)value; break;
                case SerializedPropertyType.Rect:
                    property.rectValue = (Rect)value; break;
                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = (AnimationCurve)value; break;
                case SerializedPropertyType.Bounds:
                    property.boundsValue = (Bounds)value; break;
                default:
                    throw new NotImplementedException("Unimplemented propertyType " + property.propertyType + ".");
            }
        }

    }
    #endif
}