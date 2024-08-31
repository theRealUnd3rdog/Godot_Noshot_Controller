using Godot;
using System;

[Tool]
public partial class Measurement : CsgBox3D
{
    [Export] private bool _update = true;
    [Export] private bool _showMeasurementText = true;

    [ExportCategory("Reference Text")]
    [Export] private string _refText;
    [Export(PropertyHint.Range, "15, 200, 1, or_greater")] private int _refFontSize = 50;
    [Export(PropertyHint.Range, "0.5, 2")] private float _refFontOffset = 0.75f;

    [ExportCategory("Measurement Text")]
    [Export(PropertyHint.Range, "15, 200, 1, or_greater")] private int _measurementFontSize = 50;

    // privates
    private Node3D _refControl;
    private Label3D _reference;
    private Label3D _yHeight;
    private Label3D _xWidth;
    private Label3D _zWidth;

    public override void _Ready()
    {
        _refControl = GetNode<Node3D>("RefControl");
        _reference = GetNode<Label3D>("RefControl/Reference");
        _yHeight = GetNode<Label3D>("RefControl/Y-height");
        _xWidth = GetNode<Label3D>("RefControl/X-width");
        _zWidth = GetNode<Label3D>("RefControl/Z-width");

        UpdatePositions();
    }

    public override void _Process(double delta)
    {
        if (_showMeasurementText)
        {
            _yHeight.Show();
            _xWidth.Show();
            _zWidth.Show();

            _reference.Show();
        }
        else
        {
            _yHeight.Hide();
            _xWidth.Hide();
            _zWidth.Hide();

            _reference.Hide();
        }

        if (!_update)
            return;

        UpdatePositions();
    }

    private void UpdatePositions()
    {
        _reference.Text = _refText;

        _reference.FontSize = _refFontSize;
        _yHeight.FontSize = _measurementFontSize;
        _xWidth.FontSize = _measurementFontSize;
        _zWidth.FontSize = _measurementFontSize;

        _yHeight.Text = Math.Round(Size.Y, 2).ToString() + "m";
        _xWidth.Text = Math.Round(Size.X, 2).ToString() + "m";
        _zWidth.Text = Math.Round(Size.Z, 2).ToString() + "m";

        Vector3 referencePos = _reference.Position;
        referencePos.Y = GetTransformedValue(referencePos.Y, _refFontOffset, Size.Y, Position.Y);
        _reference.Position = referencePos;

        _yHeight.Position = GetSizedTextPosition(_yHeight.Position, -0.5f, 0.5f, 0.525f, true, false, false);
        _xWidth.Position = GetSizedTextPosition(_xWidth.Position, 0.5f, -0.452f, 0.525f, false, true, false);
        _zWidth.Position = GetSizedTextPosition(_zWidth.Position, -0.5f, -0.453f, -0.525f, true, true, true);
    }

    private Vector3 GetSizedTextPosition(Vector3 startPosition, float startPosX, float startPosY, float startPosZ, bool flipX, bool flipY, bool flipZ)
    {
        Vector3 sizedPosition = startPosition;

        sizedPosition.Z = GetTransformedValue(sizedPosition.Z, startPosZ, Size.Z, Position.Z, flipZ);
        sizedPosition.X = GetTransformedValue(sizedPosition.X, startPosX, Size.X, Position.X, flipX);
        sizedPosition.Y = GetTransformedValue(sizedPosition.Y, startPosY, Size.Y, Position.Y, flipY);

        return sizedPosition;
    }

    private float GetTransformedValue(float sizedValue, float startValue, float startShapeSize, float startShapePos, bool flip = false)
    {
        float transformedValue = sizedValue;

        if (startShapeSize >= 1 && startShapePos >= 0)
        {
            transformedValue = !flip ? Mathf.Abs(1 - startShapeSize) + startValue : startValue;
            transformedValue -= Mathf.Abs((startShapeSize - 1) / 2);
            
        }
        else if (startShapeSize < 1 && startShapePos < 0)
        {
            transformedValue =  !flip ? startValue - Mathf.Abs(1 - startShapeSize) : startValue;
            transformedValue += Mathf.Abs((startShapeSize - 1) / 2);
        }
        else if (startShapeSize < 1 && startShapePos >= 0)
        {
            float halfValue = Mathf.Abs(1 - startShapeSize) / 2;

            transformedValue =  startValue;
            transformedValue = !flip ? transformedValue - halfValue : transformedValue + halfValue;
        }
        else if (startShapeSize >= 1 && startShapePos < 0)
        {
            float halfValue = Mathf.Abs(1 - startShapeSize) / 2;

            transformedValue =  startValue;
            transformedValue = !flip ? transformedValue + halfValue : transformedValue - halfValue;
        }

        return transformedValue;
    }
}
