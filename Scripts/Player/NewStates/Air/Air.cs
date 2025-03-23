using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class Air : MovementState
{
    [Export] private float _airSpeed;
    [Export] private float _airAccelerationTime = 2.0f;
    [Export(PropertyHint.Range, "0, 1")] private float _airControl = 0.05f;
    [Export(PropertyHint.Range, "0, 1,")] private float _airChangeFactor = 0.65f; 

    [Export(PropertyHint.Range, "0.01, 10,")] private float _airDirChangeTime;
    [Export(PropertyHint.Range, "1, 4,")] private float _gravityMultiplier = 1f;

    private float _airTime = 0.0f;
    private float _coyoteTimer = 0.0f;
    [Export] private float _coyoteTime = 0.2f;

    // Camera Shake
    private CamShakeInstance _airShake;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    

    // Additional States
    private Wallrunning _wallrunningState;


    public override void Enter()
    {
        base.Enter();

        Movement.SetDirectionChangeTime(_airDirChangeTime);
        Movement.SetDirectionControl(_airControl);

        Movement.AnimationPlayer.Set("parameters/Master/conditions/air", true);
        Movement.AnimationPlayer.Set("parameters/Master/conditions/land", false);
        Movement.AnimationPlayer.Set("parameters/Master/conditions/jump", false);

        _airShake = CamShake.ShakePreset(CamShakePresets.InAir);
        _wallrunningState = Movement.GetNode<Wallrunning>("FSM/Wallrunning");
    }

    public override void Exit()
    {
        Movement.AnimationPlayer.Set("parameters/Master/conditions/air", false);
        Movement.AnimationPlayer.Set("parameters/Master/conditions/land", true);

        _wallrunningState.SetWallRunTimer(0.0f); // Reset wallrun timer;

        CamShake.RemoveShake(_airShake);
    }

    public override void Update(double delta)
    {
        _airTime += (float)GetProcessDeltaTime();
        _coyoteTimer += (float)GetProcessDeltaTime();

        if (Movement.GetRawInputDirection() == Vector2.Zero)
		{
			Camera.RotateBodyMeshDirection();
		}
		else
		{
			Camera.RotateBodyMeshInput();
		}

        // Handle landing
		if (Movement.IsOnFloor())
		{
            _airTime = 0.0f;
            _coyoteTimer = 0.0f;

            //CamShake.ShakePreset(CamShakePresets.Roll);

            if (Movement.GetRawInputDirection() != Vector2.Zero)
                EmitSignal(SignalName.StateFinished, "Sprint", new());
            else
                EmitSignal(SignalName.StateFinished, "Decceleration", new());
		}
        else
        {
            if (_coyoteTimer <= _coyoteTime)
            {
                if (Input.IsActionJustPressed("jump") && Movement.FSM.PreviousState is not Jump)
                {
                    EmitSignal(SignalName.StateFinished, "Jump", new());
                    _coyoteTimer = _coyoteTime + 1; // Prevents multiple jumps
                }
            }

            if (Wallrunning.CheckWallCollision(Movement, Camera, out KinematicCollision3D col, out Wallrunning.WallDirection direction)
                && _wallrunningState.GetWallRunTimer() < _wallrunningState.GetWallRunTime()
                && !Movement.SendRayInDirection(Movement.GetPlayerDirection(), Camera.GlobalPosition, 0.5f, out Vector3 normal, out Vector3 point)) // Check if player is wallrunning
            {
                EmitSignal(SignalName.StateFinished, "Wallrunning", new());
            }
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        float yVelocity = Movement.Velocity.Y;

        if (yVelocity > 0)
        {
            yVelocity -= Movement.gravity * (float)delta;
        }
        else
        {
            yVelocity -= Movement.gravity * _gravityMultiplier * (float)delta;
        }

        Movement.SetYVelocity(yVelocity);

        float airSpeedChange = Movement.IsPlayerMainlyForward(45) ? _airSpeed : _airSpeed * _airChangeFactor;

        if (Movement.GetRawInputDirection() != Vector2.Zero && Movement.GetCurrentSpeed() < airSpeedChange)
        {
            Movement.Accelerate((float)delta, airSpeedChange, _airAccelerationTime);
        }
        else
        {
            Movement.Deccelerate((float)delta, airSpeedChange, _airAccelerationTime);
        }

        float airControlChange = Movement.IsPlayerMainlyForward(45) ? _airControl : _airControl * _airChangeFactor;
        Movement.SetDirectionControl(airControlChange);
    }

    public float GetAirTime() => _airTime;

    public void PlayAnyAudioOnVelocity(NodePath player, AudioStream stream, float velocityThreshold)
	{
        if (Mathf.Abs(Movement.GetLastVelocity().Y) > velocityThreshold)
        {
            AudioStreamPlayer3D streamPlayer = (AudioStreamPlayer3D)GetNode(player);
		
            streamPlayer.Stream = stream;

            streamPlayer.PitchScale = _rng.RandfRange(0.9f, 1.1f);
            streamPlayer.Play();

            PlayLandScreenShake();
        }

        GD.Print(Movement.GetLastVelocity().Y);
	}

    private void PlayLandScreenShake() => CamShake.ShakePreset(CamShakePresets.Roll);
}
