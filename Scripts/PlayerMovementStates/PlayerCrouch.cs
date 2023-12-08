using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class PlayerCrouch : PlayerMovementState
{
    public override void Enter()
    {
        base.Enter();
    }

    public override void PhysicsUpdate(double delta)
    {
        Movement.Crouch(delta);

        Movement.currentSpeed = Mathf.Lerp(Movement.currentSpeed, Movement.crouchingSpeed, 
                                    1.0f - Mathf.Pow(0.5f, (float)delta * Movement.lerpSpeed));

        if (!Input.IsActionPressed("crouch") && !Movement.ceilingRay.IsColliding())
        {
            EmitSignal(SignalName.StateFinished, "PlayerIdle", new());
        }

        if (!Movement.IsOnFloor() && Mathf.Abs(Movement.Velocity.Y) > 0.1f)
		{
			EmitSignal(SignalName.StateFinished, "PlayerAir", new());
		}
    }
}
