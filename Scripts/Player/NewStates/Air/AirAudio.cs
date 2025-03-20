using Godot;
using System;

public partial class AirAudio : Node3D
{
    private Movement _movement;
    [Export] public string movementBus;
	private int _movementBusIndex;

    [ExportSubgroup("Wind")]
	[Export] private AudioStreamPlayer3D _windRun;
	[Export] private float _minWindVolume = -80.0f;
	[Export] private float _maxWindVolume = -20.0f;
	[Export] private float _minWindPitch = 1.0f;
	[Export] private float _maxwindPitch = 1.2f;
	[Export] private float _maxPlayerVelocity = 15;
	[Export] private float _windLerpSpeed = 2.0f;
	[Export] private float _velocityExponent = 2.0f;

    public override void _Ready()
    {
        _movement = GetOwner<Movement>();
        
        _movementBusIndex = AudioServer.GetBusIndex(movementBus);
        _windRun.Play();
    }

    public override void _Process(double delta)
    {
        Vector3 playerVelocity = _movement.Velocity;

		float velocityMagnitude = playerVelocity.Length() / _maxPlayerVelocity;
		float velocityScale = Mathf.Pow(velocityMagnitude, _velocityExponent);

		float desiredVolume = Mathf.Lerp(_minWindVolume, _maxWindVolume, velocityScale);
		float desiredPitch = Mathf.Lerp(_minWindPitch, _maxwindPitch, velocityScale);

		desiredVolume = Mathf.Clamp(desiredVolume, _minWindVolume, _maxWindVolume);
		desiredPitch = Mathf.Clamp(desiredPitch, _minWindPitch, _maxwindPitch);

		_windRun.VolumeDb = Mathf.Lerp(_windRun.VolumeDb, desiredVolume, _windLerpSpeed * (float)delta);
		_windRun.PitchScale = Mathf.Lerp(_windRun.PitchScale, desiredPitch, _windLerpSpeed * (float)delta);
    }
}
