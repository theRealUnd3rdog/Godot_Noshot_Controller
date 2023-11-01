using Godot;
using System;

[GlobalClass]
public partial class AudioGroupPlayer : Resource
{
    [Export] public string[] groups;
    [Export] public AudioStream[] streams;
}
