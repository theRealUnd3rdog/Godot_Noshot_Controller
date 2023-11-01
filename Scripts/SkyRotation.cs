using Godot;
using System;

public partial class SkyRotation : WorldEnvironment
{
	[Export] private float _skyRotationSpeed = 90f;
	private Vector3 rotationAxis = new Vector3(0, 1, 0);
	private float currentRotationAngle = 0.0f; // Track the current rotation angle
    
	public override void _Process(double delta)
	{
		// Calculate the target rotation angle for this frame
        float targetRotationAngle = currentRotationAngle + (_skyRotationSpeed * (float)delta);

        // Use lerp to gradually increase the rotation angle from the current angle
        currentRotationAngle = Mathf.Lerp(currentRotationAngle, targetRotationAngle, 0.1f); // You can adjust the lerp speed (0.1f) as needed

        // Apply the rotation
        Environment.SkyRotation = rotationAxis * Mathf.DegToRad(currentRotationAngle);
	}
}
