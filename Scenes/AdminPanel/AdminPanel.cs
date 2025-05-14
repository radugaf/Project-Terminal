using Godot;
using ProjectTerminal.Resources.Admin;

public partial class AdminPanel : Control
{
    private Logger _logger;
    private Control _contentContainer;
    private Button _dashboardButton;
    private Button _itemsButton;
    private Button _staffButton;
    private Button _settingsButton;

    // Services
    private AdminPanelService _adminService;
    private ContentManager _contentManager;

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("AdminPanel: Initializing AdminPanel scene");

        // Get UI references
        _contentContainer = GetNode<Control>("%ContentContainer");
        _dashboardButton = GetNode<Button>("%DashboardButton");
        _itemsButton = GetNode<Button>("%ItemsButton");
        _staffButton = GetNode<Button>("%StaffButton");
        _settingsButton = GetNode<Button>("%SettingsButton");

        // Initialize services
        InitializeServices();

        // Register content
        RegisterContent();

        // Connect signals
        ConnectSignals();

        // Load initial content
        CallDeferred(nameof(InitializeContent));

        _logger.Info("AdminPanel: AdminPanel scene initialized");
    }

    private void InitializeServices()
    {
        // Create AdminPanelService
        _adminService = new AdminPanelService();
        AddChild(_adminService);

        // Create ContentManager
        _contentManager = new ContentManager();
        AddChild(_contentManager);
        _contentManager.Initialize(_contentContainer);

        // Register ContentManager with service AFTER the service is ready
        CallDeferred(nameof(InitializeAdminServiceDeferred));
    }

    private void InitializeAdminServiceDeferred()
    {
        _adminService.Initialize(_contentManager);
    }

    private void InitializeContent()
    {
        _ = _contentManager.ShowContentAsync("Dashboard", false);
        UpdateActiveButton("Dashboard");
    }

    private void RegisterContent()
    {
        // Register all admin panel content
        _contentManager.RegisterContent("Dashboard",
            GD.Load<PackedScene>("res://Scenes/AdminPanel/Dashboard.tscn"));

        _contentManager.RegisterContent("Items",
            GD.Load<PackedScene>("res://Scenes/AdminPanel/Items.tscn"));

        _contentManager.RegisterContent("Staff",
            GD.Load<PackedScene>("res://Scenes/AdminPanel/Staff.tscn"));

        _contentManager.RegisterContent("AddCategory",
            GD.Load<PackedScene>("res://Scenes/AdminPanel/AddCategory.tscn"));

        _contentManager.RegisterContent("Settings",
            GD.Load<PackedScene>("res://Scenes/AdminPanel/Settings.tscn"));

    }

    private void ConnectSignals()
    {
        // Connect ContentManager signals
        _contentManager.ContentChanged += OnContentChanged;

        // Connect button signals
        _itemsButton.Pressed += async () => await _contentManager.ShowContentAsync("Items");
        _staffButton.Pressed += async () => await _contentManager.ShowContentAsync("Staff");
        _dashboardButton.Pressed += async () => await _contentManager.ShowContentAsync("Dashboard");
        _settingsButton.Pressed += async () => await _contentManager.ShowContentAsync("Settings");
    }

    private void OnContentChanged(string contentId, Control contentNode)
    {
        _logger.Info($"AdminPanel: Content changed to: {contentId}");

        // Update the UI to reflect the active section - only for top-level sections
        if (contentId == "Dashboard" || contentId == "Items" || contentId == "Staff" || contentId == "Settings")
        {
            UpdateActiveButton(contentId);
        }
    }

    private void UpdateActiveButton(string contentId)
    {
        // Reset all buttons
        _dashboardButton.ThemeTypeVariation = "";
        _itemsButton.ThemeTypeVariation = "";
        _staffButton.ThemeTypeVariation = "";

        // Set active button
        switch (contentId)
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
            case "Settings":
                _settingsButton.ThemeTypeVariation = "ActiveButton";
                break;
        }
    }
}
