using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class PlayerAir : PlayerMovementState
{
    public static event Action<Vector3> LastVelocityChangeLanded; // Event that keeps track of the last velocity when landed

    public override void Enter()
    {
        base.Enter();
    }

    public override void Exit()
    {
        LastVelocityChangeLanded?.Invoke(Movement.lastVelocity);
    }

    public override void PhysicsUpdate(double delta)
    {
        Movement.airTime += (float)delta;

        // Handle current speed when in air based on given speed and momentum

        if (Movement.FSM.PreviousState is PlayerSlide)
        {
            Movement.currentSpeed = Mathf.Lerp(Movement.currentSpeed, Movement.sprintingSpeed + (Movement.momentum / 2), 
                            1.0f - Mathf.Pow(0.5f, (float)delta * Movement.lerpSpeed));
        }

        // Handle landing
		if (Movement.IsOnFloor())
		{
			Movement.airTime = 0f;

            EmitSignal(SignalName.StateFinished, "PlayerIdle", new());
		}
    }
}
