using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class Sprint : MovementState
{
    [Export] private float _sprintingSpeed;
    [Export] private float _sprintAccelerationTime = 2f;
    [Export(PropertyHint.Range, "0.01, 10,")] private float _sprintDirChangeTime = 0.3f;
    [Export(PropertyHint.Range, "0, 1,")] private float _sprintDirectionControl = 0.15f;
    private float _sprintSpeedChange; // Value that changes based on the input direction
    private float _sprintAccChange;

    // Value that determines how much speed to reduce when moving to other directions except forward
    [Export(PropertyHint.Range, "0.5, 1,")] private float _sprintChangeFactor = 0.65f; 

    [Export] private float _headBobSpeed = 22.0f;
    [Export] private float _headBobIntensity = 0.2f; //in centimetres

    // Camera Shake
    private CamShakeInstance _sprintShake;

    private float _sprintAirTime = 0.0f;

    public override void Enter()
    {
        base.Enter();

        _sprintSpeedChange = _sprintingSpeed;
        _sprintAccChange = _sprintAccelerationTime;

        Camera.StartStanding();
        Movement.SetDirectionChangeTime(_sprintDirChangeTime);
        Movement.SetDirectionControl(_sprintDirectionControl);

        Movement.AnimationPlayer.Set("parameters/Master/conditions/moving", true);

        _sprintShake = CamShake.ShakePreset(CamShakePresets.Sprinting);
    }

    public override void Update(double delta)
    {
        Camera.SetHeadBob(_headBobIntensity, _headBobSpeed);
        Camera.HeadBob();

        Camera.RotateBodyMeshInput();

        float maxVelocity = _sprintingSpeed;
        float normalizedSpeed = Mathf.Clamp(Movement.Velocity.Length() / maxVelocity, 0f, 1f);

        Movement.AnimationPlayer.Set("parameters/Master/Move/sprint_speed/scale", normalizedSpeed);
    }

    public override void Exit()
    {
        Movement.AnimationPlayer.Set("parameters/Master/conditions/moving", false);
        
        CamShake.RemoveShake(_sprintShake);
    }
    
    public override void PhysicsUpdate(double delta)
    {
        // Multiplication being the factor of how much you want to reduce the speed. 1 being full, 0 being nothing
        float _sprintSpeedChange = Movement.IsPlayerMainlyForward(45) ? _sprintingSpeed : _sprintingSpeed * _sprintChangeFactor;
        float _sprintAccChange = Movement.IsPlayerMainlyForward(45) ? _sprintAccelerationTime : _sprintAccelerationTime * _sprintChangeFactor;

        if (Movement.GetCurrentSpeed() < _sprintSpeedChange)
        {
            Movement.Accelerate((float)delta, _sprintSpeedChange, _sprintAccChange);
        }
        else
        {
            Movement.Deccelerate((float)delta, _sprintSpeedChange, _sprintAccChange);
        }

        /* if (Input.IsActionPressed("crouch") && !Movement.IsOnWall() && !Movement.IsRunningUpSlope() && !Movement.stepCast.IsColliding()
                && Movement.Velocity.Length() >= (Movement.sprintingSpeed - 1) && Movement.inputDirection.Y < 0f)
        {
            EmitSignal(SignalName.StateFinished, "PlayerSlide", new());
        } */

        /* if (Movement.IsOnFloor() && !Input.IsActionPressed("sprint"))
        {
            EmitSignal(SignalName.StateFinished, "Walk", new());
        } */

        // Threshold velocity before it reaches idle
        if (Movement.GetRawInputDirection() == Vector2.Zero)
            EmitSignal(SignalName.StateFinished, "Decceleration", new());

        if (!Movement.IsOnFloor())
        {
            _sprintAirTime += (float)GetPhysicsProcessDeltaTime();
        }

        // If the player is in the air for a certain amount of time (prevents weird changes in state when snapping to different locations), switch to air state
        if (_sprintAirTime >= 0.05f)
		{
            _sprintAirTime = 0.0f;
			EmitSignal(SignalName.StateFinished, "Air", new());
		}

        if (Input.IsActionJustPressed("jump"))
        {
            EmitSignal(SignalName.StateFinished, "Jump", new());
        }

        /* if (Movement.CheckVault(delta, out Vector3 vaultPoint) && Input.IsActionJustPressed("jump"))
        {
            EmitSignal(SignalName.StateFinished, "PlayerVault", new());
        } */

        /* if (Movement.CheckLadder())
        {
            EmitSignal(SignalName.StateFinished, "PlayerLadder", new());
        } */
    }
}
