using Godot;
using Godot.Collections;
using MEC;
using System;
using System.Diagnostics;

public enum CameraState
{
	Normal,
	Freelooking,
}

public partial class PlayerMovement : CharacterBody3D
{
	public GodotParadiseFiniteStateMachine FSM;
	private Node3D _head;
	private Node3D _neck;
	private Node3D _eyes;
	private Node3D _mesh;

	private Vector3 _lastPhysicsPos;
	[Export] private Node3D _resetPosition;
	[Export] private CollisionShape3D _standingCollider;
	[Export] private AnimationTree _animator;
	[Export] private bool _physicsInterpolate = true;

	public CameraState camState;

	[ExportCategory("Movement")]
	[Export] public float walkingSpeed {private set; get;} = 5.0f;
	[Export] public float sprintingSpeed {private set; get;} = 8.0f;
	
	[Export] private float _jumpVelocity = 4.5f;
	[Export] public float lerpSpeed {private set; get;} = 10.0f; // Gradually changes a value. (Adding smoothing to values)
	[Export] private float _airLerpSpeed = 3.0f;

	// private movements
	public float currentSpeed = 5.0f;
	public float momentum {set; get;} = 0.0f;
	public float airTime = 0.0f;
	public Vector3 direction = Vector3.Zero;
	public Vector2 inputDirection = Vector2.Zero;
	public Vector3 lastVelocity = Vector3.Zero;

	// private rotations
	private float _rotationX = 0f;
	private float _rotationZ = 0f;

	[ExportSubgroup("Z Tilt")]
	[Export] private float _zRotationLerp = 7f;
	[Export] private float _zClamp = 5f;

	// Events
	public static event Action<Vector3> VelocityChange; // Event that constantly fires when velocity changes


	[ExportSubgroup("Crouching")]
	[Export] private CollisionShape3D _crouchingCollider;
	[Export] private RayCast3D _ceilingRay;
	[Export] public float crouchingSpeed {private set; get;} = 3.0f;
	[Export(PropertyHint.Range, "0.25f, 0.75f")] private float _crouchingDepth = 0.5f;
	private float _initialDepth;


	[ExportSubgroup("Sliding")]
	[Export] public float slideTimerMax {private set; get;} = 1.0f;
	[Export] public float slideSpeed  {private set; get;}= 10.0f;
	public float slideTimer = 0.0f;
	public Vector2 slideVector = Vector2.Zero;
	public Basis slideBasis;
	public float initialRotationY;


	[ExportSubgroup("Head Bobbing")]
	[Export] private float _headBobSprintSpeed = 22.0f;
	[Export] private float _headBobWalkingSpeed = 14.0f;
	[Export] private float _headBobCrouchSpeed = 10.0f;

	[Export] private float _headBobSprintIntensity = 0.2f; //in centimetres
	[Export] private float _headBobWalkIntensity = 0.1f;
	[Export] private float _headBobCrouchIntensity = 0.05f;

	private Vector2 _headBobVector = Vector2.Zero; // Keep track of side to side and up and down of bob
	private float _headBobIndex = 0.0f; // Keep track of our head bob index along the sin wave
	private float _headBobCurrentIntensity = 0.0f;
	

	[ExportSubgroup("Sensitivity")]
	[Export(PropertyHint.Range, "0, 1,")] public float mouseSensitivityX = 0.4f;
	[Export(PropertyHint.Range, "0, 1,")] public float mouseSensitivityY = 0.4f;


	[ExportSubgroup("Free Looking")]
	[Export] private float _freeLookTilt = 0.3f;

	[ExportSubgroup("Field of View")]
	[Export] private Camera3D _camera;
	[Export] private float _maxFov = 98f;
	private float _minFov = 0f;
	[Export] private float _velocityExponent = 2.0f;
	[Export] private float _maxPlayerVelocity = 15f;
	[Export] private float _fovLerpSpeed = 2.0f;


	[ExportSubgroup("Interface")]
	[Export] private Label _speedLabel;
	[Export] private Label _momentumLabel;
	[Export] private Label _stateLabel;
	[Export] private Label _animationLabel;
	[Export] private Label _desiredSpeedLabel;
	[Export] private Label _previousStateLabel;

	// inputs
	public bool sprintAction {private set; get;}
	private bool _previousSprintAction;


	// Get the gravity from the project settings to be synced with RigidBody nodes.
	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _Ready()
    {
		FSM = GetNode<GodotParadiseFiniteStateMachine>("FSM");

		_head = GetNode<Node3D>("Mesh/Neck/Head");
		_eyes = GetNode<Node3D>("Mesh/Neck/Head/Eyes");
		_neck = GetNode<Node3D>("Mesh/Neck");
		_mesh = GetNode<Node3D>("Mesh");

		_lastPhysicsPos = GlobalTransform.Origin;

		_initialDepth = _head.Position.Y;

		_minFov = _camera.Fov;

		// Make the mouse confined and within the center of the screen
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent @event)
    {
		// Mouse movement on camera
		if (@event is InputEventMouseMotion eventMouseMotion)
		{
			if (camState == CameraState.Freelooking)
			{
				if (FSM.CurrentState is PlayerSlide)
				{
					RotatePlayer(eventMouseMotion.Relative.X, eventMouseMotion.Relative.Y);
				}
				else
				{
					FreeLookRotation(eventMouseMotion.Relative.X);
				}
			}
			// Rotate player like normal
			else
			{
				RotatePlayer(eventMouseMotion.Relative.X, eventMouseMotion.Relative.Y);
			}
		}
    }

    public override void _Process(double delta)
    {
		HandleZRotation((float)delta);

		if (Input.IsKeyPressed(Key.R) && _resetPosition != null)
			GlobalPosition = _resetPosition.GlobalPosition;

		if (_physicsInterpolate)
		{
			double fraction = Engine.GetPhysicsInterpolationFraction();
		
			Transform3D modifiedTransform = _mesh.GlobalTransform;
			modifiedTransform.Origin = _lastPhysicsPos.Lerp(GlobalTransform.Origin, (float)fraction);

			_mesh.GlobalTransform = modifiedTransform;
		}
        
		if (_camera != null)
		{
			Vector3 playerVelocity = Velocity;

			float velocityMagnitude = playerVelocity.Length() / _maxPlayerVelocity;
			float velocityScale = Mathf.Pow(velocityMagnitude, _velocityExponent);

			float desiredFOV = Mathf.Lerp(_minFov, _maxFov, velocityScale);

			desiredFOV = Mathf.Clamp(desiredFOV, _minFov, _maxFov);

			_camera.Fov = Mathf.Lerp(_camera.Fov, desiredFOV, _fovLerpSpeed * (float)delta);
		}

		if (_speedLabel == null)
			return;

		_speedLabel.Text = $"VELOCITY: {Mathf.Round(Velocity.Length())}";
		_momentumLabel.Text = $"MOMENTUM: {Mathf.Round(momentum)}";
		_stateLabel.Text = $"STATE: {FSM.CurrentState.Name}";
		_desiredSpeedLabel.Text = $"DESIRED SPEED: {Mathf.Round(currentSpeed)}";
		_previousStateLabel.Text = $"PREVIOUS STATE: {FSM.PreviousState.Name}";
    }

	private void RotatePlayer(float mouseX, float mouseY)
	{
		RotateY(Mathf.DegToRad(-mouseX * mouseSensitivityX));

		_rotationX += Mathf.DegToRad(-mouseY * mouseSensitivityY);
		_rotationX = Mathf.Clamp(_rotationX, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));

		_rotationZ += Mathf.DegToRad(mouseX * mouseSensitivityX * inputDirection.Length());
		_rotationZ = Mathf.Clamp(_rotationZ, Mathf.DegToRad(-_zClamp), Mathf.DegToRad(_zClamp));
	}

	private void FreeLookRotation(float mouseX)
	{
		_neck.RotateY(Mathf.DegToRad(-mouseX * mouseSensitivityX));

		float neckClampedRotation = Mathf.Clamp(_neck.Rotation.Y, Mathf.DegToRad(-120f), Mathf.DegToRad(120));
		Vector3 neckRotation = new Vector3(_neck.Rotation.X, neckClampedRotation, _neck.Rotation.Z);
		_neck.Rotation = neckRotation;
	}

	private void HandleZRotation(float delta)
	{
		_rotationZ = Mathf.Lerp(_rotationZ, 0f, delta * _zRotationLerp);

		Transform3D transform = _head.Transform;
		transform.Basis = Basis.Identity;
		_head.Transform = transform;

		_head.RotateObjectLocal(Vector3.Right, _rotationX);
		_head.RotateObjectLocal(Vector3.Forward, _rotationZ);
	}

	private void HandleAnimation()
	{
		if (_animator != null)
		{
			_animator.Set("parameters/moveState/conditions/idle", FSM.CurrentState is PlayerIdle || inputDirection.Length() <= 0.1f);
			_animator.Set("parameters/moveState/conditions/moving", IsOnFloor() && (FSM.CurrentState is PlayerWalk 
						|| FSM.CurrentState is PlayerSprint || FSM.CurrentState is PlayerCrouch) && Velocity.Length() > 0.1f);
			
			_animator.Set("parameters/moveState/conditions/jump", Input.IsActionJustPressed("jump") && IsOnFloor() && !_ceilingRay.IsColliding());
			_animator.Set("parameters/moveState/conditions/inAir", FSM.CurrentState is PlayerAir);

			if (_animationLabel != null)
			{
				AnimationNodeStateMachinePlayback node = (AnimationNodeStateMachinePlayback)_animator.Get("parameters/moveState/playback");
				_animationLabel.Text = "ANIMATION: " + node.GetCurrentNode();
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector3 depth;
		Vector2 inputDir = Input.GetVector("left", "right", "forward", "backward");
		inputDirection = inputDir;

		VelocityChange?.Invoke(Velocity);

		HandleAnimation();

		// Crouching
		if ((FSM.CurrentState is PlayerCrouch || FSM.CurrentState is PlayerSlide) && IsOnFloor())
		{
			_standingCollider.Disabled = true;
			_crouchingCollider.Disabled = false;

			depth = new Vector3(_head.Position.X, _initialDepth - _crouchingDepth, _head.Position.Z);
			_head.Position = _head.Position.Lerp(depth, 1.0f - Mathf.Pow(0.5f, (float)delta * lerpSpeed));
		}

		// Standing
		else if (!_ceilingRay.IsColliding())
		{
			_standingCollider.Disabled = false;
			_crouchingCollider.Disabled = true;

			depth = new Vector3(_head.Position.X, _initialDepth, _head.Position.Z);
			_head.Position = _head.Position.Lerp(depth, 1.0f - Mathf.Pow(0.5f, (float)delta * lerpSpeed));

			// reduce momentum here and place momentum on top of sprintingSpeed
			if (FSM.CurrentState is not PlayerSlide && momentum >= 0)
				momentum -= (float)delta * (slideSpeed);

			sprintAction = _previousSprintAction;

			if (IsOnFloor())
				sprintAction = Input.IsActionPressed("sprint");
		}

		// Handle free looking
		if (Input.IsActionPressed("free_look") || FSM.CurrentState is PlayerSlide)
		{
			camState = CameraState.Freelooking;

			// Slide Tilt
			if (FSM.CurrentState is PlayerSlide)
			{
				Vector3 eyeRotation = _eyes.Rotation;
				eyeRotation.Z = -Mathf.DegToRad(7f);
				_eyes.Rotation = _eyes.Rotation.Lerp(eyeRotation, 1.0f - Mathf.Pow(0.5f, (float)delta * lerpSpeed));
			}
			else
			{
				Vector3 eyeRotation = _eyes.Rotation;
				eyeRotation.Z = -Mathf.DegToRad(_neck.Rotation.Y * _freeLookTilt);
				_eyes.Rotation = eyeRotation;
			}
		}
		// If not free looking return to normal camera state
		else
		{
			camState = CameraState.Normal;

			Vector3 neckRot = new Vector3(_neck.Rotation.X, 0f, _neck.Rotation.Z);
			_neck.Rotation = _neck.Rotation.Lerp(neckRot, 1.0f - Mathf.Pow(0.5f, (float)delta * lerpSpeed));

			Vector3 eyeRot = new Vector3(_eyes.Rotation.X, _eyes.Rotation.Y, 0f);
			_eyes.Rotation = _eyes.Rotation.Lerp(eyeRot, 1.0f - Mathf.Pow(0.5f, (float)delta * lerpSpeed));
		}

		// Handle head bob
		switch (FSM.CurrentState)
		{
			case PlayerWalk:
				_headBobCurrentIntensity = _headBobWalkIntensity;
				_headBobIndex += _headBobWalkingSpeed * (float)delta;
				break;
			
			case PlayerSprint:
				_headBobCurrentIntensity = _headBobSprintIntensity;
				_headBobIndex += _headBobSprintSpeed * (float)delta;
				break;
			
			case PlayerCrouch:
				_headBobCurrentIntensity = _headBobCrouchIntensity;
				_headBobIndex += _headBobCrouchSpeed* (float)delta;
				break;

			default:
				break;
		}

		if (IsOnFloor() && FSM.CurrentState is not PlayerSlide && inputDir != Vector2.Zero)
		{
			Vector2 headBob;

			headBob.Y = Mathf.Sin(_headBobIndex);
			headBob.X = Mathf.Sin(_headBobIndex / 2) + 0.5f;

			_headBobVector = headBob;
			
			Vector3 eyes = _eyes.Position;
			
			eyes.Y = Mathf.Lerp(eyes.Y, _headBobVector.Y * (_headBobCurrentIntensity / 2.0f), 1.0f - Mathf.Pow(0.5f, (float)delta * lerpSpeed));
			eyes.X = Mathf.Lerp(eyes.X, _headBobVector.X * _headBobCurrentIntensity, 1.0f - Mathf.Pow(0.5f, (float)delta * lerpSpeed));

			_eyes.Position = eyes;
		}
		else
		{
			Vector3 eyes = _eyes.Position;
			eyes.Y = Mathf.Lerp(eyes.Y, 0.0f, 1.0f - Mathf.Pow(0.5f, (float)delta * lerpSpeed));
			eyes.X = Mathf.Lerp(eyes.X, 0.0f, 1.0f - Mathf.Pow(0.5f, (float)delta * lerpSpeed));

			_eyes.Position = eyes;
		}

		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
			velocity.Y -= gravity * (float)delta;

		// Handle Jump.
		if (Input.IsActionJustPressed("jump") && IsOnFloor() && !_ceilingRay.IsColliding())
		{
			velocity.Y = _jumpVelocity;
		}

		// Get the input direction and handle the movement/deceleration.
		// As good practice, you should replace UI actions with custom gameplay actions.

		if (IsOnFloor())
		{
			direction = direction.Lerp((Transform.Basis * new Vector3(inputDir.X, 0f, inputDir.Y)).Normalized(), 
				1.0f - Mathf.Pow(0.5f, (float)delta * lerpSpeed));
		}
		else
		{
			// Air control

			if (inputDir != Vector2.Zero)
			{
				direction = direction.Lerp((Transform.Basis * new Vector3(inputDir.X, 0f, inputDir.Y)).Normalized(), 
					1.0f - Mathf.Pow(0.5f, (float)delta * _airLerpSpeed));
			}
		}

		if (direction != Vector3.Zero)
		{
			velocity.X = direction.X * currentSpeed;
			velocity.Z = direction.Z * currentSpeed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, currentSpeed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, currentSpeed);
		}

		Velocity = velocity;

		_lastPhysicsPos = GlobalTransform.Origin;
		lastVelocity = velocity;
		
		_previousSprintAction = sprintAction;
		MoveAndSlide();
	}
}
