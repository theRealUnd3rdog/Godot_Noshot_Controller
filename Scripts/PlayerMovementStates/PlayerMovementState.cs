public partial class PlayerMovementState : GodotParadiseState
{
    protected PlayerMovement Movement;

    public override void Enter()
    {
        Movement = GetNode<PlayerMovement>("%Player");
    }
}