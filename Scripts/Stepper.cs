using Godot;
using MEC;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class Stepper : Node3D
{
	[Export] private CharacterBody3D _characterBody;
	[Export] private RayCast3D _stepUpRay;
	[Export] private RayCast3D _stepDownRay;

	[Export(PropertyHint.Range, "0.05f, 2f")] private float _durationToStepUp = 0.15f;
	[Export(PropertyHint.Range, "0.05f, 2f")] private float _durationToStepDown = 0.1f;
	private float _durationToStep;
	private float _deltaTime;

	[Export(PropertyHint.Range, "0.01f, 0.4f")] private float _maxElevation = 0.1f;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{

	}

    public override void _Process(double delta)
    {
        _deltaTime = (float)delta;
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 velocity = _characterBody.Velocity;

		bool grounded = _characterBody.IsOnFloor();

		Vector3 stepOffset = default(Vector3);
		bool step = false;

		step = FindStep(out stepOffset, velocity);

		if (step)
		{
			// Move to step cor
			//Timing.RunCoroutine(MoveToStep(_characterBody.Position, stepOffset, _durationToStep).CancelWith(_characterBody));
			_characterBody.Position = stepOffset;
		}
    }

	private IEnumerator<double> MoveToStep(Vector3 playerPos, Vector3 finalPoint, float duration)
	{
		Vector3 initialPos = playerPos;
		Vector3 endPos = finalPoint;

		float timeElapsed = 0f;
		float normalizedTime = 0f;

		do
		{
			timeElapsed += _deltaTime;
			normalizedTime = timeElapsed / duration;

			_characterBody.Position = playerPos.Lerp(endPos, normalizedTime);

			yield return Timing.WaitForOneFrame;
		}
		while (timeElapsed < duration);
	}

	private bool FindStep(out Vector3 stepOffset, Vector3 currVelocity)
	{
		stepOffset = default(Vector3);

		Vector2 velocityXZ = new Vector2(currVelocity.X, currVelocity.Z);

		if (velocityXZ.Length() < 0.1f)
			return false;

		bool step = ResolveStep(out stepOffset);

		if (step)
			return step;

		return false;
	}

	private bool ResolveStep(out Vector3 stepOffset)
	{
		stepOffset = default(Vector3);

		// Upper ray
		if (_stepUpRay.IsColliding())
		{
			if (_stepUpRay.GetCollisionNormal().Y >= 0.99f)
			{
				float elevation = Mathf.Abs(_stepUpRay.GetCollisionPoint().Y - _characterBody.Position.Y);

				if (elevation >= _maxElevation)
				{
					stepOffset = _stepUpRay.GetCollisionPoint();
					_durationToStep = _durationToStepUp;

					return true;
				}
			}
		}

		// Lower ray
		if (_stepDownRay.IsColliding())
		{
			if (_stepDownRay.GetCollisionNormal().Y >= 0.99f)
			{
				float elevation = Mathf.Abs(_stepDownRay.GetCollisionPoint().Y - _characterBody.Position.Y);

				if (elevation >= _maxElevation)
				{
					stepOffset = _stepDownRay.GetCollisionPoint();
					_durationToStep = _durationToStepDown;

					return true;
				}
			}
		}

		return false;
	}
}
