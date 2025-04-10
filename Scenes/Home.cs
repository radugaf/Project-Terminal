using Godot;
using System;
using ProjectTerminal.Globals.Services;

public partial class Home : Control
{
    private Logger _logger;
    private AuthManager _authManager;
    private TerminalManager _terminalManager;

    // UI Elements
    private Button _adminPanelButton;
    private Button _posTerminalButton;
    private Button _newUserButton;
    private Header _header;
    private MarginContainer _progressContainer;
    private ProgressBar _progressBar;

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _authManager = GetNode<AuthManager>("/root/AuthManager");
        _terminalManager = GetNode<TerminalManager>("/root/TerminalManager");
        _logger.Debug("Home: Initializing");

        InitializeUIElements();
        ConnectSignals();
        UpdateUI();
    }
    private void InitializeUIElements()
    {
        _adminPanelButton = GetNode<Button>("%AdminPanel");
        _posTerminalButton = GetNode<Button>("%POSTerminal");
        _newUserButton = GetNode<Button>("%NewUser");
        _header = GetNode<Header>("%Header");
        _progressContainer = GetNode<MarginContainer>("%ProgressContainer");
        _progressBar = GetNode<ProgressBar>("%ProgressBar");
    }

    private void ConnectSignals()
    {
        _adminPanelButton.Pressed += OnAdminPanelButtonPressed;
        _posTerminalButton.Pressed += OnPOSTerminalButtonPressed;
        _newUserButton.Pressed += OnNewUserButtonPressed;
        _authManager.SessionChanged += UpdateUI;

        // Connect header signals
        _header.LogoClicked += OnLogoClicked;
        _header.SettingsSelected += OnSettingsSelected;
        _header.LogoutSelected += OnLogoutSelected;
    }

    private void UpdateUI()
    {
        bool isLoggedIn = _authManager.IsLoggedIn();
        bool isNewUser = _authManager.IsNewUser;
        bool hasTerminal = !string.IsNullOrEmpty(_terminalManager?.TerminalName);
        _logger.Debug($"Home: UI update - LoggedIn: {isLoggedIn}, NewUser: {isNewUser}, HasTerminal: {hasTerminal}");

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
        _posTerminalButton.Visible = isLoggedIn && hasTerminal;

        // Update header information
        _header.UpdateOrganizationInfo();
    }

    private void OnLogoClicked()
    {
        _logger.Info("Home: Logo clicked, refreshing home screen");
        UpdateUI();
    }

    private void OnSettingsSelected()
    {
        _logger.Info("Home: Navigating to Settings page");
        GetTree().ChangeSceneToFile("res://Scenes/Settings/Settings.tscn");
    }

    private async void OnLogoutSelected()
    {
        try
        {
            _logger.Info("Home: Logging out user");

            // Show progress indicator
            _progressContainer.Visible = true;
            _progressBar.Value = 50;

            // Log out the user
            await _authManager.LogoutAsync();

            _progressBar.Value = 100;

            // Navigate to login screen
            _logger.Info("Home: Navigating to Login page");
            GetTree().ChangeSceneToFile("res://Scenes/Auth/Login.tscn");
        }
        catch (Exception ex)
        {
            _logger.Error($"Home: Logout failed: {ex.Message}");
            _progressContainer.Visible = false;

            // Show error message
            OS.Alert("Logout failed. Please try again.", "Error");
        }
    }




    private void OnAdminPanelButtonPressed()
    {
        _logger.Info("Home: Navigating to Admin Panel");
        GetTree().ChangeSceneToFile("res://Scenes/AdminPanel/AdminPanel.tscn");
    }

    private void OnPOSTerminalButtonPressed()
    {
        _logger.Info("Home: Navigating to POS Terminal");
        GetTree().ChangeSceneToFile("res://Scenes/POSTerminal/POSMain.tscn");
    }

    private void OnNewUserButtonPressed()
    {
        _logger.Info("Home: Navigating to New User onboarding");
        GetTree().ChangeSceneToFile("res://Scenes/Onboarding/OnboardingForm.tscn");
    }

}
