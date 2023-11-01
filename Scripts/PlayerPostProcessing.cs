using Godot;
using MEC;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class PlayerPostProcessing : Node3D
{
	// post processors
	[ExportCategory("Post processing")]
	[Export] private float _maxVelocity;
	[Export] private float _pingPongDuration;
	[Export] private float _pingPongSpeed;
	
	[ExportSubgroup("Vignette")]
	private Vignette _vignette;
	[Export(PropertyHint.Range, "0, 1,")] private float _minVgIntensity = 0.25f;
	[Export(PropertyHint.Range, "0, 1,")] private float _maxVgIntensity = 1f;
	[Export] private float _vgExponent;
	[Export] private float _vgSpeed;
	private float _initialVgIntensity;
	private float _initialVgSpeed;

	[ExportSubgroup("Lens Distortion")]
	private LensDistortion _lensDistortion;
	[Export(PropertyHint.Range, "0, 500")] private float _minLdZoom;
	[Export(PropertyHint.Range, "0, 500")] private float _maxLdZoom;
	[Export] private float _ldExponent;
	[Export] private float _ldSpeed;
	private float _initialLdZoom;
	private float _initialLdSpeed;

	[ExportSubgroup("Chromattic Abberation")]
	private ChromaticAbberation _chromaticAbberation;
	[Export(PropertyHint.Range, "0, 2")] private float _minCaStrength;
	[Export(PropertyHint.Range, "0, 2")] private float _maxCaStrength;
	[Export] private float _caExponent;
	[Export] private float _caSpeed;
	private float _initialCaStrength;
	private float _initialCaSpeed;

	[ExportSubgroup("Radial Blur")]
	private RadialBlur _radialBlur;
	[Export(PropertyHint.Range, "0, 0.026")] private float _minBlurPower;
	[Export(PropertyHint.Range, "0, 0.026")] private float _maxBlurPower;
	[Export] private float _blurExponent;
	[Export] private float _blurSpeed;

	// privates
	private Vector3 _playerVelocity;
	private float _deltaTime;


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		PlayerMovement.VelocityChange += GetPlayerVelocity;

		_vignette = PostProcessingManager.Instance.vignette;
		_lensDistortion = PostProcessingManager.Instance.lensDistortion;
		_chromaticAbberation = PostProcessingManager.Instance.chromaticAbberation;
		_radialBlur = PostProcessingManager.Instance.radialBlur;

		_initialVgIntensity = _minVgIntensity;
		_initialLdZoom = _minLdZoom;
		_initialCaStrength = _minCaStrength;

		_initialVgSpeed = _vgSpeed;
		_initialCaSpeed = _caSpeed;
		_initialLdSpeed = _ldSpeed;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_deltaTime = (float)delta;

		HandleVignette((float)delta);
		HandleLensDistortion((float)delta);
		HandleChromaticAbberation((float)delta);
		HandleRadialBlur((float)delta);

		if (Input.IsActionJustPressed("screenShake"))
		{
			GD.Print("Run ping pong");
			
			Timing.KillCoroutines("PongEffect");
			Timing.RunCoroutine(PingPongEventEffect().CancelWith(this), "PongEffect");
		}
	}

	private void HandleVignette(float delta)
	{
		float desiredVignette = GetVelocityBasedEffect(_maxVelocity, _vgExponent, _minVgIntensity, _maxVgIntensity);
		_vignette.vOpacity = Mathf.Lerp(_vignette.vOpacity, desiredVignette, delta * _vgSpeed);
	}

	private void HandleLensDistortion(float delta)
	{
		float desiredLd = GetVelocityBasedEffect(_maxVelocity, _ldExponent, _minLdZoom, _maxLdZoom);
		_lensDistortion.zoom = Mathf.Lerp(_lensDistortion.zoom, desiredLd, delta * _ldSpeed);
	}

	private void HandleChromaticAbberation(float delta)
	{
		float desiredCa = GetVelocityBasedEffect(_maxVelocity, _caExponent, _minCaStrength, _maxCaStrength);
		_chromaticAbberation.strength = Mathf.Lerp(_chromaticAbberation.strength, desiredCa, delta * _caSpeed);
	}

	private void HandleRadialBlur(float delta)
	{
		float desiredBlur = GetVelocityBasedEffect(_maxVelocity, _blurExponent, _minBlurPower, _maxBlurPower);
		_radialBlur.power = Mathf.Lerp(_radialBlur.power, desiredBlur, delta * _blurSpeed);
	}

	// Coroutine that you can call to ping pong vignette, ld, ca effects to handle certain events
	private IEnumerator<double> PingPongEventEffect()
	{
		float timeElapsed = 0f;
		float normalizedTime = 0f;

		float initialVg = _minVgIntensity;
		float initialCa = _minCaStrength;
		float initialLd = _minLdZoom;

		_vgSpeed = _pingPongSpeed;
		_caSpeed = _pingPongSpeed;
		_ldSpeed = _pingPongSpeed;

		do
		{
			timeElapsed += _deltaTime;
			normalizedTime = timeElapsed / _pingPongDuration;

			float weight = Mathf.PingPong(normalizedTime, 0.5f);

			_minVgIntensity = Mathf.Lerp(initialVg, _maxVgIntensity, weight);
			_minCaStrength = Mathf.Lerp(initialCa, _maxCaStrength, weight);
			_minLdZoom = Mathf.Lerp(initialLd, _maxLdZoom, weight);

			yield return Timing.WaitForOneFrame;
		}
		while (timeElapsed < _pingPongDuration);

		_minVgIntensity = _initialVgIntensity;
		_minCaStrength = _initialCaStrength;
		_minLdZoom = _initialLdZoom;

		_vgSpeed = _initialVgSpeed;
		_caSpeed = _initialCaSpeed;
		_ldSpeed = _initialLdSpeed;
	}

	public float GetVelocityBasedEffect(float maxVelocityMag, float velocityExponent, float minEffect, float maxEffect)
	{
		Vector3 playerVelocity = _playerVelocity;
		float velocityMag = playerVelocity.Length() / maxVelocityMag;
		float velocityScale = Mathf.Pow(velocityMag, velocityExponent);

		float desiredEffect = Mathf.Lerp(minEffect, maxEffect, velocityScale);
		desiredEffect = Mathf.Clamp(desiredEffect, minEffect, maxEffect);

		return desiredEffect;
	}

	private void GetPlayerVelocity(Vector3 velocity)
	{
		_playerVelocity = velocity;
	}
}
