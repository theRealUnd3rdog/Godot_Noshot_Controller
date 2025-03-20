using Godot;
using System;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class Sliding : MovementState
{
    [Export] private float _slideTimerMax = 1.0f;
	[Export] private float _slideSpeed = 10.0f;

    [ExportSubgroup("Audio")]
    [Export] private AudioStreamPlayer3D _slideIn;
	[Export] private AudioStreamPlayer3D _slideLoop;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

	private float _slideTimer = 0.0f;
	private Vector3 _slideDirection;
    
    // Camera Shake
    private CamShakeInstance _slideShake;

    private float _slideAirTime;


    public override void Enter()
    {
        base.Enter();

        _slideDirection = Movement.GetPlayerDirection();
        _slideTimer = _slideTimerMax;

        _slideShake = CamShake.ShakePreset(CamShakePresets.Sliding);
        Movement.AnimationPlayer.Set("parameters/Master/conditions/moving", true);
        Movement.AnimationPlayer.Set("parameters/Master/Move/MoveSM/conditions/slide", true);

        PlaySlideSound();
    }

    public override void Exit()
    {
        _slideTimer = 0.0f;

        CamShake.RemoveShake(_slideShake);
        Movement.AnimationPlayer.Set("parameters/Master/conditions/moving", false);
        Movement.AnimationPlayer.Set("parameters/Master/Move/MoveSM/conditions/slide", false);

        StopSlideLoop();
    }

    public override void Update(double delta)
    {

    }

    public override void PhysicsUpdate(double delta)
    {
        Vector3 slideVec = _slideDirection;
        Vector3 floorDirection = slideVec.Slide(Movement.GetFloorNormal());

        Movement.SetPlayerDirection(floorDirection);
        Movement.SetCurrentSpeed((_slideTimer + 0.1f) * _slideSpeed);

        float floorAngle = Mathf.RadToDeg(Movement.GetFloorAngle());

        if (floorAngle > 8f && !Movement.IsRunningUpSlope())
        {
            // Running down slope, increase your speed

            if (_slideTimer < _slideTimerMax)
                _slideTimer += (float)delta;
        }
        else
        {
            _slideTimer -= (float)delta;
        }

        PlaySlideLoop(_slideTimer);

        // --- STATE HANDLING BELOW --- //
        if (!Movement.IsOnFloor())
        {
            _slideAirTime += (float)GetPhysicsProcessDeltaTime();
        }

        // If the player is in the air for a certain amount of time (prevents weird changes in state when snapping to different locations), switch to air state
        if (_slideAirTime >= 0.05f)
		{
            _slideAirTime = 0.0f;
			EmitSignal(SignalName.StateFinished, "Air", new());
		}

        if (Input.IsActionJustPressed("jump"))
        {
            EmitSignal(SignalName.StateFinished, "Jump", new());
        }

        if (_slideTimer <= 0f
            || Movement.IsOnWall())
        {
            EmitSignal(SignalName.StateFinished, "CrouchMove", new());
        }
    }

    private void PlaySlideSound()
	{
		_slideIn.PitchScale = _rng.RandfRange(0.9f, 1.1f);
		_slideIn.Play();
	}

	private void PlaySlideLoop(float timer)
	{
		// Start looping sound
		if (timer > 0.05f)
		{
			if (!_slideLoop.Playing)
				_slideLoop.Play();
			
			_slideLoop.PitchScale = 1f * Mathf.InverseLerp(0f, 1f, timer);
		}
	}

    private void StopSlideLoop() => _slideLoop.Stop();
}
