using Godot;
using System;

[Tool]
public partial class RotatorNode3D : Node3D
{
    public enum Axis
    {
        Y,
        X,
        Z,
    }

    [Export] private Axis _axis = Axis.Y;

    [Export] private float _rotationSpeedDeg = 90f;
    
	public override void _Process(double delta)
	{
        switch(_axis)
        {
            case Axis.Y:
                Rotate(new Vector3(0f, 1f, 0f), Mathf.DegToRad(_rotationSpeedDeg));
                break;
            
            case Axis.X:
                Rotate(new Vector3(1f, 0f, 0f), Mathf.DegToRad(_rotationSpeedDeg));
                break;
            
            case Axis.Z:
                Rotate(new Vector3(0f, 0f, 1f), Mathf.DegToRad(_rotationSpeedDeg));
                break;
        }
        
	}
}
