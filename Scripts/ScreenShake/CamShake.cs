using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class CamShake : Area3D
{
	public static CamShake Instance;
	private static Dictionary<string, CamShake> _instanceList = new Dictionary<string, CamShake>();

	[Export] private float _maxX = 10.0f;
	[Export] private float _maxY = 10.0f;
	[Export] private float _maxZ = 5.0f;

	private Vector3 _maxRotShake;
	private Vector3 _rotAddShake;
	private List<CamShakeInstance> _cameraShakeInstances = new List<CamShakeInstance>();
	
	private Vector3 _initialRotation;


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Instance = this;
		_instanceList.Add(this.Name, this);

		_maxRotShake = new Vector3(_maxX, _maxY, _maxZ);
		_initialRotation = RotationDegrees;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_rotAddShake = Vector3.Zero;

		for (int i = 0; i < _cameraShakeInstances.Count; i++)
		{
			CamShakeInstance c = _cameraShakeInstances[i];

			_rotAddShake += c.GetShake((float)delta, _maxRotShake, _initialRotation, RotationDegrees);

			if (c.IsDone())
			{
				_cameraShakeInstances.RemoveAt(i);
				i--;
			}
		}

		RotationDegrees = _rotAddShake;

		// Test shake
		if (Input.IsActionJustPressed("screenShake"))
		{
			CamShake.Instance.ShakeOnce(0.1f, 0.5f, 50f, 1f, 0.5f);
		}
	}

	public static CamShake GetInstance(string name)
	{
		CamShake c;

		if (_instanceList.TryGetValue(name, out c))
			return c;

		GD.PrintErr($"CameraShake {name} not found!");

		return null;
	}

	public CamShakeInstance Shake(CamShakeInstance shake)
	{
		_cameraShakeInstances.Add(shake);

		return shake;
	}

	public CamShakeInstance ShakeOnce(float frequency, float gain, float noiseSpeed, float traumaAmount, float traumaReduction)
	{
		CamShakeInstance shake = new CamShakeInstance(frequency, gain, noiseSpeed, traumaAmount, traumaReduction);
		
		_cameraShakeInstances.Add(shake);

		return shake;
	}

	public CamShakeInstance StartShake(float frequency, float gain, float noiseSpeed, float traumaAmount, float maxTrauma)
	{
		CamShakeInstance shake = new CamShakeInstance(frequency, gain, noiseSpeed, traumaAmount);

		shake.loop = true;
		shake.maxTrauma = maxTrauma;
		_cameraShakeInstances.Add(shake);

		return shake;
	}

	public static CamShakeInstance ShakePreset(CamShakeInstance camShakeInstance)
	{
		Instance._cameraShakeInstances.Add(camShakeInstance);
		return camShakeInstance;
	}

	public override void _ExitTree()
    {
        _instanceList.Remove(Name);
    }
}
