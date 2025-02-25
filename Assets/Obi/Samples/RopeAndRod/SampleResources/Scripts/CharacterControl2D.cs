using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterControl2D : MonoBehaviour {

	public float acceleration = 10;
	public float maxSpeed = 8;
	public float jumpPower = 2;
    public float floorRaycastDistance= 1.2f;

	private Rigidbody unityRigidbody;
	
	public void Awake(){
		unityRigidbody = GetComponent<Rigidbody>();
	}

    private void Update()
    {
        unityRigidbody.AddForce(new Vector3(Input.GetAxis("Horizontal") * acceleration, 0, 0));

        bool grounded = Physics.Raycast(new Ray(transform.position, -Vector3.up), floorRaycastDistance);

        if (Input.GetButtonDown("Jump") && grounded)
        {
            unityRigidbody.AddForce(Vector3.up * jumpPower, ForceMode.VelocityChange);
        }
    }

    void FixedUpdate () {
		unityRigidbody.velocity = Vector3.ClampMagnitude(unityRigidbody.velocity,maxSpeed);
	}
}
