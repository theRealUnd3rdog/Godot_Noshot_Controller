using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class PlayerVerticalWallrun : PlayerMovementState
{
    public static Action VerticalWallRunStart;
    public static Action<Node> VerticalWallRunEnd;

    public override void Enter()
    {
        base.Enter();

        VerticalWallRunStart?.Invoke();

        float desiredVelocity = Mathf.Sqrt(2 * Movement.gravity * Movement.verticalRunHeight);
        float time = Movement.verticalRunHeight / desiredVelocity;

        float height = 1/2 * Movement.gravity * (float)Math.Pow(time, 2) + desiredVelocity * time;
        GD.Print(height);

        Movement.currentSpeed = 0f;

        Movement.Jump(desiredVelocity);
        Movement.Velocity = Movement.playerVelocity;

        Movement.camState = CameraState.Wallrunning;
    }

    public override void Exit()
    {
        VerticalWallRunEnd?.Invoke(this);
        Movement.camState = CameraState.Normal;

        Movement.currentSpeed = 3f;
        Movement.playerVelocity.Y = 0f;
    }

    public override void HandleInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion eventMouseMotion)
        {
            Movement.RotatePlayerByConstraintUp(eventMouseMotion.Relative.X, eventMouseMotion.Relative.Y, -15f, 15f, 75f, 20f);
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        if (!Movement.SendRayInDirection(-Movement.GlobalBasis.Z, 0.5f, out Vector3 normal, out Vector3 point))
        {
            EmitSignal(SignalName.StateFinished, "PlayerAir", new());
        }

        // Handle landing
		if (Movement.IsOnFloor() || Movement.playerVelocity.Y < 0f)
		{
            EmitSignal(SignalName.StateFinished, "PlayerAir", new());
		}

        if (Movement.CheckVault(delta, out Vector3 vaultPoint))
        {
            EmitSignal(SignalName.StateFinished, "PlayerVault", new());
        }

        
    }
}
