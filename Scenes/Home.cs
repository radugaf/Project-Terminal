using Godot;

public partial class Home : Control
{
    private Button _adminPanelButton;
    private Button _posTerminalButton;
    private Button _newUserButton;
    private Node _logger;
    private AuthManager _authManager;

    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _authManager = GetNode<AuthManager>("/root/AuthManager");
        _logger.Call("debug", "Home: Initializing");

        _adminPanelButton = GetNode<Button>("%AdminPanel");
        _posTerminalButton = GetNode<Button>("%POSTerminal");
        _newUserButton = GetNode<Button>("%NewUser");

        _adminPanelButton.Pressed += OnAdminPanelButtonPressed;
        _posTerminalButton.Pressed += OnPOSTerminalButtonPressed;
        _newUserButton.Pressed += OnNewUserButtonPressed;

        _authManager.SessionChanged += UpdateUI;
        UpdateUI();
    }

    private void UpdateUI()
    {
        bool isLoggedIn = _authManager.IsLoggedIn();
        bool isNewUser = _authManager.IsNewUser;
        _logger.Call("debug", $"Home: UI update - LoggedIn: {isLoggedIn}, NewUser: {isNewUser}");

        if (isNewUser)
        {
            // Show only the new user button when user is new
            _newUserButton.Visible = true;
            _adminPanelButton.Visible = false;
            _posTerminalButton.Visible = false;
            return;
        }

        // Regular user view
        _newUserButton.Visible = false;
        _adminPanelButton.Visible = isLoggedIn;

        // Only show POS terminal button for authenticated users
        _posTerminalButton.Visible = isLoggedIn;
    }

    private void OnAdminPanelButtonPressed()
    {
        _logger.Call("info", "Home: Navigating to Admin Panel");
        GetTree().ChangeSceneToFile("res://Scenes/AdminPanel/AdminPanel.tscn");
    }

    private void OnPOSTerminalButtonPressed()
    {
        _logger.Call("info", "Home: Navigating to POS Terminal");
        GetTree().ChangeSceneToFile("res://Scenes/POSTerminal/POSMain.tscn");
    }

    private void OnNewUserButtonPressed()
    {
        _logger.Call("info", "Home: Navigating to New User onboarding");
        GetTree().ChangeSceneToFile("res://Scenes/Onboarding/BrandNewUser.tscn");
    }
}
