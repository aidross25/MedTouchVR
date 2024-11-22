using UnityEngine;

public class VRStartPosition : MonoBehaviour
{
    public Vector3 defaultPosition = new Vector3(0, 1.6f, 0);  // Default height for VR user
    public Quaternion defaultRotation = Quaternion.identity;    // Default rotation
    
    void Start()
    {
        // Set the default position and rotation
        transform.position = defaultPosition;
        transform.rotation = defaultRotation;
    }
}
