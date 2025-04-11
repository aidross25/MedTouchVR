using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{

    /**
     * Custom inspector for ObiSolver components.
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
    [CustomEditor(typeof(ObiSolver)), CanEditMultipleObjects]
    public class ObiSolverEditor : Editor
    {

        [MenuItem("GameObject/3D Object/Obi/Obi Solver", false, 100)]
        static void CreateObiSolver(MenuCommand menuCommand)
        {
            GameObject go = ObiEditorUtils.CreateNewSolver();
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Selection.activeGameObject = go;
        }

        ObiSolver solver;

        SerializedProperty backend;
        SerializedProperty substeps;
        SerializedProperty maxStepsPerFrame;
        SerializedProperty synchronization;
        SerializedProperty simulateWhenInvisible;
        SerializedProperty parameters;
        SerializedProperty gravity;
        SerializedProperty gravitySpace;
        SerializedProperty ambientWind;
        SerializedProperty windSpace;
        SerializedProperty worldLinearInertiaScale;
        SerializedProperty worldAngularInertiaScale;

        SerializedProperty foamSubsteps;
        SerializedProperty maxFoamVelocityStretch;
        SerializedProperty foamFade;
        SerializedProperty foamAccelAgingRange;
        SerializedProperty foamAccelAging;

        SerializedProperty distanceConstraintParameters;
        SerializedProperty bendingConstraintParameters;
        SerializedProperty particleCollisionConstraintParameters;
        SerializedProperty particleFrictionConstraintParameters;
        SerializedProperty collisionConstraintParameters;
        SerializedProperty frictionConstraintParameters;
        SerializedProperty skinConstraintParameters;
        SerializedProperty volumeConstraintParameters;
        SerializedProperty shapeMatchingConstraintParameters;
        SerializedProperty tetherConstraintParameters;
        SerializedProperty pinConstraintParameters;
        SerializedProperty pinholeConstraintParameters;
        SerializedProperty stitchConstraintParameters;
        SerializedProperty densityConstraintParameters;
        SerializedProperty stretchShearConstraintParameters;
        SerializedProperty bendTwistConstraintParameters;
        SerializedProperty chainConstraintParameters;

        SerializedProperty maxSurfaceChunks;
        SerializedProperty maxQueryResults;
        SerializedProperty maxFoamParticles;
        SerializedProperty maxParticleNeighbors;
        SerializedProperty maxParticleContacts;

        BooleanPreference solverFoldout;
        BooleanPreference simulationFoldout;
        BooleanPreference advectionFoldout;
        BooleanPreference collisionsFoldout;
        BooleanPreference constraintsFoldout;
        BooleanPreference memoryFoldout;

        GUIContent constraintLabelContent;

        public void OnEnable()
        {
            solver = (ObiSolver)target;
            constraintLabelContent = new GUIContent();

            solverFoldout = new BooleanPreference($"{target.GetType()}.solverFoldout", true);
            simulationFoldout = new BooleanPreference($"{target.GetType()}.simulationFoldout", false);
            advectionFoldout = new BooleanPreference($"{target.GetType()}.advectionFoldout", false);
            collisionsFoldout = new BooleanPreference($"{target.GetType()}.collisionsFoldout", false);
            constraintsFoldout = new BooleanPreference($"{target.GetType()}.constraintsFoldout", false);
            memoryFoldout = new BooleanPreference($"{target.GetType()}.memoryFoldout", false);

            backend = serializedObject.FindProperty("m_Backend");
            substeps = serializedObject.FindProperty("substeps");
            maxStepsPerFrame = serializedObject.FindProperty("maxStepsPerFrame");
            synchronization = serializedObject.FindProperty("synchronization");
            simulateWhenInvisible = serializedObject.FindProperty("simulateWhenInvisible");
            parameters = serializedObject.FindProperty("parameters");
            gravity = serializedObject.FindProperty("gravity");
            gravitySpace = serializedObject.FindProperty("gravitySpace");
            ambientWind = serializedObject.FindProperty("ambientWind");
            windSpace = serializedObject.FindProperty("windSpace");
            worldLinearInertiaScale = serializedObject.FindProperty("worldLinearInertiaScale");
            worldAngularInertiaScale = serializedObject.FindProperty("worldAngularInertiaScale");

            foamSubsteps = serializedObject.FindProperty("foamSubsteps");
            maxFoamVelocityStretch = serializedObject.FindProperty("maxFoamVelocityStretch");
            foamFade = serializedObject.FindProperty("foamFade");
            foamAccelAgingRange = serializedObject.FindProperty("foamAccelAgingRange");
            foamAccelAging = serializedObject.FindProperty("foamAccelAging");

            distanceConstraintParameters = serializedObject.FindProperty("distanceConstraintParameters");
            bendingConstraintParameters = serializedObject.FindProperty("bendingConstraintParameters");
            particleCollisionConstraintParameters = serializedObject.FindProperty("particleCollisionConstraintParameters");
            particleFrictionConstraintParameters = serializedObject.FindProperty("particleFrictionConstraintParameters");
            collisionConstraintParameters = serializedObject.FindProperty("collisionConstraintParameters");
            frictionConstraintParameters = serializedObject.FindProperty("frictionConstraintParameters");
            skinConstraintParameters = serializedObject.FindProperty("skinConstraintParameters");
            volumeConstraintParameters = serializedObject.FindProperty("volumeConstraintParameters");
            shapeMatchingConstraintParameters = serializedObject.FindProperty("shapeMatchingConstraintParameters");
            tetherConstraintParameters = serializedObject.FindProperty("tetherConstraintParameters");
            pinConstraintParameters = serializedObject.FindProperty("pinConstraintParameters");
            pinholeConstraintParameters = serializedObject.FindProperty("pinholeConstraintParameters");
            stitchConstraintParameters = serializedObject.FindProperty("stitchConstraintParameters");
            densityConstraintParameters = serializedObject.FindProperty("densityConstraintParameters");
            stretchShearConstraintParameters = serializedObject.FindProperty("stretchShearConstraintParameters");
            bendTwistConstraintParameters = serializedObject.FindProperty("bendTwistConstraintParameters");
            chainConstraintParameters = serializedObject.FindProperty("chainConstraintParameters");

            maxSurfaceChunks = serializedObject.FindProperty("m_MaxSurfaceChunks");
            maxQueryResults = serializedObject.FindProperty("maxQueryResults");
            maxFoamParticles = serializedObject.FindProperty("maxFoamParticles");
            maxParticleNeighbors = serializedObject.FindProperty("maxParticleNeighbors");
            maxParticleContacts = serializedObject.FindProperty("maxParticleContacts");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            EditorGUILayout.HelpBox("Particles:" + solver.allocParticleCount + "\n" +
                                    "Simplices:" + solver.simplexCounts.simplexCount + "\n" +
                                    "Contacts:" + solver.contactCount + "\n" +
                                    "Simplex contacts:" + solver.particleContactCount, MessageType.None);

            solverFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(solverFoldout, "Solver settings");
            if (solverFoldout)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(backend);

#if !(OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
                if (backend.enumValueIndex == (int)ObiSolver.BackendType.Burst)
                    EditorGUILayout.HelpBox("The Burst backend depends on the following packages: Mathematics, Collections, Jobs and Burst. Please install the required dependencies. The solver will try to fall back to the Compute backend instead.", MessageType.Warning);
#endif
                if (!SystemInfo.supportsComputeShaders)
                {
                    EditorGUILayout.HelpBox("This platform doesn't support compute shaders. Please switch to the Burst backend.", MessageType.Error);
                }


                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    foreach (var t in targets)
                        (t as ObiSolver).UpdateBackend();
                }

                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("mode"));
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("interpolation"));
                EditorGUILayout.PropertyField(synchronization);
                EditorGUILayout.PropertyField(substeps);
                EditorGUILayout.PropertyField(maxStepsPerFrame);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            simulationFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(simulationFoldout, "Simulation settings");
            if (simulationFoldout)
            {
                EditorGUILayout.PropertyField(gravitySpace);
                EditorGUILayout.PropertyField(gravity);
                EditorGUILayout.PropertyField(windSpace);
                EditorGUILayout.PropertyField(ambientWind);
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("sleepThreshold"));
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("maxVelocity"));
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("maxAngularVelocity"));
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("damping"));
                EditorGUILayout.PropertyField(worldLinearInertiaScale);
                EditorGUILayout.PropertyField(worldAngularInertiaScale);
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("maxAnisotropy"));
                EditorGUILayout.PropertyField(simulateWhenInvisible);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            advectionFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(advectionFoldout, "Foam settings");
            if (advectionFoldout)
            {
                EditorGUILayout.PropertyField(foamSubsteps);
                EditorGUILayout.PropertyField(maxFoamVelocityStretch);
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("foamGravityScale"));
                EditorGUILayout.PropertyField(foamFade);
                EditorGUILayout.PropertyField(foamAccelAgingRange);
                EditorGUILayout.PropertyField(foamAccelAging);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            collisionsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(collisionsFoldout, "Collision settings");
            if (collisionsFoldout)
            {
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("colliderCCD"));
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("particleCCD"));
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("collisionMargin"));
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("maxDepenetration"));
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("shockPropagation"));
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("surfaceCollisionIterations"));
                EditorGUILayout.PropertyField(parameters.FindPropertyRelative("surfaceCollisionTolerance"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            constraintsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(constraintsFoldout, "Constraint settings");
            if (constraintsFoldout)
            {
                constraintLabelContent.text = "Distance";
                EditorGUILayout.PropertyField(distanceConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Bending";
                EditorGUILayout.PropertyField(bendingConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Particle collision / Queries";
                EditorGUILayout.PropertyField(particleCollisionConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Particle friction";
                EditorGUILayout.PropertyField(particleFrictionConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Collision";
                EditorGUILayout.PropertyField(collisionConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Friction";
                EditorGUILayout.PropertyField(frictionConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Skin";
                EditorGUILayout.PropertyField(skinConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Volume";
                EditorGUILayout.PropertyField(volumeConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Shape matching";
                EditorGUILayout.PropertyField(shapeMatchingConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Tether";
                EditorGUILayout.PropertyField(tetherConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Pin";
                EditorGUILayout.PropertyField(pinConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Pinhole";
                EditorGUILayout.PropertyField(pinholeConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Stitch";
                EditorGUILayout.PropertyField(stitchConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Density";
                EditorGUILayout.PropertyField(densityConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Stretch & Shear";
                EditorGUILayout.PropertyField(stretchShearConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Bend & Twist";
                EditorGUILayout.PropertyField(bendTwistConstraintParameters, constraintLabelContent);

                constraintLabelContent.text = "Chain";
                EditorGUILayout.PropertyField(chainConstraintParameters, constraintLabelContent);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            memoryFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(memoryFoldout, "Memory budget");
            if (memoryFoldout)
            {
                EditorGUILayout.PropertyField(maxQueryResults);
                EditorGUILayout.PropertyField(maxFoamParticles);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(maxSurfaceChunks);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    foreach (var t in targets)
                        (t as ObiSolver).dirtyRendering |= (int)Oni.RenderingSystemType.Fluid;
                }

                EditorGUILayout.PropertyField(maxParticleNeighbors);
                EditorGUILayout.PropertyField(maxParticleContacts);

                uint usedChunks = solver.usedSurfaceChunks;
                float usagePercentage = usedChunks / (float)maxSurfaceChunks.intValue;
                uint foamParticles = solver.initialized ? solver.implementation.activeFoamParticleCount : 0;

                // memory consumption per chunk:
                // (8 + 12 + 64*4 + 64*6*4 + 64*16) = 2836 bytes
                EditorGUILayout.HelpBox("Active foam particles: " + foamParticles + "/" + maxFoamParticles.intValue + "\n"+ 
                                        "Surface memory (Mb): " + string.Format("{0:N2}", maxSurfaceChunks.intValue * 0.002836f)+ "\n"+
                                        "Used surface chunks: "+ usedChunks + "/"+ maxSurfaceChunks.intValue + ", hashtable usage "+ string.Format("{0:N1}", usagePercentage * 100) + "%", MessageType.None);

                if (usagePercentage >= 0.5f)
                {
                    EditorGUILayout.HelpBox("Hashtable usage should be below 50% for best performance. Increase max surface chunks if % is too high.", MessageType.Warning);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Apply changes to the serializedProperty
            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
                solver.PushSolverParameters();
            }

        }

        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.Selected)]
        static void DrawGizmoForSolver(ObiSolver solver, GizmoType gizmoType)
        {

            if ((gizmoType & GizmoType.InSelectionHierarchy) != 0)
            {

                Gizmos.color = new Color(1, 1, 1, 0.5f);
                var bounds = solver.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }

        }

    }
}


