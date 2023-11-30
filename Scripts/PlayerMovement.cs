using Godot;
using Godot.Collections;
using MEC;
using System;
using System.Diagnostics;

public enum MovementState
{
	Idle,
	Walking,
	InAir,
	Sprinting,
	Crouching,
	Sliding,
}

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

	// States
	public MovementState moveState;
	public MovementState movementState;
	public CameraState camState;

	[ExportCategory("Movement")]
	[Export] private float _walkingSpeed = 5.0f;
	[Export] private float _sprintingSpeed = 8.0f;
	
	[Export] private float _jumpVelocity = 4.5f;
	[Export] private float _lerpSpeed = 10.0f; // Gradually changes a value. (Adding smoothing to values)
	[Export] private float _airLerpSpeed = 3.0f;

	// private movements
	public float currentSpeed = 5.0f;
	private float _momentum = 0.0f;
	public float airTime = 0.0f;
	public Vector3 direction = Vector3.Zero;
	public Vector3 _lastVelocity = Vector3.Zero;
	public Vector2 inputDirection = Vector2.Zero;
	private Array<StringName> _groups = new Array<StringName>();

	// private rotations
	private float _rotationX = 0f;
	private float _rotationZ = 0f;

	[ExportSubgroup("Z Tilt")]
	[Export] private float _zRotationLerp = 7f;
	[Export] private float _zClamp = 5f;

	// Events
	public static event Action<Vector3> LastVelocityChangeLanded; // Event that keeps track of the last velocity when landed
	public static event Action SlideStartChange; // Event that fires whenever a slide has started
	public static event Action<float> SlideCurrentChange; // Event that constantly fires returning the timer
	public static event Action<Vector3> VelocityChange; // Event that constantly fires when velocity changes


	[ExportSubgroup("Crouching")]
	[Export] private CollisionShape3D _crouchingCollider;
	[Export] private RayCast3D _ceilingRay;
	[Export] private float _crouchingSpeed = 3.0f;
	[Export(PropertyHint.Range, "0.25f, 0.75f")] private float _crouchingDepth = 0.5f;
	private float _initialDepth;


	[ExportSubgroup("Sliding")]
	[Export] private float _slideTimerMax = 1.0f;
	[Export] private float _slideSpeed = 10.0f;
	public float slideTimer = 0.0f;
	private Vector2 _slideVector = Vector2.Zero;
	private Basis _slideBasis;
	private float _initialRotationY;


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

	// inputs
	private bool _sprintAction;
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
				if (moveState == MovementState.Sliding)
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
		_momentumLabel.Text = $"MOMENTUM: {Mathf.Round(_momentum)}";
		_stateLabel.Text = $"STATE: {moveState}";
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

	public override void _PhysicsProcess(double delta)
	{
		Vector3 depth;
		Vector2 inputDir = Input.GetVector("left", "right", "forward", "backward");
		inputDirection = inputDir;

		VelocityChange?.Invoke(Velocity);

		if (_animator != null)
		{
			_animator.Set("parameters/moveState/conditions/idle", moveState == MovementState.Idle || inputDirection.Length() <= 0.1f);
			_animator.Set("parameters/moveState/conditions/moving", IsOnFloor() && (moveState == MovementState.Walking 
						|| moveState == MovementState.Sprinting || moveState == MovementState.Crouching) && Velocity.Length() > 0.1f);
			
			_animator.Set("parameters/moveState/conditions/jump", Input.IsActionJustPressed("jump") && IsOnFloor() && !_ceilingRay.IsColliding());
			_animator.Set("parameters/moveState/conditions/inAir", moveState == MovementState.InAir);

			if (_animationLabel != null)
			{
				AnimationNodeStateMachinePlayback node = (AnimationNodeStateMachinePlayback)_animator.Get("parameters/moveState/playback");
				_animationLabel.Text = "ANIMATION: " + node.GetCurrentNode();
			}
		}

		// Crouching
		if ((Input.IsActionPressed("crouch") || moveState == MovementState.Sliding) && IsOnFloor())
		{
			_standingCollider.Disabled = true;
			_crouchingCollider.Disabled = false;

			currentSpeed = Mathf.Lerp(currentSpeed, _crouchingSpeed, 1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));
			depth = new Vector3(_head.Position.X, _initialDepth - _crouchingDepth, _head.Position.Z);
			_head.Position = _head.Position.Lerp(depth, 1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));

			// Sliding
			if ((moveState == MovementState.Sprinting || moveState == MovementState.InAir && _previousSprintAction) && inputDir != Vector2.Zero && IsOnFloor())
			{
				moveState = MovementState.Sliding;
				camState = CameraState.Freelooking;

				SlideStartChange?.Invoke();

				_slideVector = inputDir;
				_slideBasis = Transform.Basis;
				_initialRotationY = Rotation.Y;

				// Get slide momentum
				_momentum = Velocity.Length();

				slideTimer = _slideTimerMax;
			}
			else if (slideTimer <= 0f)
			{
				moveState = MovementState.Crouching;
			}
		}

		// Standing
		else if (!_ceilingRay.IsColliding())
		{
			_standingCollider.Disabled = false;
			_crouchingCollider.Disabled = true;

			depth = new Vector3(_head.Position.X, _initialDepth, _head.Position.Z);
			_head.Position = _head.Position.Lerp(depth, 1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));

			// reduce momentum here and place momentum on top of sprintingSpeed
			if (moveState != MovementState.Sliding && _momentum >= 0)
				_momentum -= (float)delta * (_slideSpeed);

			_sprintAction = _previousSprintAction;

			if (IsOnFloor())
				_sprintAction = Input.IsActionPressed("sprint");

			// Sprinting
			if (_sprintAction)
			{
				currentSpeed = Mathf.Lerp(currentSpeed, _sprintingSpeed + (_momentum / 2), 1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));

				if (IsOnFloor())
					moveState = MovementState.Sprinting;
			}
			//Walking
			else
			{
				currentSpeed = Mathf.Lerp(currentSpeed, _walkingSpeed, 1.0f - Mathf.Pow(0.5f, (float)delta *  _lerpSpeed));

				if (IsOnFloor())
				{
					if (inputDir != Vector2.Zero)
						moveState = MovementState.Walking;
					else
						moveState = MovementState.Idle;
				}
			}
		}

		// Handle free looking
		if (Input.IsActionPressed("free_look") || moveState == MovementState.Sliding)
		{
			camState = CameraState.Freelooking;

			// Slide Tilt
			if (moveState == MovementState.Sliding)
			{
				Vector3 eyeRotation = _eyes.Rotation;
				eyeRotation.Z = -Mathf.DegToRad(7f);
				_eyes.Rotation = _eyes.Rotation.Lerp(eyeRotation, 1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));
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
			_neck.Rotation = _neck.Rotation.Lerp(neckRot, 1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));

			Vector3 eyeRot = new Vector3(_eyes.Rotation.X, _eyes.Rotation.Y, 0f);
			_eyes.Rotation = _eyes.Rotation.Lerp(eyeRot, 1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));
		}

		// Handle slide timer
		if (moveState == MovementState.Sliding)
		{
			// Get the extra momentum from the slide
			slideTimer -= (float)delta;

			if (slideTimer <= 0f || !IsOnFloor())
			{
				slideTimer = 0f;

				camState = CameraState.Normal;
			}

			SlideCurrentChange?.Invoke(slideTimer);
		}

		// Handle head bob
		switch (moveState)
		{
			case MovementState.Walking:
				_headBobCurrentIntensity = _headBobWalkIntensity;
				_headBobIndex += _headBobWalkingSpeed * (float)delta;
				break;
			
			case MovementState.Sprinting:
				_headBobCurrentIntensity = _headBobSprintIntensity;
				_headBobIndex += _headBobSprintSpeed * (float)delta;
				break;
			
			case MovementState.Crouching:
				_headBobCurrentIntensity = _headBobCrouchIntensity;
				_headBobIndex += _headBobCrouchSpeed* (float)delta;
				break;

			default:
				break;
		}

		if (IsOnFloor() && moveState != MovementState.Sliding && inputDir != Vector2.Zero)
		{
			Vector2 headBob;

			headBob.Y = Mathf.Sin(_headBobIndex);
			headBob.X = Mathf.Sin(_headBobIndex / 2) + 0.5f;

			_headBobVector = headBob;
			
			Vector3 eyes = _eyes.Position;

			/* float targetY = _headBobVector.Y * (_headBobCurrentIntensity / 2.0f);

			// Define a threshold for proximity check
			float threshold = 0.01f; // Adjust this threshold as needed
			

			if (Mathf.Abs(eyes.Y) > targetY - threshold)
			{
    			// Trigger your action when eyes.Y is approximately equal to targetY
			} */
			
			eyes.Y = Mathf.Lerp(eyes.Y, _headBobVector.Y * (_headBobCurrentIntensity / 2.0f), 1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));
			eyes.X = Mathf.Lerp(eyes.X, _headBobVector.X * _headBobCurrentIntensity, 1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));

			_eyes.Position = eyes;
		}
		else
		{
			Vector3 eyes = _eyes.Position;
			eyes.Y = Mathf.Lerp(eyes.Y, 0.0f, 1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));
			eyes.X = Mathf.Lerp(eyes.X, 0.0f, 1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));

			_eyes.Position = eyes;
		}

		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
			velocity.Y -= gravity * (float)delta;

		// Change movement state to inAir and increase airtime
		if (!IsOnFloor() && Mathf.Abs(Velocity.Y) > 0.1f)
		{
			moveState = MovementState.InAir;
			airTime += (float)delta;
		}	

		// Handle Jump.
		if (Input.IsActionJustPressed("jump") && IsOnFloor() && !_ceilingRay.IsColliding())
		{
			velocity.Y = _jumpVelocity;

			// Reset slide timer
			slideTimer = 0f;

			if (_animator != null)
			{
				
				//_animator.Play("jump");
			}
		}

		// Handle landing
		if (IsOnFloor())
		{
			airTime = 0f;

			if (Mathf.Abs(_lastVelocity.Y) > 0.1f)
			{
				LastVelocityChangeLanded?.Invoke(_lastVelocity);
			}
		}

		// Get the input direction and handle the movement/deceleration.
		// As good practice, you should replace UI actions with custom gameplay actions.

		if (IsOnFloor())
		{
			direction = direction.Lerp((Transform.Basis * new Vector3(inputDir.X, 0f, inputDir.Y)).Normalized(), 
				1.0f - Mathf.Pow(0.5f, (float)delta * _lerpSpeed));
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


		if (moveState == MovementState.Sliding)
		{
			direction = (_slideBasis * new Vector3(_slideVector.X, 0f, _slideVector.Y)).Normalized();

			currentSpeed = (slideTimer + 0.1f) * _slideSpeed;
			currentSpeed = (slideTimer + 0.1f) * _slideSpeed;
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

		_lastVelocity = velocity;
		_previousSprintAction = _sprintAction;
		MoveAndSlide();
	}
}
