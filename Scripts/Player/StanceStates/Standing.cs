using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class Standing : MovementState
{
    public override void Enter()
    {
        base.Enter();
        
        Camera.StartStance(0f);
        Movement.SetColliderHeight(2f);
    }

    public override void Exit()
    {
        base.Enter();

        Camera.StopStance();
    }

    public override void Update(double delta)
    {

    }

    public override void PhysicsUpdate(double delta)
    {
        if (Input.IsActionPressed("crouch") && Movement.FSM.CurrentState is not Air)
        {
            EmitSignal(SignalName.StateFinished, "Crouching", new());
        }
    }
}
