using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class StateManager<EState> : Node where EState : Enum
{
    public Dictionary<EState, BaseState<EState>> States = new Dictionary<EState, BaseState<EState>>();
    public BaseState<EState> CurrentState;

    public bool IsTransitioningState = false;

    public override void _Ready()
    {
        CurrentState.EnterState();
    }

    public override void _Process(double delta)
    {
        EState nextStateKey = CurrentState.GetNextState();

        if (!IsTransitioningState && nextStateKey.Equals(CurrentState.StateKey))
        {
            CurrentState.UpdateState();
        }
        else if (!IsTransitioningState)
        {
            TransitionToState(nextStateKey);
        }
    }

    public void TransitionToState(EState stateKey)
    {
        IsTransitioningState = true;
        CurrentState.ExitState();

        CurrentState = States[stateKey];

        CurrentState.EnterState();
        IsTransitioningState = false;
    }
}
