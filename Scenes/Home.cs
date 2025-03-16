using Godot;
using ProjectTerminal.Resources;

/// <summary>
/// Home screen controller handling navigation to main application areas.
/// Manages visibility of buttons based on user state and terminal registration.
/// </summary>
public partial class Home : Control
{
    // UI Buttons
    private Button _adminPanelButton;
    private Button _posTerminalButton;
    private Button _newUserButton;

    // Managers
    private Node _logger;
    private AuthManager _authManager;
    private TerminalManager _terminalManager;

    public override void _Ready()
    {
        // Get singleton references
        _logger = GetNode<Node>("/root/Logger");
        _authManager = GetNode<AuthManager>("/root/AuthManager");
        _terminalManager = GetNode<TerminalManager>("/root/TerminalManager");

        _logger.Call("debug", "Home: Initializing Home screen");

        // Connect UI elements
        _adminPanelButton = GetNode<Button>("%AdminPanel");
        _posTerminalButton = GetNode<Button>("%POSTerminal");
        _newUserButton = GetNode<Button>("%NewUser");

        // Connect button signals
        _adminPanelButton.Pressed += OnAdminPanelButtonPressed;
        _posTerminalButton.Pressed += OnPOSTerminalButtonPressed;
        _newUserButton.Pressed += OnNewUserButtonPressed;

        // Connect to manager signals
        _authManager.Connect(AuthManager.SignalName.SessionChanged, new Callable(this, nameof(UpdateUI)));
        _terminalManager.Connect(TerminalManager.SignalName.TerminalIdentityChanged, new Callable(this, nameof(UpdateUI)));

        // Initialize UI state
        UpdateUI();
    }

    /// <summary>
    /// Updates UI elements based on current application state.
    /// This is the central method for handling UI visibility.
    /// </summary>
    private void UpdateUI()
    {
        bool isStaffLoggedIn = _authManager.IsStaffLoggedIn();
        bool isTerminalRegistered = _terminalManager.IsTerminalRegistered;
        bool isNewUser = _authManager.IsNewUser;

        _logger.Call("debug", $"Home: UI update - Staff logged in: {isStaffLoggedIn}, Terminal registered: {isTerminalRegistered}, Is new user: {isNewUser}");

        // New user state takes precedence
        if (isNewUser)
        {
            _newUserButton.Visible = true;
            _adminPanelButton.Visible = false;
            _posTerminalButton.Visible = false;
            return;
        }

        // Existing user flow
        _newUserButton.Visible = false;

        _adminPanelButton.Visible = isStaffLoggedIn;

        // POS Terminal button only visible when terminal is registered
        _posTerminalButton.Visible = isTerminalRegistered;
    }

    // Button handlers
    private void OnAdminPanelButtonPressed()
    {
        _logger.Call("info", "Home: Admin Panel button pressed");
        GetTree().ChangeSceneToFile("res://Scenes/AdminPanel/AdminPanel.tscn");
    }

    private void OnPOSTerminalButtonPressed()
    {
        _logger.Call("info", "Home: POS Terminal button pressed");
        GetTree().ChangeSceneToFile("res://Scenes/POSTerminal/POSMain.tscn");
    }

    private void OnNewUserButtonPressed()
    {
        _logger.Call("info", "Home: New User button pressed");
        GetTree().ChangeSceneToFile("res://Scenes/Onboarding/BrandNewUser.tscn");
    }

    // Helper methods for showing dialogs if needed later
    private void ShowDialog(string title, string message)
    {
        var dialog = new AcceptDialog();
        AddChild(dialog);
        dialog.Title = title;
        dialog.DialogText = message;
        dialog.PopupCentered();
    }
}
