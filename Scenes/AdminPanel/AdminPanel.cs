using Godot;
using ProjectTerminal.Resources;

public partial class AdminPanel : Control
{
    private Node _logger;
    private Control _contentContainer;
    private Button _dashboardButton;
    private Button _itemsButton;
    private Button _staffButton;
    private Button _exitButton;


    private ContentManager _contentManager;

    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "AdminPanel: Initializing AdminPanel scene");

        _contentContainer = GetNode<Control>("%ContentContainer");
        _dashboardButton = GetNode<Button>("%DashboardButton");
        _itemsButton = GetNode<Button>("%ItemsButton");
        _staffButton = GetNode<Button>("%StaffButton");
        _exitButton = GetNode<Button>("%ExitButton");

        // Initialize ContentManager
        _contentManager = new ContentManager();
        AddChild(_contentManager); // Add to scene tree
        _contentManager.Initialize(_contentContainer);

        // Register content scenes with ContentManager
        _contentManager.RegisterContent("Dashboard", GD.Load<PackedScene>("res://Scenes/AdminPanel/Dashboard.tscn"));
        _contentManager.RegisterContent("Items", GD.Load<PackedScene>("res://Scenes/AdminPanel/Items.tscn"));
        _contentManager.RegisterContent("Staff", GD.Load<PackedScene>("res://Scenes/AdminPanel/Staff.tscn"));
        _contentManager.RegisterContent("AddCategory", GD.Load<PackedScene>("res://Scenes/AdminPanel/AddCategory.tscn"));

        // Connect to ContentChanged signal to update UI
        _contentManager.ContentChanged += OnContentChanged;

        // Connect button signals
        _itemsButton.Pressed += () => _contentManager.ShowContent("Items");
        _staffButton.Pressed += () => _contentManager.ShowContent("Staff");
        _dashboardButton.Pressed += () => _contentManager.ShowContent("Dashboard");
        _exitButton.Pressed += OnExitButtonPressed;

        // Load default content
        _contentManager.ShowContent("Dashboard", false); // Don't add to history since it's initial content
        _logger.Call("info", "AdminPanel: AdminPanel scene initialized");
    }

    private void OnContentChanged(string contentName, Control contentNode)
    {
        _logger.Call("info", $"AdminPanel: Content changed to: {contentName}");

        // Update the UI to reflect the active section - but only for top-level sections
        if (contentName == "Dashboard" || contentName == "Items" || contentName == "Staff")
        {
            UpdateActiveButton(contentName);
        }

        // If this is a sub-content node (like AddCategory), we might need to set up additional navigation
        if (contentName == "AddCategory")
        {
            // We can access the node directly from the parameter
            AddCategory addCategoryNode = contentNode as AddCategory;
            if (addCategoryNode != null)
            {
                // Set up back navigation (we'll add this method to AddCategory)
                addCategoryNode.SetContentManager(_contentManager);
            }
        }
    }

    private void UpdateActiveButton(string contentName)
    {
        // Reset all buttons
        _dashboardButton.ThemeTypeVariation = "";
        _itemsButton.ThemeTypeVariation = "";
        _staffButton.ThemeTypeVariation = "";

        // Set active button
        switch (contentName)
        {
            case "Dashboard":
                _dashboardButton.ThemeTypeVariation = "ActiveButton";
                break;
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
