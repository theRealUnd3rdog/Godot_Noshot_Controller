using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using MEC;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class PlayerVault : PlayerMovementState
{
    public static event Action PlayerVaulted; // Event that fires whenever the vault started
    public static event Action PlayerVaultEnded; // Event that fires whenever the vault ends

    private double _deltaTime = 0f;

    public override void Enter()
    {
        base.Enter();

        CamShake.ShakePreset(CamShakePresets.Vault);
        PlayerVaulted?.Invoke();

        if (Movement.FSM.PreviousState is not PlayerAir)
            Movement.momentum = Movement.vaultMomentum;

        Vector3 vaultPoint = Vector3.Zero;
        Movement.CheckVault(_deltaTime, out vaultPoint);

        Timing.RunCoroutine(PerformVault(_deltaTime, vaultPoint).CancelWith(this), "Vault");
    }

    public override void _PhysicsProcess(double delta)
    {
        _deltaTime = delta;
    }

    public IEnumerator<double> PerformVault(double delta, Vector3 endPoint, float midPointHeight = 1f)
	{
		// Get points
		Vector3 startPoint = Movement.GlobalPosition;
		Vector3 centerPoint = (startPoint + endPoint) / 2;

		centerPoint += Vector3.Up * (startPoint.DistanceTo(endPoint) / 2f * midPointHeight);

		// Get duration based on current speed and distance

		float currentSpeed = Movement.Velocity.Length();
		float distanceFromVaultPoint = startPoint.DistanceTo(endPoint);

        float slowVaultDuration = 0.5f;
		float vaultDuration = currentSpeed >= 0.5f ? (distanceFromVaultPoint / currentSpeed) : slowVaultDuration;

        vaultDuration = Mathf.Clamp(vaultDuration, 0.2f, slowVaultDuration);

		float timeElapsed = 0f;
        
        GD.Print("Vault Duration: " + vaultDuration);
        // Play vault sound here

		do
		{
			timeElapsed += (float)delta;
			float normalizedTime = timeElapsed / vaultDuration;

			Vector3 centerPointLerp = startPoint.Lerp(centerPoint, normalizedTime);
			Vector3 endPointLerp = centerPoint.Lerp(endPoint, normalizedTime);
			centerPointLerp.Lerp(endPointLerp, normalizedTime);

			Movement.GlobalPosition = centerPointLerp;

			yield return Timing.WaitForOneFrame;
		}
		while (timeElapsed < vaultDuration);

        if (Movement.FSM.PreviousState is not PlayerAir)
            Movement.Jump(Movement.vaultJumpVelocity);

        PlayerVaultEnded?.Invoke();

        // end vault
        EmitSignal(SignalName.StateFinished, "PlayerAir", new());
	}
}
