using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class Crouching : MovementState
{
    private RayCast3D _ceilingRay;

    // Camera Shake
    private CamShakeInstance _crouchShake;
    private bool _crouchMove;

    public override void Awake()
    { 
        _ceilingRay = GetNode<RayCast3D>("CeilingRay");
    }

    public override void Enter()
    {
        base.Enter();

        Movement.SetColliderHeight(1.0f);
        Camera.StartStance(-0.5f);

        _crouchShake = CamShake.ShakePreset(CamShakePresets.Idle);
    }

    public override void Exit()
    {
        CamShake.RemoveShake(_crouchShake);
        Camera.StopStance();
    }

    public override void PhysicsUpdate(double delta)
    {
        if (!Input.IsActionPressed("crouch") && !_ceilingRay.IsColliding() && Movement.FSM.CurrentState is not Sliding
            || Movement.FSM.CurrentState is Air)
        {
            EmitSignal(SignalName.StateFinished, "Standing", new());
        }
    }
}
