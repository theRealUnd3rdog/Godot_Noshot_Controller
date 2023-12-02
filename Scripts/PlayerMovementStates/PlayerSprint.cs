using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class PlayerSprint : PlayerMovementState
{
    public override void Enter()
    {
        base.Enter();
    }

    public override void PhysicsUpdate(double delta)
    {
        Movement.currentSpeed = Mathf.Lerp(Movement.currentSpeed, Movement.sprintingSpeed + (Movement.momentum / 2), 
                            1.0f - Mathf.Pow(0.5f, (float)delta * Movement.lerpSpeed));

        if (Input.IsActionPressed("crouch") && !Movement.IsOnWall())
        {
            EmitSignal(SignalName.StateFinished, "PlayerSlide", new());
        }

        if (!Movement.sprintAction)
        {
            EmitSignal(SignalName.StateFinished, "PlayerWalk", new());
        }

        if (!Movement.IsOnFloor() && Mathf.Abs(Movement.Velocity.Y) > 0.1f)
		{
			EmitSignal(SignalName.StateFinished, "PlayerAir", new());
		}
    }
}
