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

        Movement.SetDirectionChangeTime(_sprintDirChangeTime);
        Movement.SetDirectionControl(_sprintDirectionControl);

        Movement.AnimationPlayer.Set("parameters/Master/conditions/moving", true);
        Movement.AnimationPlayer.Set("parameters/Master/Move/MoveSM/conditions/sprint", true);

        _sprintShake = CamShake.ShakePreset(CamShakePresets.Sprinting);
    }

    public override void Update(double delta)
    {
        Camera.SetHeadBob(_headBobIntensity, _headBobSpeed);
        Camera.HeadBob();

        Camera.RotateBodyMeshInput();

        float maxVelocity = _sprintingSpeed;
        float normalizedSpeed = Mathf.Clamp(Movement.Velocity.Length() / maxVelocity, 0f, 1f);

        Movement.AnimationPlayer.Set("parameters/Master/Move/MoveSM/sprint/sprint_speed/scale", normalizedSpeed);
    }

    public override void Exit()
    {
        Movement.AnimationPlayer.Set("parameters/Master/conditions/moving", false);
        Movement.AnimationPlayer.Set("parameters/Master/Move/MoveSM/conditions/sprint", false);
        
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

        if (Movement.StanceFSM.CurrentState is Crouching)
        {
            if (Movement.IsRunningUpSlope())
                EmitSignal(SignalName.StateFinished, "CrouchMove", new());
            else
            {
                float angleBetweenDirection = Camera.GetSignedYAngleBetween(Camera.GetNeckBasis().Z, Movement.GetPlayerDirection());

                // Check if the player is mainly moving forward and not on a slope (prevents from sliding backwards)
                if (Mathf.Abs(Mathf.RadToDeg(angleBetweenDirection)) > 100)
                {
                    EmitSignal(SignalName.StateFinished, "Sliding", new());
                }
            }
        }

        Movement.IsRunningUpSlope();
    }
}
