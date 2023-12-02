using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class PlayerSlide : PlayerMovementState
{
    public static event Action SlideStartChange; // Event that fires whenever a slide has started
	public static event Action<float> SlideCurrentChange; // Event that constantly fires returning the timer

    public override void Enter()
    {
        base.Enter();

        SlideStartChange?.Invoke();

        // Get slide momentum
        Movement.momentum = Movement.Velocity.Length();

        Movement.camState = CameraState.Freelooking;

        Movement.slideVector = Movement.inputDirection;
        Movement.slideBasis = Movement.Transform.Basis;
        Movement.initialRotationY = Movement.Rotation.Y;

        Movement.slideTimer = Movement.slideTimerMax;
    }

    public override void Exit()
    {
        Movement.slideTimer = 0f;
        Movement.camState = CameraState.Normal;
    }

    public override void PhysicsUpdate(double delta)
    {
        // Handle slide timer
        // Get the extra momentum from the slide
        Movement.direction = (Movement.slideBasis * new Vector3(Movement.slideVector.X, 0f, Movement.slideVector.Y)).Normalized();
        Movement.currentSpeed = (Movement.slideTimer + 0.1f) * Movement.slideSpeed;

        Movement.slideTimer -= (float)delta;

        if (Movement.IsOnWall())
            EmitSignal(SignalName.StateFinished, "PlayerIdle", new());

        if (Movement.slideTimer <= 0f)
        {
            EmitSignal(SignalName.StateFinished, "PlayerWalk", new());
        }

        if (Input.IsActionJustReleased("crouch"))
        {
            EmitSignal(SignalName.StateFinished, "PlayerIdle", new());
        }
        
        if (!Movement.IsOnFloor() && Mathf.Abs(Movement.Velocity.Y) > 0.1f)
		{
			EmitSignal(SignalName.StateFinished, "PlayerAir", new());
		}

        SlideCurrentChange?.Invoke(Movement.slideTimer);
    }
}
