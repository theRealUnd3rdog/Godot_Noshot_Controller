using Godot;
using System;
using System.Collections.Generic;
using MEC;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class Wallrunning : MovementState
{
    [Export] private float _wallRunTime = 3f;
	[Export] private float _wallRunSpeed = 15f;
	[Export] private float _wallFrictionCoefficient = 0.5f;
    private float _wallFrictionForce;
	[Export] private float _wallJumpSpeed = 4.5f;
	
	private float _wallRunTimer = 0.0f;
    private WallDirection _wallDirection;

    private CamShakeInstance _wallrunShake;

    [ExportSubgroup("Audio")]
    [Export] private AudioStreamPlayer3D _wallrun;
    private string _movementBus = "Movement";
	private int _movementBusIndex;

    public override void Awake()
    {
        base.Enter();

        _movementBusIndex = AudioServer.GetBusIndex(_movementBus);
    }

    public override void Enter()
    {
        base.Enter();
        
        _wallFrictionForce = _wallFrictionCoefficient * Movement.gravity;
        Wallrunning.CheckWallCollision(Movement, Camera, out KinematicCollision3D col, out _wallDirection);

        Movement.AnimationPlayer.Set("parameters/Master/conditions/wallrun", true);

        Movement.AnimationPlayer.Set("parameters/Master/Wallrun/conditions/left_wallrun", _wallDirection == WallDirection.Right);
        Movement.AnimationPlayer.Set("parameters/Master/Wallrun/conditions/right_wallrun", _wallDirection == WallDirection.Left);

        _wallrunShake = CamShake.ShakePreset(CamShakePresets.Sliding);
        PlayWallRun();
    }

    public override void Exit()
    {
        Movement.AnimationPlayer.Set("parameters/Master/conditions/wallrun", false);

        Movement.AnimationPlayer.Set("parameters/Master/Wallrun/conditions/left_wallrun", false);
        Movement.AnimationPlayer.Set("parameters/Master/Wallrun/conditions/right_wallrun", false);

        CamShake.RemoveShake(_wallrunShake);
        StopWallRun(this);
    }

    public override void Update(double delta)
    {

    }

    public override void PhysicsUpdate(double delta)
    {
        Vector3 rayDirection = _wallDirection == WallDirection.Right ? Camera.GetNeckBasis().X : -Camera.GetNeckBasis().X;

        bool wall = Movement.SendRayInDirection(rayDirection.Normalized(), Camera.GlobalPosition, 1f, out Vector3 wallNormal, out Vector3 wallPoint);

        if (wall && _wallRunTimer <= _wallRunTime)
        {
            _wallRunTimer += (float)delta;
        
            float normalizedTime = _wallRunTimer / _wallRunTime;
            Vector3 rotatedVector = wallNormal.Rotated(Vector3.Up, Mathf.DegToRad(_wallDirection == WallDirection.Right ? -90f : 90f));

            Movement.SetPlayerDirection(rotatedVector);
            Movement.SetCurrentSpeed(Mathf.Lerp(Movement.GetCurrentSpeed(), _wallRunSpeed, normalizedTime));

            // Decrease gravity with friction force over time
            float yVelocity = Movement.Velocity.Y;

            yVelocity -= _wallFrictionForce * (float)delta;
            Movement.SetYVelocity(yVelocity);

            // Increase gravity over time
            _wallFrictionForce = Mathf.Lerp(_wallFrictionForce, Movement.gravity, normalizedTime / 4);

            /* DebugDraw3D.DrawArrow(wallPoint, wallPoint + (rotatedVector * 1f), Colors.Aqua, 0.2f);
            DebugDraw3D.DrawArrow(wallPoint + (rotatedVector * 1f), wallPoint + (wallNormal * 1f), Colors.IndianRed, 0.1f); */

            if (Input.IsActionJustPressed("jump"))
            {
                WallJump(wallNormal);
                EmitSignal(SignalName.StateFinished, "Jump", new());
            }
        }
        else
        {
            EmitSignal(SignalName.StateFinished, "Air", new());
        }
    }

    public enum WallDirection
    {
        Left,
        Right
    }

    public static bool CheckWall(Movement movement, Vector3 origin, Basis basis, out Vector3 wNormal, out Vector3 direction)
    {
        direction = default;
        wNormal = default;

        if (movement.GetRawInputDirection().Y >= 0)
            return false;

        Vector3[] directions = { basis.X, -basis.X};
        WallDirection[] possibleDirections = { WallDirection.Right, WallDirection.Left };
        float[] rotationAngles = { -90f, 90f }; // -90 for Right, 90 for Left

        for (int i = 0; i < directions.Length; i++)
        {
            if (movement.SendRayInDirection(directions[i], origin, 0.5f, out Vector3 normal, out Vector3 point))
            {
                float dotCollision = Mathf.Abs(normal.Dot(Vector3.Up)); // Check if the wall is sideways

                if (dotCollision < 0.1f)
                {
                    Vector3 playerForward = -basis.Z;
                    float angleToWall = Mathf.RadToDeg(playerForward.AngleTo(normal));

                    if (angleToWall < 105f && angleToWall > 25f)
                    {
                        Vector3 rotatedVector = normal.Rotated(Vector3.Up, Mathf.DegToRad(rotationAngles[i]));

                        DebugDraw3D.DrawArrow(point, point + (rotatedVector * 1f), Colors.Aqua, 0.2f);
                        DebugDraw3D.DrawArrow(point + (rotatedVector * 1f), point + (normal * 1f), Colors.IndianRed, 0.1f);

                        direction = rotatedVector; // Set the correct wall direction
                        wNormal = normal;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static bool CheckWallCollision(Movement movement, Camera camera, out KinematicCollision3D collision, out WallDirection direction)
	{
		int count = movement.GetSlideCollisionCount();
		direction = default;
		collision = default(KinematicCollision3D);

		if (count <= 0)
			return false;
		
		if (movement.GetRawInputDirection().Y >= 0)
			return false;
		
		List<KinematicCollision3D> collisions = new List<KinematicCollision3D>();
		
		for (int i = 0; i < count; i++)
		{
			collisions.Add(movement.GetSlideCollision(i));
		}

		foreach (KinematicCollision3D c in collisions)
		{
			Vector3 collisionNormal = c.GetNormal();
			Vector3 collisionPoint = c.GetPosition();

			float dotCollision = Mathf.Abs(collisionNormal.Dot(Vector3.Up)); // Get the dot product to see if the wall is side ways

			if (dotCollision < 0.1f)
			{
				Vector3 playerForward = camera.GetNeckBasis().Z;
				float angleToWall = Mathf.RadToDeg(playerForward.AngleTo(collisionNormal));
				float signedAngle = Mathf.RadToDeg(playerForward.SignedAngleTo(collisionNormal, Vector3.Up));	

				// Check if camera is facing somewhat in that direction
				if (angleToWall < 105f && angleToWall > 25f)
				{
					//DebugDraw3D.DrawSquare(c.GetPosition(), 0.1f, Colors.Blue);

					direction = Mathf.Sign(signedAngle) > 0 ? WallDirection.Left : WallDirection.Right;
					collision = c;
					
					//GD.Print(direction);
					
					return true;
				}
			}
		}

		return false;
	}

    public void WallJump(Vector3 wallDir)
	{
        Vector3 direction = ((wallDir.Normalized()) + (-Camera.GetNeckBasis().Z / 4)) * (_wallJumpSpeed / 16);
        Movement.SetCurrentSpeed(_wallJumpSpeed * 3f);
        Movement.SetPlayerDirection(direction);
	}

    public float GetWallRunTime() => _wallRunTime;
    public float GetWallRunTimer() => _wallRunTimer;
    public void SetWallRunTimer(float time) => _wallRunTimer = time;

    // Audio
    #region AUDIO
    private void PlayWallRun()
	{
		Timing.KillCoroutines("Wallrun");

		_wallrun.PitchScale = 1f;

		if (!_wallrun.Playing)
			_wallrun.Play();

		AudioEffect lowPass = AudioServer.GetBusEffect(_movementBusIndex, 2);
		lowPass.Set("cutoff_hz", 3000f);
	}

	private void StopWallRun(Node node)
	{
		Timing.RunCoroutine(StopWallRunSound(node).CancelWith(this), "Wallrun");
	}

	private IEnumerator<double> StopWallRunSound(Node node)
	{
		float timeElapsed = 0f;
		float duration = 0.3f;

		AudioEffect lowPass = AudioServer.GetBusEffect(_movementBusIndex, 2);
		float initialLowpass = (float)lowPass.Get("cutoff_hz");

		do
		{
			timeElapsed += (float)node.GetProcessDeltaTime();
			float normalizedTime = timeElapsed / duration;

			_wallrun.PitchScale = Mathf.Lerp(1f, 0.1f, normalizedTime);
			lowPass.Set("cutoff_hz", Mathf.Lerp(initialLowpass, 20500f, normalizedTime));

			yield return Timing.WaitForOneFrame;
		}
		while (timeElapsed < duration);

		_wallrun.Stop();
	}
    #endregion
}
