using Godot;
using System;

public partial class PlayerShaker : Node
{
    [Export] private PlayerMovement _movement;
    private CamShakeInstance _idleShake;
    private CamShakeInstance _sprintShake;
    private CamShakeInstance _walkShake;
    private CamShakeInstance _inAirShake;
    private CamShakeInstance _slideShake;

    public override void _Ready()
    {
        if (_movement == null)
            return;

        _idleShake = CamShake.ShakePreset(CamShakePresets.Idle);
        _sprintShake = CamShake.ShakePreset(CamShakePresets.Sprinting);
        _walkShake = CamShake.ShakePreset(CamShakePresets.Walking);
        _inAirShake = CamShake.ShakePreset(CamShakePresets.InAir);
        _slideShake = CamShake.ShakePreset(CamShakePresets.Sliding);

        ResetAllInfluences();

        PlayerMovement.LastVelocityChangeLanded += ShakeWhenLanded;
    }

    public override void _ExitTree()
    {
        PlayerMovement.LastVelocityChangeLanded -= ShakeWhenLanded;
    }

    private void ShakeWhenLanded(Vector3 lastVelocity)
    {
        if (Mathf.Abs(lastVelocity.Y) > 10f || Mathf.Abs(lastVelocity.Y) > 7f)
            CamShake.ShakePreset(CamShakePresets.Roll);
    }

    public override void _Process(double delta)
    {
        if (_movement == null)
            return;

        switch (_movement.moveState)
        {
            case MovementState.Idle:
                ChangeShakeInfluence(_idleShake);
                break;

            case MovementState.InAir:
                ChangeShakeInfluence(_inAirShake);
                break;

            case MovementState.Walking:
                ChangeShakeInfluence(_walkShake);
                break;

            case MovementState.Crouching:
                ChangeShakeInfluence(_walkShake);
                break;

            case MovementState.Sprinting:
                ChangeShakeInfluence(_sprintShake);
                break;
            
            case MovementState.Sliding:
                ChangeShakeInfluence(_slideShake);
                break;

            default:
                ChangeShakeInfluence(_idleShake);
                break;
        }
    }

    private void ChangeShakeInfluence(CamShakeInstance instance)
    {
        if (instance.influence >= 1f)
            return;

        ResetAllInfluences();

        instance.influence = 1f;
    }

    private void ResetAllInfluences()
    {
        _idleShake.influence = 0;
        _sprintShake.influence = 0;
        _walkShake.influence = 0;
        _inAirShake.influence = 0;
        _slideShake.influence = 0;
    }
}
