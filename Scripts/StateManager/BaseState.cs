using Godot;
using System;

public class BaseState
{
    public FiniteStateMachine fsm;
    
    public virtual void Enter(BaseState previous = null) {}
    public virtual void Exit() {}
    public virtual void Process(float delta) {}
    public virtual void PhysicsProcess(float delta) {}
    public virtual void InputProcess(InputEvent @event) {}
}
