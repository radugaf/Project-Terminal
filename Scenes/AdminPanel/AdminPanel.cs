using Godot;
using System.Collections.Generic;

public partial class AdminPanel : Control
{
    private Node _logger;
    private Control _contentContainer;
    private Button _dashboardButton;
    private Button _itemsButton;
    private Button _staffButton;
    private Button _exitButton;

    private Node _currentContent;
    private Dictionary<string, PackedScene> _contentScenes;

    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "AdminPanel: Initializing AdminPanel scene");

        _contentContainer = GetNode<Control>("%ContentContainer");
        _dashboardButton = GetNode<Button>("%DashboardButton");
        _itemsButton = GetNode<Button>("%ItemsButton");
        _staffButton = GetNode<Button>("%StaffButton");
        _exitButton = GetNode<Button>("%ExitButton");

        // Connect button signals
        _itemsButton.Pressed += () => LoadContent("Items");
        _staffButton.Pressed += () => LoadContent("Staff");
        _dashboardButton.Pressed += () => LoadContent("Dashboard");
        _exitButton.Pressed += OnExitButtonPressed;

        // Initialize content scenes dictionary
        _contentScenes = new Dictionary<string, PackedScene>
        {
            { "Dashboard", GD.Load<PackedScene>("res://Scenes/AdminPanel/Dashboard.tscn") },
            { "Items", GD.Load<PackedScene>("res://Scenes/AdminPanel/Items.tscn") },
            { "Staff", GD.Load<PackedScene>("res://Scenes/AdminPanel/Staff.tscn") },
        };

        // Load default content
        LoadContent("Dashboard");
        _logger.Call("info", "AdminPanel: AdminPanel scene initialized");
    }

    private void LoadContent(string contentName)
    {
        _logger.Call("debug", $"AdminPanel: Loading content: {contentName}");

        // Clear existing content
        if (_currentContent != null)
        {
            _currentContent.QueueFree();
            _currentContent = null;
        }

        // Check if the content scene exists in the dictionary and load it
        if (!_contentScenes.TryGetValue(contentName, out PackedScene contentScene))
        {
            _logger.Call("error", $"AdminPanel: Content scene not found: {contentName}");
            return;
        }

        Node contentInstance = contentScene.Instantiate();
        _contentContainer.AddChild(contentInstance);



        _currentContent = contentInstance;
        _logger.Call("info", $"AdminPanel: Content loaded: {contentName}");

        // Update the UI to reflect the active section
        UpdateActiveButton(contentName);
    }

    private void UpdateActiveButton(string contentName)
    {
        // Reset all buttons
        _itemsButton.ThemeTypeVariation = "";
        _staffButton.ThemeTypeVariation = "";

        // Set active button
        switch (contentName)
        {
            case "Items":
                _itemsButton.ThemeTypeVariation = "ActiveButton";
                break;
            case "Staff":
                _staffButton.ThemeTypeVariation = "ActiveButton";
                break;
        }
    }

    private void OnExitButtonPressed()
    {
        GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Home.tscn");
    }
}
