using Godot;
using System;
using System.Collections.Generic;
using MEC;

[GlobalClass, Icon("res://addons/finite_state_machine/state_icon.png")]
public partial class Idle : MovementState
{
    // Camera shake
    private CamShakeInstance _idleShake;

    public override void Enter()
    {
        base.Enter();

        Timing.RunCoroutine(AlignMeshBeforeAutoAlignment(), "AutoMeshAlignment");

        Movement.AnimationPlayer.Set("parameters/Master/conditions/idle", true);

        // Camera shake
        _idleShake = CamShake.ShakePreset(CamShakePresets.Idle);
    }

    public override void Exit()
    {
        StopAutoMeshAlignment();

        Movement.AnimationPlayer.Set("parameters/Master/conditions/idle", false);

        CamShake.RemoveShake(_idleShake);
    }

    public override void PhysicsUpdate(double delta)
    {
        if (Input.IsActionJustPressed("jump"))
        {
            EmitSignal(SignalName.StateFinished, "Jump", new());
        }

        if (Movement.GetRawInputDirection() != Vector2.Zero)
        {
            EmitSignal(SignalName.StateFinished, "Sprint", new());
        }

        if (Movement.StanceFSM.CurrentState is Crouching && Movement.GetRawInputDirection() != Vector2.Zero)
        {
            EmitSignal(SignalName.StateFinished, "CrouchMove", new());
        }

		if (!Movement.IsOnFloor())
		{
			EmitSignal(SignalName.StateFinished, "Air", new());
		}
    }

    /// <summary>
	/// Coroutine that aligns the mesh with the neck
	/// </summary>
    private IEnumerator<double> AlignMeshBeforeAutoAlignment()
	{
        CoroutineHandle handle = Timing.RunCoroutine(Camera.AlignMeshWithDirectionConstant(), "AutoMeshAlignment");

        yield return Timing.WaitUntilDone(handle);
        RunAutoMeshAlignment();
	}

    private void RunAutoMeshAlignment() => Timing.RunCoroutine(Camera.RunBodyMeshRotation(), Segment.Process, "AutoMeshAlignment");
	private void StopAutoMeshAlignment() => Timing.KillCoroutines("AutoMeshAlignment");
}
