using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class CrouchMove : MovementState
{
    [Export] private float _crouchingSpeed;
    [Export] private float _crouchAccelerationTime = 2f;
    [Export(PropertyHint.Range, "0.01, 10,")] private float _crouchDirChangeTime = 0.3f;
    [Export(PropertyHint.Range, "0, 1,")] private float _crouchDirectionControl = 0.15f;
    private float _crouchSpeedChange; // Value that changes based on the input direction
    private float _crouchAccChange;

    // Value that determines how much speed to reduce when moving to other directions except forward
    [Export(PropertyHint.Range, "0.5, 1,")] private float _crouchChangeFactor = 0.65f; 

    [Export] private float _headBobSpeed = 22.0f;
    [Export] private float _headBobIntensity = 0.2f; //in centimetres

    // Camera Shake
    private CamShakeInstance _crouchShake;

    private float _crouchAirTime = 0.0f;

    public override void Awake()
    {
        base.Enter();
    }

    public override void Enter()
    {
        base.Enter();
        
        _crouchSpeedChange = _crouchingSpeed;
        _crouchAccChange = _crouchAccelerationTime;

        Movement.SetDirectionChangeTime(_crouchDirChangeTime);
        Movement.SetDirectionControl(_crouchDirectionControl);

        _crouchShake = CamShake.ShakePreset(CamShakePresets.Sprinting);
        Movement.AnimationPlayer.Set("parameters/Master/conditions/moving", true);
    }

    public override void Exit()
    {
        CamShake.RemoveShake(_crouchShake);
        Movement.AnimationPlayer.Set("parameters/Master/conditions/moving", false);
    }

    public override void Update(double delta)
    {
        Camera.SetHeadBob(_headBobIntensity, _headBobSpeed);
        Camera.HeadBob();

        Camera.RotateBodyMeshInput();

        float maxVelocity = _crouchingSpeed * 3f;
        float normalizedSpeed = Mathf.Clamp(Movement.Velocity.Length() / maxVelocity, 0f, 1f);

        Movement.AnimationPlayer.Set("parameters/Master/Move/sprint_speed/scale", normalizedSpeed);
    }

    public override void PhysicsUpdate(double delta)
    {
        if (Movement.GetCurrentSpeed() < _crouchingSpeed)
        {
            Movement.Accelerate((float)delta, _crouchingSpeed, _crouchAccelerationTime);
        }
        else
        {
            Movement.Deccelerate((float)delta, _crouchingSpeed * 4f, _crouchAccelerationTime);
        }

        if (Movement.GetRawInputDirection() == Vector2.Zero)
            EmitSignal(SignalName.StateFinished, "Decceleration", new());

        if (!Movement.IsOnFloor())
        {
            _crouchAirTime += (float)GetPhysicsProcessDeltaTime();
        }

        if (_crouchAirTime >= 0.05f)
        {
            _crouchAirTime = 0.0f;
            EmitSignal(SignalName.StateFinished, "Air", new());
        }

        if (Movement.StanceFSM.CurrentState is Standing && Movement.GetRawInputDirection() != Vector2.Zero)
        {
            EmitSignal(SignalName.StateFinished, "Sprint", new());
        }

        if (Input.IsActionJustPressed("jump"))
        {
            EmitSignal(SignalName.StateFinished, "Jump", new());
        }
    }
}
