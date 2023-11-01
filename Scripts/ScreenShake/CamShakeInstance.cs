using Godot;
using System;
using System.Security.Claims;

public class CamShakeInstance
{
	private FastNoiseLite _noise = new FastNoiseLite();

	public float noiseSpeed; // Controls how fast it moves through the noise
	public float gain; // Controls the roughness of the shake
	public float frequency; // Controls the magnitude of the shake
	public float traumaAmount; // Controls the amount of trauma intensity
	public float traumaReduction; // Controls the reduction rate of the shake
	public float maxTrauma = 1f; // Maximum amount of trauma from looped shakes
	public float influence = 1f;
	public bool loop; // Do you want the shake to loop

	private float _time = 0.0f;
	private float _trauma = 0.0f;
	private Vector3 _shake;

	public CamShakeInstance(float frequency, float gain, float noiseSpeed, float traumaAmount, float traumaReduction)
	{
		this.frequency = frequency;

		this.traumaAmount = traumaAmount;
		this.traumaReduction = traumaReduction;

		this.gain = gain;
		this.noiseSpeed = noiseSpeed;

		InitializeNoise();

		_trauma = Mathf.Clamp(_trauma + traumaAmount, 0.0f, 1.0f);
	}

	public CamShakeInstance(float frequency, float gain, float noiseSpeed, float traumaAmount)
	{
		this.frequency = frequency;
		this.gain = gain;
		this.noiseSpeed = noiseSpeed;
		this.traumaAmount = traumaAmount;

		InitializeNoise();
	}

	public CamShakeInstance(CamShakeInstance instance)
	{
		this.frequency = instance.frequency;
		this.gain = instance.gain;

		this.noiseSpeed = instance.noiseSpeed;

		this.traumaAmount = instance.traumaAmount;
		this.traumaReduction = instance.traumaReduction;
		this.maxTrauma = instance.maxTrauma;

		InitializeNoise();
	}

	public void InitializeNoise()
	{
		_noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noise.Frequency = frequency;

		_noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noise.FractalGain = gain;
	}

	public Vector3 GetShake(float delta, Vector3 maxRot, Vector3 initialRotation, Vector3 currentRotDeg)
	{
		_time += delta;

		_shake = currentRotDeg;

		if (loop)
			_trauma = Mathf.Clamp(_trauma + traumaAmount * delta, 0.0f, maxTrauma);
		else
			_trauma = Mathf.Max(_trauma - delta * traumaReduction, 0.0f);

		_shake.X = initialRotation.X + maxRot.X * GetNoiseFromSeed(0) * GetShakeIntensity() * influence;
		_shake.Y = initialRotation.Y + maxRot.Y * GetNoiseFromSeed(1) * GetShakeIntensity() * influence;
		_shake.Z = initialRotation.Z + maxRot.Z * GetNoiseFromSeed(2) * GetShakeIntensity() * influence;

		return _shake;
	}

	public float GetShakeIntensity()
	{
		return _trauma * _trauma;
	}

	public float GetNoiseFromSeed(int seed)
	{
		_noise.Seed = seed;
		return _noise.GetNoise1D(_time * noiseSpeed);
	}

	public bool IsDone()
	{
		return _trauma <= 0f;
	}
}
