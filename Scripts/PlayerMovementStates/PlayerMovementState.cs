using Godot;

public partial class PlayerMovementState : GodotParadiseState
{
    protected PlayerMovement Movement;

    public override void Enter()
    {
        Movement = GetOwner<PlayerMovement>();
    }
}