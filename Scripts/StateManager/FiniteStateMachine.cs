using Godot;
using System;
using System.Collections.Generic;

public class FiniteStateMachine
{
    protected Dictionary<string, BaseState> states = new Dictionary<string, BaseState>();
    public BaseState CurrentState {get; private set;}
    public string CurrentStateName {get; private set;}
    public string previousStateName {get; set;}

    public void Add(string key, BaseState state)
    {
        states[key] = state;
        state.fsm = this;
    }

    public void ExecuteStatePhysics(float delta) => CurrentState.PhysicsProcess(delta);
    public void ExecuteProcess(float delta) => CurrentState.Process(delta);
    public void ExecuteInput(InputEvent @event) => CurrentState.InputProcess(@event);

    public void InitialiseState(string newState)
    {
        CurrentState = states[newState];
        CurrentStateName = newState;
        CurrentState.Enter();
    }

    public void ChangeState(string newState, BaseState previous = null)
    {
        CurrentState.Exit();
        CurrentState = states[newState];
        CurrentStateName = newState;
        CurrentState.Enter(previous);
    }
}
