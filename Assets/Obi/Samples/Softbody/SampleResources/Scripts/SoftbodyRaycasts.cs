using System.Collections.Generic;
using UnityEngine;
using Obi;

public class SoftbodyRaycasts : MonoBehaviour
{
    public ObiSolver solver;
    public LineRenderer[] lasers;
    int filter;

    int queryOffset = -1;
    List<QueryResult> raycastResults = new List<QueryResult>();

    private void Start()
    {
        filter = ObiUtils.MakeFilter(ObiUtils.CollideWithEverything, 0);
        solver.OnSpatialQueryResults += Solver_OnSpatialQueryResults;
        solver.OnSimulationStart += Solver_OnSimulate;
    }

    private void OnDestroy()
    {
        solver.OnSpatialQueryResults -= Solver_OnSpatialQueryResults;
        solver.OnSimulationStart -= Solver_OnSimulate;
    }

    private void Solver_OnSimulate(ObiSolver s, float timeToSimulate, float substepTime)
    {
        raycastResults.Clear();
        queryOffset = solver.pendingQueryCount;

        for (int i = 0; i < lasers.Length; ++i)
        {
            lasers[i].useWorldSpace = true;
            lasers[i].positionCount = 2;
            lasers[i].SetPosition(0, lasers[i].transform.position);
            solver.EnqueueRaycast(new Ray(lasers[i].transform.position, lasers[i].transform.up), filter, 20);
            raycastResults.Add(new QueryResult { distanceAlongRay = 20, simplexIndex = -1, queryIndex = -1 });
        }
    }

    private void Solver_OnSpatialQueryResults(ObiSolver s, ObiNativeQueryResultList queryResults)
    {
        for (int i = 0; i < queryResults.count; ++i)
        {
            int raycastIndex = queryResults[i].queryIndex - queryOffset;
            if (raycastIndex >= 0 && raycastIndex < raycastResults.Count &&
                queryResults[i].distanceAlongRay < raycastResults[raycastIndex].distanceAlongRay)
                raycastResults[raycastIndex] = queryResults[i];
        }

        for (int i = 0; i < raycastResults.Count; ++i)
        {
            lasers[i].SetPosition(1, lasers[i].transform.position + lasers[i].transform.up * raycastResults[i].distanceAlongRay);

            if (raycastResults[i].simplexIndex >= 0)
            {
                lasers[i].startColor = Color.red;
                lasers[i].endColor = Color.red;
            }
            else
            {
                lasers[i].startColor = Color.blue;
                lasers[i].endColor = Color.blue;
            }
        }

    }
}
