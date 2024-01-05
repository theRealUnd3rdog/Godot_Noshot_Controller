using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using MEC;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class PlayerWallrun : PlayerMovementState
{
    public static Action WallRunStart;
    public static Action<Node> WallRunEnd;

    private float _frictionForce;

    private KinematicCollision3D _collision;
    public String wallDirection;
    
    public override void Enter()
    {
        base.Enter();

        WallRunStart?.Invoke();

        // Calculate friction force and give forward momentum
        _frictionForce = Movement.wallFrictionCoefficient * Movement.gravity; 

        Movement.momentum = Movement.Velocity.Length();
        //Movement.camState = CameraState.Freelooking;

        Movement.CheckWall(out _collision, out wallDirection);
    }

    public override void Exit()
    {
        WallRunEnd?.Invoke(Movement);
        //Movement.camState = CameraState.Normal;
    }

    public override void PhysicsUpdate(double delta)
    {
        Vector3 rayDirection = Movement.GlobalBasis.X;

        if (wallDirection == "Left")
            rayDirection = -Movement.GlobalBasis.X; 

        bool wall = Movement.SendRayInDirection(rayDirection, 0.5f, out Vector3 wallNormal, out Vector3 wallPoint);

        if (wall && Movement.wallRunTimer <= Movement.wallRunTime)
        {
            // Decrease wall run timer
            Movement.wallRunTimer += (float)delta;
            GD.Print(Movement.wallRunTimer);

            float normalizedTime = Movement.wallRunTimer / Movement.wallRunTime;

            //Movement.momentum += (float)delta * Movement.wallRunSpeed / 4;
            Movement.currentSpeed = Mathf.Lerp(Movement.currentSpeed, Movement.wallRunSpeed, normalizedTime);

            // Get forward vector of wall based on direction
            Vector3 rotatedVector = wallNormal.Rotated(Vector3.Up, Mathf.DegToRad(wallDirection == "Right" ? -90f : 90f));
            

            // Clamp rotation
            float angle = Movement.GlobalBasis.Z.SignedAngleTo(-rotatedVector, Vector3.Up);
            //Movement.RotateY(angle);

            //DebugDraw3D.DrawArrow(wallPoint, wallPoint + (rotatedVector * 1f), Colors.Aqua, 0.2f);
            //DebugDraw3D.DrawArrow(wallPoint + (rotatedVector * 1f), wallPoint + (wallNormal * 1f), Colors.IndianRed, 0.1f);

            // Decrease gravity with friction force over time
            Movement.playerVelocity.Y -= _frictionForce * (float)delta;

            // Increase gravity over time
            _frictionForce = Mathf.Lerp(_frictionForce, Movement.gravity, normalizedTime / 4);

            Movement.direction = Movement.direction.Lerp(rotatedVector.Normalized(), 
                1.0f - Mathf.Pow(0.5f, (float)delta * Movement.lerpSpeed));

            if (Input.IsActionJustPressed("jump"))
            {
		        Movement.WallJump(wallNormal);

                EmitSignal(SignalName.StateEntered, "PlayerAir", new());
            }
        }
        else
        {
            EmitSignal(SignalName.StateFinished, "PlayerAir", new());
        }
        
        // Handle landing
		if (Movement.IsOnFloor())
		{
            EmitSignal(SignalName.StateFinished, "PlayerAir", new());
		}

        if (Movement.CheckVault(delta, out Vector3 vaultPoint))
        {
            Movement.wallRunTimer = 0f; // Reset the timer
            EmitSignal(SignalName.StateFinished, "PlayerVault", new());
        }

        Movement.Velocity = Movement.playerVelocity;
    }
}
