using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{

    [CustomEditor(typeof(ObiRopeCursor)), CanEditMultipleObjects]
    public class ObiRopeCursorEditor : Editor
    {
        SerializedProperty cursorMu;
        SerializedProperty sourceMu;
        SerializedProperty direction;

        public void OnEnable()
        {
            cursorMu = serializedObject.FindProperty("m_CursorMu");
            sourceMu = serializedObject.FindProperty("m_SourceMu");
            direction = serializedObject.FindProperty("direction"); 
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(cursorMu);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var t in targets)
                    (t as ObiRopeCursor).UpdateCursor();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(sourceMu);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var t in targets)
                    (t as ObiRopeCursor).UpdateSource();
            }

            EditorGUILayout.PropertyField(direction);

            // Apply changes to the serializedProperty
            if (GUI.changed)
                serializedObject.ApplyModifiedProperties();

        }

        private static void DrawArrow()
        {
            Gizmos.DrawLine(Vector3.left, Vector3.up);
            Gizmos.DrawLine(Vector3.right, Vector3.up);
            Gizmos.DrawLine(Vector3.left, Vector3.down);
            Gizmos.DrawLine(Vector3.right, Vector3.down);
            Gizmos.DrawLine(Vector3.left, Vector3.forward);
            Gizmos.DrawLine(Vector3.right, Vector3.forward);
            Gizmos.DrawLine(Vector3.up, Vector3.forward);
            Gizmos.DrawLine(Vector3.down, Vector3.forward);
        }

        [DrawGizmo(GizmoType.Selected)]
        private static void DrawGizmos(ObiRopeCursor cursor, GizmoType gizmoType)
        {
            var rope = cursor.GetComponent<ObiRope>();
            if (rope.isLoaded)
            {
                Handles.matrix = rope.solver.transform.localToWorldMatrix;
                Handles.color = new Color(1, 0.5f, 0.2f, 1);

                // draw source particle:
                int sourceIndex = cursor.sourceParticleIndex;

                if (sourceIndex >= 0 && rope.IsParticleActive(rope.solver.particleToActor[sourceIndex].indexInActor))
                {
                    Vector3 pos = rope.solver.positions[sourceIndex];
                    float size = HandleUtility.GetHandleSize(pos) * 0.15f;
                    Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);
                }

                // draw cursor:
                var element = cursor.cursorElement;

                if (element != null && element.particle1 != element.particle2)
                {
                    Vector3 pos = rope.solver.positions[cursor.direction ? element.particle1 : element.particle2];
                    Vector3 pos2 = rope.solver.positions[cursor.direction ? element.particle2 : element.particle1];
                    Vector3 direction = pos2 - pos;

                    float size = HandleUtility.GetHandleSize(pos) * 0.25f;
                    Handles.ConeHandleCap(0, pos + Vector3.Normalize(direction)*size*0.5f, Quaternion.LookRotation(direction), size, EventType.Repaint);
                }
            }
        }
    }
}

