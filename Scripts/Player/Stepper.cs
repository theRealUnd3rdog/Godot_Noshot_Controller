using Godot;
using System;

public partial class Stepper : Node3D
{
    [Export] private float _maxStepUp = 0.5f; // Maximum height in meters the player can step up.
    [Export] private float _maxStepDown = -0.5f; // Maximum height in meters the player can step down.
    [Export] private CollisionShape3D _collisionShape; // The collision shape of the player.
    [Export] private Movement _movement;

    [Export] private bool _debugStairDown;
    [Export] private bool _debugStairUp;

    private RayCast3D _collisionRay1;
    private RayCast3D _collisionRay2;
    private RayCast3D _collisionRay3;
    private RayCast3D _collisionRay4;
    private RayCast3D _collisionRay5;
    private RayCast3D[] _collisionRays;

    private ShapeCast3D _playerBottom;

    private float _colrayDistance = 0.5f;
    private float _playerHeight;

    private bool _wasGrounded;
    private bool _isGrounded;

    private Vector3 _vertical = new Vector3(0, 1, 0);
    private Vector3 _horizontal = new Vector3(1, 0, 1);

    public override void _Ready()
    {
        _collisionRay1 = GetNode<RayCast3D>("CollisionRays/CollisionRay1");
        _collisionRay2 = GetNode<RayCast3D>("CollisionRays/CollisionRay2");
        _collisionRay3 = GetNode<RayCast3D>("CollisionRays/CollisionRay3");
        _collisionRay4 = GetNode<RayCast3D>("CollisionRays/CollisionRay4");
        _collisionRay5 = GetNode<RayCast3D>("CollisionRays/CollisionRay5");

        _collisionRays = new RayCast3D[] { _collisionRay1, _collisionRay2, _collisionRay3, _collisionRay4, _collisionRay5 };

        CapsuleShape3D capsule = (CapsuleShape3D)_collisionShape.Shape;
        _playerHeight = capsule.Height;

        _playerBottom = GetNode<ShapeCast3D>("PlayerBottom");

        AdjustCollisionRays();
    }

    public override void _PhysicsProcess(double delta)
    {
        _wasGrounded = _isGrounded;
        _isGrounded = _movement.IsOnFloor();
    }

    private void AdjustCollisionRays()
    {
        for (int i = 0; i < _collisionRays.Length; i++)
        {
            Vector3 colTargetPos = _collisionRays[i].TargetPosition;
            Vector3 colPos = _collisionRays[i].Position;

            colTargetPos.Y = -_maxStepUp;
            colPos.Y = _maxStepUp;

            _collisionRays[i].TargetPosition = colTargetPos;
            _collisionRays[i].Position = colPos;
        }
    }

    public void StairStepDown()
    {
        if (_movement.IsOnFloor())
            return;

        if (_movement.Velocity.Y <= 0 && _wasGrounded)
        {
            DebugStairStepDown("SSD_ENTER", String.Empty);

            var bodyTestResult = new PhysicsTestMotionResult3D();
            var bodyTestParams = new PhysicsTestMotionParameters3D();

            bodyTestParams.From = _movement.GlobalTransform;
            bodyTestParams.Motion = new Vector3(0, _maxStepDown, 0);

            if (PhysicsServer3D.BodyTestMotion(_movement.GetRid(), bodyTestParams, bodyTestResult))
            {
                Vector3 movementPos = _movement.Position;
                movementPos.Y += bodyTestResult.GetTravel().Y;

                _movement.ApplyFloorSnap();
                _isGrounded = true;

                _movement.Position = movementPos;

                DebugStairStepDown("SSD_APPLIED", bodyTestResult.GetTravel().Y.ToString());
            }
        }
    }

    public void StairStepUp(double delta)
    {
        if (_movement.GetRawInputDirection() == Vector2.Zero)
            return;

        DebugStairStepUp("SSU_ENTER", String.Empty);

        var bodyTestParams = new PhysicsTestMotionParameters3D();
        var bodyTestResult = new PhysicsTestMotionResult3D();

        var distance = (_movement.Velocity * _horizontal) * (float)delta; // Store horizontal per frame
        bodyTestParams.From = _movement.GlobalTransform; // Movement as origin point
        bodyTestParams.Motion = distance; // Go forward by current distance

        // Pre-check: Are we colliding?
        if (!PhysicsServer3D.BodyTestMotion(_movement.GetRid(), bodyTestParams, bodyTestResult))
        {
            DebugStairStepUp("SSU_EXIT", String.Empty);

            // If we don't collide, return
            return;
        }

        // Start step checking
        var stepCollisions = 0;
        var stepHeight = 0.0f;
        var playerCoordy = _playerBottom.GlobalPosition.Y;
        DebugStairStepUp("SSU_PLAYER", playerCoordy.ToString());

        for (int i = 0; i < _collisionRays.Length; i++)
        {
            var colrayPos = _movement.GetPlayerDirection() * _colrayDistance + distance;

            switch (i)
            {
                case 1:
                    colrayPos = colrayPos.Rotated(Vector3.Up, Mathf.DegToRad(-30f));
                    break;
                
                case 2:
                    colrayPos = colrayPos.Rotated(Vector3.Up, Mathf.DegToRad(30f));
                    break;
                
                case 3:
                    colrayPos = colrayPos.Rotated(Vector3.Up, Mathf.DegToRad(-60f));
                    break;

                case 4:
                    colrayPos = colrayPos.Rotated(Vector3.Up, Mathf.DegToRad(60f));
                    break;
            }

            Vector3 collisionRayPoses = _collisionRays[i].GlobalPosition;

            collisionRayPoses.X = _movement.GlobalPosition.X + colrayPos.X;
            collisionRayPoses.Z = _movement.GlobalPosition.Z + colrayPos.Z;

            _collisionRays[i].GlobalPosition = collisionRayPoses;

            if (_collisionRays[i].IsColliding())
            {
                // If a collision ray collides, we check for step height
                var collisionCoordy = _collisionRays[i].GetCollisionPoint().Y;
                var difference = Mathf.Abs(collisionCoordy - playerCoordy);
                DebugStairStepUp("SSU_COL_COORDS", collisionCoordy.ToString());

                // Also check for slope
                var collisionNormal = _collisionRays[i].GetCollisionNormal().Y;
                DebugStairStepUp("SSU_COL_NORMAL", collisionNormal.ToString());

                // If 1: The step difference is within the margin
			    // And 2: Slope is walkable (Based on 45° [0.707], must manually change here)
                if ((0.0 <= difference && difference <= (_maxStepUp + 0.05)) && (0.707f <= collisionNormal)
                    && difference > 0.05f)
                {
                    if (Mathf.Abs(difference) > Mathf.Abs(stepHeight))
                    {
                        stepHeight = difference;
                        stepCollisions++;

                        DebugStairStepUp("SSU_NEW_HEIGHT", difference.ToString());
                    }
                }
            }
        }

        // Ensure we aren't colliding with a ceiling when applying height
        bodyTestParams.From = _movement.GlobalTransform;
        bodyTestParams.Motion = stepHeight * _vertical;

        if (PhysicsServer3D.BodyTestMotion(_movement.GetRid(), bodyTestParams, bodyTestResult))
        {
           // Make sure it's a ceiling collision
           for (int i = 0; i < bodyTestResult.GetCollisionCount(); i++)
           {
                if (bodyTestResult.GetCollisionNormal(i).Y <= -0.9f)
                {
                    DebugStairStepUp("SSU_CEILING_COLLISION", String.Empty);
                    return;
                }

           }
        }

        // Finally push player up by highest step if found
        // or exit if we didn't hit a step
        if (stepCollisions != 0)
        {
            Vector3 movementPos = _movement.Position;
            movementPos.Y += stepHeight;
            _movement.Position = movementPos;

            DebugStairStepUp("SSU_APPLIED", stepHeight.ToString());
        }
        else
        {
            DebugStairStepUp("SSU_EXIT", String.Empty);
        }
    }

    private void DebugStairStepDown(string param, string value)
    {
        if (!_debugStairDown)
            return;
        
        switch (param)
        {
            case "SSD_ENTER":
                GD.Print("");
                GD.Print("Stair step down entered");
                break;
            
            case "SSD_APPLIED":
                GD.Print("Stair step down applied, travel = ", value);
                break;
        }
    }

    private void DebugStairStepUp(string param, string value)
    {
        if (!_debugStairUp)
            return;

        switch (param)
        {
            case "SSU_ENTER":
                GD.Print("");
                GD.Print("SSU: Stair step up entered");
                break;

            case "SSU_EXIT":
                GD.Print("SSU: Exited with no collisions");
                break;

            case "SSU_PLAYER":
                GD.Print("SSU: Collision ahead, checking for step...");
                GD.Print("SSU: Player coordinates = ", value);
                break;

            case "SSU_COL_COORDS":
                GD.Print("SSU: Collision detected");
                GD.Print("SSU: Collision coordinates = ", value);
                break;
            
            case "SSU_COL_NORMAL":
                GD.Print("SSU: Collision normal = ", value);
                break;
            
            case "SSU_CEILING_COLLISION":
                GD.Print("SSU: Ceiling is blocking step-up");
                break;

            case "SSU_NEW_HEIGHT":
                GD.Print("SSU: New height saved = ", value);
                break;

            case "SSU_APPLIED":
                GD.Print("SSU: Applied new height = ", value);
                break;
        }
    }
}
