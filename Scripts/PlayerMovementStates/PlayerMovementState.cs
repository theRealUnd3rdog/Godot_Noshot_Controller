public partial class PlayerMovementState : GodotParadiseState
{
    public PlayerMovement Movement;

    public override void Enter()
    {
        Movement = GetNode<PlayerMovement>("%Player");
    }
}