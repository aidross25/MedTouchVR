using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;


namespace Obi
{

    [CustomEditor(typeof(ObiPinhole))]
    public class ObiPinholeEditor : Editor
    {

        SerializedProperty targetTransform;
        SerializedProperty position;
        SerializedProperty limitRange;
        SerializedProperty range;
        SerializedProperty compliance;
        SerializedProperty friction;
        SerializedProperty motorSpeed;
        SerializedProperty motorForce;
        SerializedProperty clamp;
        SerializedProperty breakThreshold;

        ObiPinhole pinhole;

        public void OnEnable()
        {

            pinhole = target as ObiPinhole;
            targetTransform = serializedObject.FindProperty("m_Target");
            position = serializedObject.FindProperty("m_Position");
            limitRange = serializedObject.FindProperty("m_LimitRange");
            range = serializedObject.FindProperty("m_Range");
            friction = serializedObject.FindProperty("m_Friction");
            motorSpeed = serializedObject.FindProperty("m_MotorSpeed");
            motorForce = serializedObject.FindProperty("m_MotorForce");
            compliance = serializedObject.FindProperty("m_Compliance");
            clamp = serializedObject.FindProperty("m_ClampAtEnds");
            breakThreshold = serializedObject.FindProperty("breakThreshold");
        }

        public override void OnInspectorGUI()
        {

            serializedObject.UpdateIfRequiredOrScript();

            // warn about incorrect setups:
            if (!targetTransform.hasMultipleDifferentValues)
            {
                var targetValue = targetTransform.objectReferenceValue as UnityEngine.Component;
                if (targetValue != null)
                {
                    var collider = targetValue.GetComponent<ObiColliderBase>();
                    if (collider == null)
                    {
                        EditorGUILayout.HelpBox("Pinholes require the target object to have a ObiCollider component. Please add one.", MessageType.Warning);
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            Transform trget = EditorGUILayout.ObjectField("Target", pinhole.target, typeof(Transform), true) as Transform;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pinhole, "Set target");
                pinhole.target = trget;
                PrefabUtility.RecordPrefabInstancePropertyModifications(pinhole);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(position, new GUIContent("Position"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                pinhole.CalculateMu();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(limitRange, new GUIContent("Limit Range"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                pinhole.CalculateRange();
            }

            if (limitRange.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(range, new GUIContent("Range"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    pinhole.CalculateRange();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(clamp, new GUIContent("Clamp at ends"));
            EditorGUILayout.PropertyField(friction, new GUIContent("Friction"));
            EditorGUILayout.PropertyField(motorSpeed, new GUIContent("Motor Target Speed"));
            EditorGUILayout.PropertyField(motorForce, new GUIContent("Motor Max Force"));
            EditorGUILayout.PropertyField(compliance, new GUIContent("Compliance"));
            EditorGUILayout.PropertyField(breakThreshold, new GUIContent("Break threshold"));
            
            if (GUI.changed)
                serializedObject.ApplyModifiedProperties();

        }

        [DrawGizmo(GizmoType.Selected)]
        private static void DrawGizmos(ObiPinhole pinhole, GizmoType gizmoType)
        {
            var rope = pinhole.GetComponent<ObiRope>();

            var ropeBlueprint = rope.sharedBlueprint as ObiRopeBlueprintBase;
            if (rope.isLoaded && ropeBlueprint != null && ropeBlueprint.deformableEdges != null)
            {
                Handles.color = new Color(1, 0.5f, 0.2f, 1);
                Handles.matrix = rope.solver.transform.localToWorldMatrix;

                // draw limits:
                if (pinhole.limitRange)
                {
                    for (int i = pinhole.firstEdge.edgeIndex; i <= pinhole.lastEdge.edgeIndex; ++i)
                    {
                        if (i >= 0 && i < ropeBlueprint.deformableEdges.Length)
                        {
                            int p1 = ropeBlueprint.deformableEdges[i * 2];
                            int p2 = ropeBlueprint.deformableEdges[i * 2 + 1];
                            var pos1 = rope.solver.positions[rope.solverIndices[p1]];
                            var pos2 = rope.solver.positions[rope.solverIndices[p2]];

                            if (i == pinhole.firstEdge.edgeIndex)
                            {
                                pos1 = Vector4.Lerp(pos1, pos2, pinhole.firstEdge.coordinate);
                                Handles.DrawSolidDisc(pos1, pos2 - pos1, HandleUtility.GetHandleSize(pos1) * 0.05f);
                            }
                            if (i == pinhole.lastEdge.edgeIndex)
                            {
                                pos2 = Vector4.Lerp(pos1, pos2, pinhole.lastEdge.coordinate);
                                Handles.DrawSolidDisc(pos2, pos1 - pos2, HandleUtility.GetHandleSize(pos2) * 0.05f);
                            }

                            Handles.DrawLine(pos1, pos2, 2);
                        }
                    }
                }

                // draw source particle:
                int edgeIndex = pinhole.edgeIndex;

                if (edgeIndex >= 0 && edgeIndex < ropeBlueprint.deformableEdges.Length)
                {
                    int p1 = ropeBlueprint.deformableEdges[edgeIndex * 2];
                    int p2 = ropeBlueprint.deformableEdges[edgeIndex * 2 + 1];
                    var pos1 = rope.solver.positions[rope.solverIndices[p1]];
                    var pos2 = rope.solver.positions[rope.solverIndices[p2]];
                    Vector4 pos = Vector4.Lerp(pos1, pos2, pinhole.edgeCoordinate);
                    Handles.DrawWireDisc(pos, pos1 - pos2,  HandleUtility.GetHandleSize(pos) * 0.1f, 2);
                }
            }
        }
    }

}


