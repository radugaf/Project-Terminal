using Godot;
using System;

public partial class AdminPanel : Control
{
    private Node _logger;
    private ColorRect _colorRect;
    private Button _itemsButton;
    private Button _staffButton;
    private Button _exitButton;
    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "AdminPanel: Initializing AdminPanel scene");

        _colorRect = GetNode<ColorRect>("%ContentContainer");
        _itemsButton = GetNode<Button>("%ItemsButton");
        _staffButton = GetNode<Button>("%StaffButton");
        _exitButton = GetNode<Button>("%ExitButton");

        _exitButton.Pressed += OnExitButtonPressed;
    }

    private void OnExitButtonPressed()
    {
        GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Home.tscn");
    }
}
