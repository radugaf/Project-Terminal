using Godot;
using System;

public partial class POSMain : Control
{
    private Logger _logger;

    private Button _exitButton;
    public override void _Ready()
    {
        // Initialize logger
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("POSMain: Initializing POSMain scene");

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
