using Godot;
using System;
using ProjectTerminal.Globals.Services;

public partial class Header : Control
{
    [Signal]
    public delegate void LogoClickedEventHandler();

    [Signal]
    public delegate void SettingsSelectedEventHandler();

    [Signal]
    public delegate void LogoutSelectedEventHandler();

    private Logger _logger;
    private AuthManager _authManager;
    private OrganizationManager _organizationManager;

    private TextureButton _logoButton;
    private Label _orgNameLabel;


    // Option button item indices
    private const int SETTINGS_OPTION_INDEX = 0;
    private const int LOGOUT_OPTION_INDEX = 1;

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _authManager = GetNode<AuthManager>("/root/AuthManager");
        _organizationManager = GetNode<OrganizationManager>("/root/OrganizationManager");

        _logoButton = GetNode<TextureButton>("%LogoButton");
        _orgNameLabel = GetNode<Label>("%OrgNameLabel");


        ConnectSignals();
        UpdateOrganizationInfo();

        _logger.Debug("Header: Component initialized");
    }


    private void ConnectSignals()
    {
        _logoButton.Pressed += OnLogoButtonPressed;
    }

    public void UpdateOrganizationInfo()
    {
        try
        {
            if (_authManager.IsLoggedIn() && !_authManager.IsNewUser)
            {
                string orgName = _organizationManager.OrgName;
                _orgNameLabel.Text = orgName;

                // Show the header content
                Visible = true;
            }
            else
            {
                // Hide the header for new users or not logged in
                Visible = false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Header: Failed to update organization info: {ex.Message}");
            _orgNameLabel.Text = "Organization";
        }
    }

    private void OnLogoButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Home.tscn");
    }

}
