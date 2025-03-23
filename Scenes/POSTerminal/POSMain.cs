using Godot;
using System;

public partial class POSMain : Control
{

    private Button _exitButton;
    public override void _Ready()
    {
        // Initialize logger
        var logger = GetNode<Node>("/root/Logger");
        logger.Call("info", "POSMain: Initializing POSMain scene");

        // Initialize UI elements
        _exitButton = GetNode<Button>("%ExitButton");

        // Connect button signals
        _exitButton.Pressed += OnExitButtonPressed;
    }

    private void OnExitButtonPressed()
    {
        GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Home.tscn");
    }

}
