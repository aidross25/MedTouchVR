using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace Obi
{
    [ExecuteAlways]
    public class ObiActorEditorSelectionHandler
    {
        private static HashSet<ObiSolver> solvers = new HashSet<ObiSolver>();
        private static ObiSolver clickedSolver;
        private static int particleIndex;

        #if UNITY_EDITOR
        internal static void SolverInitialized(ObiSolver solver)
        {
            if (solver != null)
            {
                if (solvers.Count == 0)
                    Init();

                if (solver != null)
                    solvers.Add(solver);
            }
 
        }

        internal static void SolverTeardown(ObiSolver solver)
        {
            if (solver != null)
            {
                solvers.Remove(solver);

                if (solvers.Count == 0)
                    Dispose();
            }
        }

        internal static void Init()
        {
            SceneView.duringSceneGui += SceneGui;
        }

        internal static void Dispose()
        {
            SceneView.duringSceneGui -= SceneGui;
        }

        private static bool RaycastAgainstSolver(ObiSolver solver, Ray ray, float thickness, out int pIndex, out float distance)
        {
            float closestDistanceToRay = float.MaxValue;
            pIndex = -1;
            distance = float.MaxValue;

            Matrix4x4 solver2World = solver.transform.localToWorldMatrix;

            // Find the closest particle hit by the ray:
            for (int i = 0; i < solver.activeParticleCount; ++i)
            {
                int p = solver.activeParticles[i];

                Vector3 worldPos = solver2World.MultiplyPoint3x4(solver.positions[p]);
                Vector3 projected = ObiUtils.ProjectPointLine(ray.origin, ray.origin + ray.direction, worldPos, out float mu, false);

                // Disregard particles behind the camera:
                if (mu < 0)
                    continue;

                float radius = solver.principalRadii[p][0] + thickness;
                float distanceToRay = Vector3.SqrMagnitude(worldPos - projected);

                if (distanceToRay <= radius * radius &&
                    distanceToRay < closestDistanceToRay &&
                    mu < closestDistanceToRay)
                {
                    distance = mu;
                    closestDistanceToRay = distanceToRay;
                    pIndex = p;
                }
            }

            return pIndex >= 0;
        }

        private static ObiSolver RaycastAllSolvers(Ray ray, out int pIndex, out float distance)
        {
            distance = float.MaxValue;
            pIndex = -1;

            ObiSolver hitSolver = null;
            
            foreach (ObiSolver s in solvers)
            {
                if (s == null || !s.bounds.IntersectRay(ray))
                    continue;

                if (RaycastAgainstSolver(s, ray, 0, out int p, out float d))
                {
                    if (d < distance)
                    {
                        distance = d;
                        hitSolver = s;
                        pIndex = p;
                    }
                }
            }
            return hitSolver;
        }

        private static void SceneGui(SceneView sceneView)
        {
            if (EditorApplication.isPaused || !ObiEditorSettings.GetOrCreateSettings().sceneViewParticlePicking)
                return;

            // only do this in the main stage or prefab stage, if we're in any other stage don't raycast against particles.
            // This will prevent selecting stuff when in the blueprint editor, avatar editor, or other stages.
            var stage = StageUtility.GetCurrentStage();
            if (!(stage is MainStage) && !(stage is PrefabStage))
                return;

            var evt = Event.current;

            if (evt.alt)
                return;

            float ppp = EditorGUIUtility.pixelsPerPoint;
            int mouseScreenX = (int)(evt.mousePosition.x * ppp);
            int mouseScreenY = (int)(evt.mousePosition.y * ppp);

            if (mouseScreenX < 0 || mouseScreenX >= sceneView.camera.pixelWidth ||
                mouseScreenY < 0 || mouseScreenY >= sceneView.camera.pixelHeight)
                return;

            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            switch (evt.type)
            {
                case EventType.Layout:
                case EventType.MouseMove:

                    if (!Tools.viewToolActive)
                    {
                        // Raycast against all solvers:
                        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                        clickedSolver = RaycastAllSolvers(ray, out particleIndex, out float closest);

                        // check whether we hit a collider before the closest particle in the solver:
                        RaycastHit hit;
                        ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                        if (Physics.Raycast(ray, out hit, closest) && hit.collider != null)
                            clickedSolver = null;

                        // If we hit something, register our control ID and the distance to the particle:
                        if (clickedSolver != null && Camera.current != null)
                        {
                            var worldPos = clickedSolver.transform.TransformPoint(clickedSolver.positions[particleIndex]);
                            var screenCenter = HandleUtility.WorldToGUIPoint(worldPos);

                            var distance = (Event.current.mousePosition - screenCenter).magnitude;

                            HandleUtility.AddControl(controlID, distance);

                            // AddDefaultControl means that if no other control is selected, this will be chosen as the fallback. 
                            // This allows things like the translate handle and buttons to function. 
                            HandleUtility.AddDefaultControl(controlID);
                        }
                    }

                    break;

                case EventType.MouseDown:

                    if (evt.button == 0 && HandleUtility.nearestControl == controlID && clickedSolver != null)
                    {
                        // Setting the hotControl tells the Scene View that this mouse down/up event cannot be considered 
                        // a picking action because the event is in use. 
                        GUIUtility.hotControl = controlID;
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:

                    if (!Tools.viewToolActive && GUIUtility.hotControl == controlID)
                    {
                        // In case we hit some actor, select the actor it belongs to:
                        if (clickedSolver != null)
                        {
                            var clickedActor = clickedSolver.particleToActor[particleIndex].actor;
                            if (clickedActor != null)
                            {
                                var selection = Selection.objects.ToList();

                                if (evt.shift || evt.control)
                                {
                                    if (selection.Contains(clickedActor.gameObject))
                                        selection.Remove(clickedActor.gameObject);
                                    else
                                        selection.Add(clickedActor.gameObject);
                                }
                                else
                                {
                                    selection.Clear();
                                    selection.Add(clickedActor.gameObject);
                                }

                                Selection.objects = selection.ToArray(); 
                            }

                            GUIUtility.hotControl = 0;
                            evt.Use();
                        }
                    }

                    break;
            }
        }
#endif
    }
}
