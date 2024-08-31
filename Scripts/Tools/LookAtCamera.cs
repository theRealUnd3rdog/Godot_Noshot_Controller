using Godot;
using System;

[Tool]
public partial class LookAtCamera : Label3D
{
    public override void _Process(double delta)
    {
        var viewport = EditorInterface.Singleton.GetEditorViewport3D();
        var cam = viewport.GetCamera3D();

        // Calculate the direction from the label to the camera
        Vector3 lookAtDirection = (cam.Transform.Origin - GlobalTransform.Origin).Normalized();

        LookAt(-lookAtDirection, Vector3.Up);
    }
}
