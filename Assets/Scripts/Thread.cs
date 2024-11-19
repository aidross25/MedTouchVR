using UnityEngine;
using System.Collections;

public class Thread : MonoBehaviour
{
    public int numSegments = 10;                // Number of segments in the thread
    public float segmentLength = 0.5f;          // Length between each segment
    public float springStrength = 50f;          // Spring force to simulate tension
    public float damping = 0.5f;               // Damping to slow down the movement
    public float gravity = 9.81f;              // Simulate gravity for the thread
    public Transform target;                   // The target object that the thread will follow
    public Material ribbonMaterial;            // Material to use for the LineRenderer

    public float ribbonWidth = 0.1f;           // Width of the ribbon (LineRenderer)

    private GameObject[] segments;             // The thread segments
    private LineRenderer lineRenderer;         // The LineRenderer for visualizing the thread

    private Vector3[] velocities;              // Velocity for each segment to simulate damping and movement
    private Vector3[] forces;                  // Forces acting on each segment

    void Start()
    {
        // Initialize the segments and other properties
        segments = new GameObject[numSegments];
        velocities = new Vector3[numSegments];
        forces = new Vector3[numSegments];

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = ribbonMaterial;
        lineRenderer.positionCount = numSegments;

        // Set the width of the ribbon based on the parameter
        lineRenderer.startWidth = ribbonWidth;
        lineRenderer.endWidth = ribbonWidth;

        CreateThread();
    }

    void CreateThread()
    {
        // Create the segments and set up the initial positions
        for (int i = 0; i < numSegments; i++)
        {
            GameObject segment = new GameObject("Segment_" + i);
            if (i == 0)
            {
                // Start with the first segment near the target (you can adjust this)
                segment.transform.position = target.position + Vector3.down * segmentLength; 
            }
            else
            {
                segment.transform.position = segments[i - 1].transform.position - Vector3.up * segmentLength;
            }
            
            segment.AddComponent<Rigidbody>().isKinematic = true; // No physics interaction with the environment
            segments[i] = segment;
        }
    }

    void Update()
    {
        // Apply forces and update positions
        ApplyForces();
        UpdatePositions();
        DrawRibbon();
    }

    void ApplyForces()
    {
        // Reset forces
        for (int i = 0; i < numSegments; i++)
        {
            forces[i] = Vector3.zero;
        }

        // The last segment follows the target's position
        segments[numSegments - 1].transform.position = target.position;

        // Apply gravity to all segments
        forces[0] = Vector3.down * gravity;  // Apply gravity to the first segment

        // Apply spring forces between segments (spring-chain behavior)
        for (int i = 1; i < numSegments; i++)
        {
            Vector3 direction = segments[i - 1].transform.position - segments[i].transform.position;
            float distance = direction.magnitude;

            // Calculate spring force based on Hooke's law (F = -k * x)
            float springForce = (distance - segmentLength) * springStrength;
            Vector3 springDirection = direction.normalized;

            // Apply the spring force with some damping
            forces[i] += springDirection * springForce;
            forces[i] -= velocities[i] * damping;  // Apply damping to the velocity
        }

        // Update velocities and positions using simple Euler integration
        for (int i = 0; i < numSegments; i++)
        {
            velocities[i] += forces[i] * Time.deltaTime;  // Update velocity with force
            segments[i].transform.position += velocities[i] * Time.deltaTime; // Update position based on velocity

            // Prevent excessive velocity that can cause the segments to shoot out of control
            velocities[i] = Vector3.ClampMagnitude(velocities[i], 5f); // Clamp max velocity
        }
    }

    void UpdatePositions()
    {
        // Ensure each segment is being connected to the next one
        for (int i = 0; i < numSegments; i++)
        {
            lineRenderer.SetPosition(i, segments[i].transform.position);
        }
    }

    void DrawRibbon()
    {
        // Update the LineRenderer's positions based on the segment positions
        for (int i = 0; i < numSegments; i++)
        {
            lineRenderer.SetPosition(i, segments[i].transform.position);
        }
    }
}
