using Godot;
using System;

public partial class MotionBlur : MeshInstance3D
{
	private Camera3D _cam;

	private Vector3 _camPosPrev = Vector3.Zero;
	private Quaternion _camRotPrev = Quaternion.Identity;
	private ShaderMaterial _material;

    public override void _Ready()
    {
		_cam = PostProcessingManager.Instance.mainCamera;
		_material = (ShaderMaterial)MaterialOverride;
    }

    public override void _Process(double delta)
    {
		// Linear velocity is just difference in pos between 2 frames
		Vector3 velocity =  _cam.GlobalTransform.Origin - _camPosPrev;

		// Angular velocity
		Quaternion camRot = _cam.GlobalTransform.Basis.GetRotationQuaternion();
		Quaternion camRotDiff = camRot - _camRotPrev;
		Quaternion camRotConj = Conjugate(camRot);
		Quaternion angleVelocity = camRotDiff * 2.0f * camRotConj;

		
		Vector3 angleVelVector = new Vector3(Mathf.Abs(angleVelocity.X), Mathf.Abs(angleVelocity.Y), angleVelocity.Z);
		//material.SetShaderParameter("linear_velocity", new Vector3(Mathf.Abs(velocity.X), Mathf.Abs(velocity.Y), velocity.Z));
		_material.SetShaderParameter("angular_velocity", angleVelVector);

		_camPosPrev = _cam.GlobalTransform.Origin;
		_camRotPrev = _cam.GlobalTransform.Basis.GetRotationQuaternion();
    }

	// Calculate the conjugate of the quaternion
	private Quaternion Conjugate(Quaternion quat)
	{
		Quaternion conjugate = new Quaternion(-quat.X, -quat.Y, -quat.Z, quat.W);
		return conjugate;
	}
}
