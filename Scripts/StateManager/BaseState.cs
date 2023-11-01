using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public abstract class BaseState<EState> where EState : Enum
{
    public BaseState(EState key)
    {
        StateKey = key;
    }

    public EState StateKey {get; private set;}

    public abstract void EnterState();
    public abstract void ExitState();
    public abstract void UpdateState();
    public abstract EState GetNextState();
}
