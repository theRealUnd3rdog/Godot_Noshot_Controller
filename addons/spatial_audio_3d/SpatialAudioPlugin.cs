#if TOOLS
using Godot;
using System;

[Tool]
public partial class SpatialAudioPlugin : EditorPlugin
{
	public override void _EnterTree()
	{
		var gui = GetEditorInterface().GetBaseControl();
		var loadIcon = gui.GetThemeIcon("AudioStreamPlayer3D", "EditorIcons");

		AddCustomType("SpatialAudioPlayer3D",
			"AudioStreamPlayer3D",
			GD.Load<Script>("res://addons/spatial_audio_3d/SpatialAudioPlayer3D.cs"),
			loadIcon
		);
	}

	public override void _ExitTree() => RemoveCustomType("SpatialAudioPlayer3D");
}
#endif