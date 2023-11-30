#if TOOLS
using Godot;
using System;

[Tool]
public partial class GodotParadiseFSMPlugin : EditorPlugin
{
	public override void _EnterTree()
	{
		AddCustomType("GodotParadiseFiniteStateMachine",
			"Node",
			GD.Load<Script>("res://addons/finite_state_machine/GodotParadiseFiniteStateMachine.cs"),
			GD.Load<Texture2D>("res://addons/finite_state_machine/icon.png")
		);

		AddCustomType("GodotParadiseState",
		"Node",
		GD.Load<Script>("res://addons/finite_state_machine/GodotParadiseState.cs"),
		GD.Load<Texture2D>("res://addons/finite_state_machine/state_icon.png")
	);
	}

	public override void _ExitTree() => RemoveCustomType("GodotParadiseFiniteStateMachine");
}
#endif