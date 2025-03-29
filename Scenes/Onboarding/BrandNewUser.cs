using Godot;
using System;
using ProjectTerminal.Resources;
using System.Collections.Generic;

public partial class BrandNewUser : Control
{
    private Logger _logger;
    private SupabaseClient _supabaseClient;
    private AuthManager _authManager;
    private OrganizationManager _organizationManager;

    // UI Elements
    private LineEdit _firstNameLineEdit;
    private LineEdit _lastNameLineEdit;
    private LineEdit _emailLineEdit;
    private LineEdit _businessNameLineEdit;
    private OptionButton _businessTypeOptionButton;
    private Button _deviceYesButton;
    private Button _deviceNoButton;
    private Button _submitButton;
    private Label _statusLabel;

    // State tracking
    private bool _registerAsTerminal = false;

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("BrandNewUser: Brand New User scene initializing");

        // Get references to managers
        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");
        _authManager = GetNode<AuthManager>("/root/AuthManager");
        _organizationManager = GetNode<OrganizationManager>("/root/OrganizationManager");

        // Get references to UI components
        _firstNameLineEdit = GetNode<LineEdit>("%FirstNameLineEdit");
        _lastNameLineEdit = GetNode<LineEdit>("%LastNameLineEdit");
        _emailLineEdit = GetNode<LineEdit>("%EmailLineEdit");
        _businessNameLineEdit = GetNode<LineEdit>("%BusinessNameLineEdit");
        _businessTypeOptionButton = GetNode<OptionButton>("%BusinessTypeOptionButton");
        _deviceYesButton = GetNode<Button>("%DeviceYesButton");
        _deviceNoButton = GetNode<Button>("%DeviceNoButton");
        _submitButton = GetNode<Button>("%SubmitButton");
        _statusLabel = GetNode<Label>("%StatusLabel");

        if (_authManager.CurrentUser != null && !string.IsNullOrEmpty(_authManager.CurrentUser.Email))
        {
            _emailLineEdit.Text = _authManager.CurrentUser.Email;
            _emailLineEdit.Editable = false; // Make it read-only as it's already verified
            _logger.Debug("BrandNewUser: Pre-populated email from authenticated user");
        }


        PopulateBusinessTypeOptions();

        // Clear status label
        if (_statusLabel != null)
            _statusLabel.Text = "";

        _deviceYesButton.ToggleMode = true;
        _deviceNoButton.ToggleMode = true;

        // Connect signals
        _submitButton.Pressed += OnSubmitButtonPressed;
        _deviceYesButton.Pressed += OnDeviceYesButtonPressed;
        _deviceNoButton.Pressed += OnDeviceNoButtonPressed;
    }

    private void OnDeviceYesButtonPressed()
    {
        _registerAsTerminal = true;
        _deviceYesButton.ButtonPressed = true;
        _deviceNoButton.ButtonPressed = false;
        _logger.Debug("BrandNewUser: Device will be registered as a terminal");
    }

    private void OnDeviceNoButtonPressed()
    {
        _registerAsTerminal = false;
        _deviceYesButton.ButtonPressed = false;
        _deviceNoButton.ButtonPressed = true;
        _logger.Debug("BrandNewUser: Device will not be registered as a terminal");
    }

    private async void OnSubmitButtonPressed()
    {
        try
        {
            _logger.Info("BrandNewUser: Submit button pressed, starting registration flow");

            // Disable submit button to prevent multiple submissions
            _submitButton.Disabled = true;
            UpdateStatusLabel("Processing...");

            // Validate input fields
            if (!ValidateInputs())
            {
                _submitButton.Disabled = false;
                return;
            }

            // 1. Update user email
            if (_emailLineEdit.Editable)
            {
                await _authManager.UpdateUserEmailAsync(_emailLineEdit.Text.Trim());
            }

            // 2. Create organization
            var businessType = (BusinessType)_businessTypeOptionButton.Selected;
            string _createdOrganizationId = await _organizationManager.CreateOrganizationAsync(
                _businessNameLineEdit.Text.Trim(),
                businessType,
                _emailLineEdit.Text.Trim()
            );

            // 4. Register staff as owner
            string staffId = await _organizationManager.RegisterStaffOwnerAsync(
                _createdOrganizationId,
                _firstNameLineEdit.Text.Trim(),
                _lastNameLineEdit.Text.Trim(),
                _emailLineEdit.Text.Trim()
            );

            UpdateStatusLabel("Registration complete!");
            _logger.Info("BrandNewUser: Registration completed successfully");

            bool isNewUser = _authManager.SetUserAsExisting();
            _logger.Info($"BrandNewUser: User set as existing: {isNewUser}");

            GoToNextScreen();
        }
        catch (Exception ex)
        {
            UpdateStatusLabel($"Error: {ex.Message}");
            _logger.Error($"BrandNewUser: Registration failed: {ex.Message}", new Dictionary<string, object> { { "stack_trace", ex.StackTrace } });
            _submitButton.Disabled = false;
        }
    }

    private bool ValidateInputs()
    {
        // Check firstName
        if (string.IsNullOrWhiteSpace(_firstNameLineEdit.Text))
        {
            UpdateStatusLabel("Error: First name is required");
            _logger.Warn("BrandNewUser: Missing first name");
            return false;
        }

        // Check lastName
        if (string.IsNullOrWhiteSpace(_lastNameLineEdit.Text))
        {
            UpdateStatusLabel("Error: Last name is required");
            _logger.Warn("BrandNewUser: Missing last name");
            return false;
        }

        // Check email
        if (string.IsNullOrWhiteSpace(_emailLineEdit.Text) || !_emailLineEdit.Text.Contains("@"))
        {
            UpdateStatusLabel("Error: Valid email is required");
            _logger.Warn("BrandNewUser: Invalid email");
            return false;
        }

        // Check businessName
        if (string.IsNullOrWhiteSpace(_businessNameLineEdit.Text))
        {
            UpdateStatusLabel("Error: Business name is required");
            _logger.Warn("BrandNewUser: Missing business name");
            return false;
        }

        // Check business type selection
        if (_businessTypeOptionButton.Selected == -1)
        {
            UpdateStatusLabel("Error: Please select a business type");
            _logger.Warn("BrandNewUser: No business type selected");
            return false;
        }

        // Check terminal selection
        if (!_deviceYesButton.ButtonPressed && !_deviceNoButton.ButtonPressed)
        {
            UpdateStatusLabel("Error: Please select whether to use this device as a terminal");
            _logger.Warn("BrandNewUser: Terminal selection not made");
            return false;
        }

        return true;
    }

    private void UpdateStatusLabel(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
            _logger.Debug($"BrandNewUser: Status updated - {message}");
        }
    }

    private void PopulateBusinessTypeOptions()
    {
        _businessTypeOptionButton.Clear();

        // Add enum values to dropdown
        foreach (BusinessType type in Enum.GetValues(typeof(BusinessType)))
        {
            _businessTypeOptionButton.AddItem(type.ToString());
        }

        // Set default selection
        if (_businessTypeOptionButton.ItemCount > 0)
            _businessTypeOptionButton.Select(0);
    }

    private void GoToNextScreen()
    {
        try
        {
            if (_registerAsTerminal)
            {
                // If registered as terminal, go to Terminal scene
                GetTree().ChangeSceneToFile("res://Scenes/Onboarding/RegisterThisTerminal.tscn");
            }
            else
            {
                // Only set up timer for Home scene if not registering terminal
                GetTree().ChangeSceneToFile("res://Scenes/Home.tscn");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"BrandNewUser: Exception in GoToNextScreen: {ex.Message}");
        }
    }
}
