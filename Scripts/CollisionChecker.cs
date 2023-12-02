using Godot;
using Godot.Collections;
using System;
using System.Linq;

public partial class CollisionChecker : Godot.Area3D
{
    public static event Action<string> OnGroupChange; // Event that's invoked whenever the collision group (tag) changes
    private Array<string> _touchingGroups = new();
    private string _currentTouchingGroup;

    public override void _Ready()
    {
        BodyEntered += StartRepeatCall;
        AreaEntered += StartRepeatCall;
        
        OnGroupChange += DebugGroup;

        BodyExited += StopRepeatCall;
        AreaExited += StopRepeatCall;
    }

    private void DebugGroup(string group)
    {
        //GD.Print(group);
    }

    public override void _ExitTree()
    {
        BodyEntered -= StartRepeatCall;
        AreaEntered -= StartRepeatCall;

        OnGroupChange -= DebugGroup;

        BodyExited -= StopRepeatCall;
        AreaExited -= StopRepeatCall;
    }

	public string GetGroupFromBody(Node3D body)
	{
        char[] trimArr = {'"', '[', '&', ']'};
        string group = string.Empty;

		group = body.GetGroups().ToString();
        group = group.Trim(trimArr);

        return group;
	}

    private void StartRepeatCall(Node3D body)
    {
        _touchingGroups.Add(GetGroupFromBody(body));

        // Priority
        if (_touchingGroups.Contains("Water"))
        {
            if (_currentTouchingGroup != "Water")
            {
                _currentTouchingGroup = "Water";
                OnGroupChange?.Invoke(_currentTouchingGroup);
            }
        }
        else
        {
            if (_currentTouchingGroup != _touchingGroups.LastOrDefault(string.Empty))
            {
                _currentTouchingGroup = _touchingGroups.LastOrDefault(string.Empty);
                OnGroupChange?.Invoke(_currentTouchingGroup);
            }
        }
    }

    private void StopRepeatCall(Node3D body)
    {
        _touchingGroups.Remove(GetGroupFromBody(body));

        if (_touchingGroups.Contains("Water"))
        {
            if (_currentTouchingGroup != "Water")
            {
                _currentTouchingGroup = "Water";
                OnGroupChange?.Invoke(_currentTouchingGroup);
            }
        }
        else
        {
            if (_currentTouchingGroup != _touchingGroups.LastOrDefault(string.Empty))
            {
                _currentTouchingGroup = _touchingGroups.LastOrDefault(string.Empty);
                OnGroupChange?.Invoke(_currentTouchingGroup);
            }
        }
    }
}
