using Godot;
using System;

public class PlayerMovementState : BaseState
{
    protected PlayerMovement movement;

    public PlayerMovementState(PlayerMovement owner) {this.movement = owner;}
}
