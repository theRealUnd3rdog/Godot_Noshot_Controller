using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class PlayerIdle : PlayerMovementState
{
    public override void Enter()
    {
        base.Enter();
    }

    public override void PhysicsUpdate(double delta)
    {
        if (Input.IsActionPressed("crouch"))
        {
            EmitSignal(SignalName.StateFinished, "PlayerCrouch", new());
        }

        if (Movement.inputDirection != Vector2.Zero)
            EmitSignal(SignalName.StateFinished, "PlayerWalk", new());

		if (!Movement.IsOnFloor() && Mathf.Abs(Movement.Velocity.Y) > 0.1f)
		{
			EmitSignal(SignalName.StateFinished, "PlayerAir", new());
		}
    }
}
