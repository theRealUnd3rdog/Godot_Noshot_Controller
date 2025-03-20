using Godot;
using System.Collections.Generic;
using System.Linq;

public class FovModifier
{
    public float Fov;
    public int Priority;
    public float Duration; // -1 means permanent
    public float TimeLeft;
    public float TransitionTime; // How long it takes to transition

    public FovModifier(float fov, int priority, float duration, float transitionTime)
    {
        Fov = fov;
        Priority = priority;
        Duration = duration;
        TimeLeft = duration;
        TransitionTime = transitionTime;
    }
}

public partial class FOVController : Node
{
    public static FOVController Instance;
    [Export] private Camera3D _camera;
    [Export] public float defaultFov {private set; get;} = 75f;

    private List<FovModifier> _fovModifiers = new List<FovModifier>();
    private float _currentFov;
    private float _targetFov;
    private float _transitionStartFov;
    private float _transitionDuration;
    private float _transitionElapsedTime;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        _targetFov = defaultFov;
        _currentFov = defaultFov;
    }

    public override void _Process(double delta)
    {
        // Update time-based modifiers
        _fovModifiers = _fovModifiers
            .Where(mod => mod.Duration < 0 || (mod.TimeLeft -= (float)delta) > 0)
            .ToList();

        // Determine highest priority FOV
        float newTargetFov = _fovModifiers.Count > 0 ? _fovModifiers.MaxBy(mod => mod.Priority).Fov : defaultFov;

        // Start a new transition if FOV target changes
        if (!Mathf.IsEqualApprox(newTargetFov, _targetFov))
        {
            _transitionStartFov = _currentFov;
            _targetFov = newTargetFov;
            _transitionDuration = _fovModifiers.Count > 0 ? _fovModifiers.MaxBy(mod => mod.Priority).TransitionTime : 0.5f;
            _transitionElapsedTime = 0f;
        }

        // Smooth transition using duration-based interpolation
        if (_transitionElapsedTime < _transitionDuration)
        {
            _transitionElapsedTime += (float)delta;
            float t = Mathf.Clamp(_transitionElapsedTime / _transitionDuration, 0, 1);
            _currentFov = Mathf.Lerp(_transitionStartFov, _targetFov, t);
        }
        else
        {
            _currentFov = _targetFov;
        }

        _camera.Fov = _currentFov;
    }

    /// <summary>
    /// Adds a new FOV modifier and returns its reference.
    /// </summary>
    public FovModifier AddFovModifier(float fov, int priority, float duration = -1, float transitionDuration = 0.5f)
    {
        var modifier = new FovModifier(fov, priority, duration, transitionDuration);
        _fovModifiers.Add(modifier);
        return modifier;
    }

    /// <summary>
    /// Removes a specific FOV modifier.
    /// </summary>
    public void RemoveFovModifier(FovModifier modifier)
    {
        _fovModifiers.Remove(modifier);
    }
}
