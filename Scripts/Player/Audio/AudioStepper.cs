using Godot;
using System;
using Godot.Collections;

public partial class AudioStepper : Node3D
{
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    
    [ExportSubgroup("Step sounds")]
	[Export] private AudioStreamPlayer3D _step;
	[Export] private Array<AudioGroupPlayer> _stepPlayers;
	private string _currentGroup;

    public override void _Ready()
    {
        CollisionChecker.OnGroupChange += GetStepGroup;
    }

    public override void _ExitTree()
    {
        CollisionChecker.OnGroupChange -= GetStepGroup;
    }

    private void GetStepGroup(string curGroup)
	{
		_currentGroup = curGroup;
	}

    public void PlayStepSound()
	{
		// fail safe default
		if (_currentGroup == string.Empty || _currentGroup == null)
			_currentGroup = "Concrete";
		
		LoadStepSound(_currentGroup);

		_step.PitchScale = _rng.RandfRange(0.9f, 1.1f);
		_step.Play();
	}

	private void LoadStepSound(string group)
	{
		// loop through through the audio groups
		foreach (AudioGroupPlayer player in _stepPlayers)
		{
			// loop through the groups
			foreach (string curGroup in player.groups)
			{
				if (group == curGroup)
				{
					_step.Stream = player.streams[_rng.RandiRange(0, player.streams.Length - 1)];

					// break if group is found
					break;
				}
			}
		}
	}
}
