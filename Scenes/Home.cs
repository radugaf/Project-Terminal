using Godot;
using System;
using System.Threading.Tasks;
using ProjectTerminal.Resources;

/// <summary>
/// Home screen controller handling navigation to main application areas.
/// Manages visibility of buttons based on user state and terminal registration.
/// </summary>
public partial class Home : Control
{
    #region Nodes

    private Button _adminPanelButton;
    private Button _posTerminalButton;
    private Button _newUserButton;
    private Node _logger;
    private AuthManager _authManager;
    private TerminalManager _terminalManager;

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Called when the node enters the scene tree for the first time.
    /// Sets up button connections and initializes the UI based on user state.
    /// </summary>
    public override void _Ready()
    {
        // Get references to singleton managers
        _logger = GetNode<Node>("/root/Logger");
        _authManager = GetNode<AuthManager>("/root/AuthManager");
        _terminalManager = GetNode<TerminalManager>("/root/TerminalManager");

        _logger.Call("debug", "Home: Initializing Home screen");

        // Connect button references
        _adminPanelButton = GetNode<Button>("%AdminPanel");
        _posTerminalButton = GetNode<Button>("%POSTerminal");
        _newUserButton = GetNode<Button>("%NewUser");

        // Connect button signals to handlers
        _adminPanelButton.Pressed += OnAdminPanelButtonPressed;
        _posTerminalButton.Pressed += OnPosTerminalButtonPressed;
        _newUserButton.Pressed += OnNewUserButtonPressed;

        // Set up UI based on user state and terminal registration
        InitializeUI();

        // Connect to session changed signal to update UI when auth state changes
        _authManager.Connect(AuthManager.SignalName.SessionChanged, new Callable(this, nameof(OnSessionChanged)));
        _terminalManager.Connect(TerminalManager.SignalName.TerminalIdentityChanged, new Callable(this, nameof(OnTerminalIdentityChanged)));
    }

    /// <summary>
    /// Sets up the initial UI state based on authentication and terminal registration status.
    /// </summary>
    private void InitializeUI()
    {
        _logger.Call("debug", "Home: Setting up initial UI state");

        // Check if user is logged in
        bool isStaffLoggedIn = _authManager.IsStaffLoggedIn();
        bool isTerminalRegistered = _terminalManager.IsTerminalRegistered;

        _logger.Call("debug", $"Home: Staff logged in: {isStaffLoggedIn}, Terminal registered: {isTerminalRegistered}");

        // Set button visibility based on auth state
        UpdateButtonVisibility(isStaffLoggedIn, isTerminalRegistered);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Called when the user session changes (login/logout).
    /// </summary>
    private void OnSessionChanged()
    {
        _logger.Call("debug", "Home: Session changed, updating UI");
        bool isStaffLoggedIn = _authManager.IsStaffLoggedIn();
        bool isTerminalRegistered = _terminalManager.IsTerminalRegistered;
        UpdateButtonVisibility(isStaffLoggedIn, isTerminalRegistered);
    }

    /// <summary>
    /// Called when the terminal identity changes (registered/unregistered).
    /// </summary>
    private void OnTerminalIdentityChanged()
    {
        _logger.Call("debug", "Home: Terminal identity changed, updating UI");
        bool isStaffLoggedIn = _authManager.IsStaffLoggedIn();
        bool isTerminalRegistered = _terminalManager.IsTerminalRegistered;
        UpdateButtonVisibility(isStaffLoggedIn, isTerminalRegistered);
    }

    /// <summary>
    /// Updates which buttons are visible based on authentication and terminal registration state.
    /// </summary>
    private void UpdateButtonVisibility(bool isStaffLoggedIn, bool isTerminalRegistered)
    {
        if (!isStaffLoggedIn || !isTerminalRegistered)
        {
            // New user flow: only show new user button
            _adminPanelButton.Visible = false;
            _posTerminalButton.Visible = false;
            _newUserButton.Visible = true;
            _logger.Call("debug", "Home: Showing new user flow (only New User button)");
        }
        else
        {
            // Existing user: show admin/POS buttons based on role
            _newUserButton.Visible = false;

            // Show admin panel only for Owner/Manager roles
            _adminPanelButton.Visible = _authManager.CurrentUserRole != StaffRole.Staff;

            // Always show POS terminal for registered users
            _posTerminalButton.Visible = true;

            _logger.Call("debug", $"Home: Showing regular user flow (role: {_authManager.CurrentUserRole})");
        }
    }

    #endregion

    #region Button Handlers

    /// <summary>
    /// Handler for the Admin Panel button press.
    /// Navigates to the admin panel scene.
    /// </summary>
    private async void OnAdminPanelButtonPressed()
    {
        _logger.Call("info", "Home: Admin Panel button pressed");

        // Check permissions before allowing access
        bool hasAdminPermission = await _authManager.HasPermissionAsync("access_admin");

        if (!hasAdminPermission)
        {
            _logger.Call("warn", "Home: User does not have admin permission");
            // Show permission denied message
            DisplayPermissionDeniedMessage();
            return;
        }

        // Navigate to admin panel
        GetTree().ChangeSceneToFile("res://Scenes/AdminPanel/AdminPanel.tscn");
    }

    /// <summary>
    /// Handler for the POS Terminal button press.
    /// Navigates to the POS terminal main screen.
    /// </summary>
    private async void OnPosTerminalButtonPressed()
    {
        _logger.Call("info", "Home: POS Terminal button pressed");

        // Check location authorization before allowing access
        bool isAuthorized = await _authManager.IsAuthorizedForLocationAsync();

        if (!isAuthorized)
        {
            _logger.Call("warn", "Home: User is not authorized for this location");
            // Show authorization denied message
            DisplayAuthorizationDeniedMessage();
            return;
        }

        // Update terminal heartbeat to show it's active
        await _terminalManager.UpdateTerminalHeartbeatAsync();

        // Navigate to POS terminal main screen
        GetTree().ChangeSceneToFile("res://Scenes/POSTerminal/POSMain.tscn");
    }

    /// <summary>
    /// Handler for the New User button press.
    /// Navigates to the new user onboarding screen.
    /// </summary>
    private void OnNewUserButtonPressed()
    {
        _logger.Call("info", "Home: New User button pressed");

        // Navigate to new user onboarding
        GetTree().ChangeSceneToFile("res://Scenes/Onboarding/BrandNewUser.tscn");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Displays a message when user doesn't have admin permissions.
    /// </summary>
    private void DisplayPermissionDeniedMessage()
    {
        // Create a popup dialog for permission denied
        var dialog = new AcceptDialog();
        AddChild(dialog);
        dialog.Title = "Permission Denied";
        dialog.DialogText = "You do not have permission to access the Admin Panel.";
        dialog.PopupCentered();
    }

    /// <summary>
    /// Displays a message when user is not authorized for this location.
    /// </summary>
    private void DisplayAuthorizationDeniedMessage()
    {
        // Create a popup dialog for authorization denied
        var dialog = new AcceptDialog();
        AddChild(dialog);
        dialog.Title = "Access Denied";
        dialog.DialogText = "You are not authorized to access this terminal location.";
        dialog.PopupCentered();
    }

    #endregion
}
