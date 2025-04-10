using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using ProjectTerminal.Resources;
using ProjectTerminal.Globals.Services;

public partial class OnboardingForm : Control
{
    #region Dependencies
    private Logger _logger;
    private OrganizationManager _organizationManager;
    private AddressManager _addressManager;
    private TerminalManager _terminalManager;
    private AuthManager _authManager;
    #endregion

    #region UI Elements
    // Personal Information
    private LineEdit _firstNameLineEdit;
    private LineEdit _lastNameLineEdit;
    private MarginContainer _emailContainer;
    private LineEdit _emailLineEdit;
    private MarginContainer _phoneContainer;
    private LineEdit _phoneLineEdit;

    // Business Information
    private LineEdit _businessNameLineEdit;
    private OptionButton _businessTypeOptionButton;
    private CheckBox _useThisDeviceCheckBox;

    // Terminal Registration
    private MarginContainer _locationNameContainer;
    private LineEdit _locationNameLineEdit;
    private MarginContainer _cityContainer;
    private LineEdit _cityLineEdit;
    private MarginContainer _addressContainer;
    private LineEdit _addressLineEdit;
    private MarginContainer _postalCodeContainer;
    private LineEdit _postalCodeLineEdit;

    // UI State
    private MarginContainer _statusContainer;
    private Label _statusLabel;
    private Button _submitButton;
    private MarginContainer _progressContainer;
    private ProgressBar _progressBar;
    #endregion

    // Form processing state
    private bool _isProcessing = false;
    private int _currentStep = 0;
    private const int TOTAL_STEPS = 4;

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("OnboardingForm: Initializing");

        InitializeDependencies();
        InitializeUIReferences();
        ConnectSignals();
        SetupInitialState();
    }

    private void InitializeDependencies()
    {
        try
        {
            _organizationManager = GetNode<OrganizationManager>("/root/OrganizationManager");
            _addressManager = GetNode<AddressManager>("/root/AddressManager");
            _terminalManager = GetNode<TerminalManager>("/root/TerminalManager");
            _authManager = GetNode<AuthManager>("/root/AuthManager");
            _logger.Debug("OnboardingForm: Dependencies initialized");
        }
        catch (Exception ex)
        {
            _logger.Error($"OnboardingForm: Failed to initialize dependencies: {ex.Message}");
            ShowError("System initialization error. Please restart the application.");
        }
    }

    private void InitializeUIReferences()
    {
        try
        {
            // Personal Information
            _firstNameLineEdit = GetNode<LineEdit>("%FirstNameLineEdit");
            _lastNameLineEdit = GetNode<LineEdit>("%LastNameLineEdit");
            _emailContainer = GetNode<MarginContainer>("%EmailContainer");
            _emailLineEdit = GetNode<LineEdit>("%EmailLineEdit");
            _phoneContainer = GetNode<MarginContainer>("%PhoneContainer");
            _phoneLineEdit = GetNode<LineEdit>("%PhoneLineEdit");

            // Business Information
            _businessNameLineEdit = GetNode<LineEdit>("%BusinessNameLineEdit");
            _businessTypeOptionButton = GetNode<OptionButton>("%BusinessTypeOptionButton");
            _useThisDeviceCheckBox = GetNode<CheckBox>("%UseThisDeviceCheckBox");

            // Terminal Registration
            _locationNameContainer = GetNode<MarginContainer>("%LocationNameContainer");
            _locationNameLineEdit = GetNode<LineEdit>("%LocationNameLineEdit");
            _cityContainer = GetNode<MarginContainer>("%CityContainer");
            _cityLineEdit = GetNode<LineEdit>("%CityLineEdit");
            _addressContainer = GetNode<MarginContainer>("%AddressContainer");
            _addressLineEdit = GetNode<LineEdit>("%AddressLineEdit");
            _postalCodeContainer = GetNode<MarginContainer>("%PostalCodeContainer");
            _postalCodeLineEdit = GetNode<LineEdit>("%PostalCodeLineEdit");

            // UI State
            _statusContainer = GetNode<MarginContainer>("%StatusContainer");
            _statusLabel = GetNode<Label>("%StatusLabel");
            _submitButton = GetNode<Button>("%SubmitButton");
            _progressContainer = GetNode<MarginContainer>("%ProgressContainer");
            _progressBar = GetNode<ProgressBar>("%ProgressBar");

            _logger.Debug("OnboardingForm: UI references initialized");
        }
        catch (Exception ex)
        {
            _logger.Error($"OnboardingForm: Failed to initialize UI references: {ex.Message}");
            // We can't call ShowError here as UI elements might not be available
            OS.Alert("UI initialization error. Please restart the application.", "Error");
            throw; // This will prevent further execution
        }
    }

    private void ConnectSignals()
    {
        _useThisDeviceCheckBox.Toggled += OnUseThisDeviceToggled;
        _submitButton.Pressed += OnSubmitButtonPressed;
        _logger.Debug("OnboardingForm: Signals connected");
    }

    private void SetupInitialState()
    {
        PopulateBusinessTypeOptions();

        // Pre-fill fields with existing user data
        PreFillUserData();

        // Initialize terminal fields visibility
        UpdateLocationFieldsVisibility(_useThisDeviceCheckBox.ButtonPressed);

        // Reset UI state
        ShowStatus("");
        _progressBar.Value = 0;
        _progressContainer.Visible = false;
        _isProcessing = false;

        _logger.Debug("OnboardingForm: Initial state setup completed");
    }

    private void PreFillUserData()
    {
        var user = _authManager.CurrentUser;
        if (user != null)
        {
            // Pre-fill email if available
            if (!string.IsNullOrEmpty(user.Email))
            {
                _emailLineEdit.Text = user.Email;
                _emailLineEdit.Editable = false;
            }

            // Pre-fill phone if available
            if (!string.IsNullOrEmpty(user.Phone))
            {
                _phoneLineEdit.Text = user.Phone;
                _phoneLineEdit.Editable = false;
            }

            _logger.Debug("OnboardingForm: Pre-filled form with user data");
        }
    }

    private async void OnSubmitButtonPressed()
    {
        if (_isProcessing)
        {
            _logger.Debug("OnboardingForm: Submit already in progress, ignoring duplicate request");
            return;
        }

        try
        {
            // Validate input before proceeding
            if (!ValidateInput())
            {
                return;
            }

            _isProcessing = true;
            _submitButton.Disabled = true;
            _progressContainer.Visible = true;
            _currentStep = 0;

            ShowStatus("Starting onboarding process...");
            _logger.Info("OnboardingForm: Starting onboarding process");

            // Create organization
            await CreateOrganization();

            // Register staff member
            await RegisterStaffOwner();

            // Register terminal if selected
            if (_useThisDeviceCheckBox.ButtonPressed)
            {
                await RegisterLocation();
            }

            // Complete
            _progressBar.Value = 100;
            ShowStatus("Onboarding complete! Redirecting...");
            _logger.Info("OnboardingForm: Onboarding completed successfully");

            // Wait a moment to show completion before navigating
            await ToSignal(GetTree().CreateTimer(1.5f), "timeout");
            GoToNextScreen();
        }
        catch (Exception ex)
        {
            _logger.Error($"OnboardingForm: Onboarding process failed: {ex.Message}");
            ShowError($"Onboarding failed: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            _submitButton.Disabled = false;
        }
    }

    private async Task CreateOrganization()
    {
        try
        {
            UpdateProgress("Creating organization...");

            var businessType = (BusinessType)_businessTypeOptionButton.Selected;
            string organizationId = await _organizationManager.CreateOrganizationAsync(
                _businessNameLineEdit.Text.Trim(),
                businessType,
                _emailLineEdit.Text.Trim()
            );

            if (string.IsNullOrEmpty(organizationId))
            {
                throw new Exception("Failed to create organization - received empty ID");
            }

            _logger.Info($"OnboardingForm: Organization created with ID: {organizationId}");
            UpdateProgress("Organization created successfully");
        }
        catch (Exception ex)
        {
            _logger.Error($"OnboardingForm: Failed to create organization: {ex.Message}");
            throw new Exception("Could not create organization", ex);
        }
    }

    private async Task RegisterStaffOwner()
    {
        try
        {
            UpdateProgress("Registering staff member...");

            string organizationId = _organizationManager.GetOrganizationId();
            string staffId = await _organizationManager.RegisterStaffOwnerAsync(
                organizationId,
                _firstNameLineEdit.Text.Trim(),
                _lastNameLineEdit.Text.Trim(),
                _emailLineEdit.Text.Trim()
            );

            if (string.IsNullOrEmpty(staffId))
            {
                throw new Exception("Failed to register staff - received empty ID");
            }

            _logger.Info($"OnboardingForm: Staff owner registered with ID: {staffId}");
            UpdateProgress("Staff registered successfully");
        }
        catch (Exception ex)
        {
            _logger.Error($"OnboardingForm: Failed to register staff owner: {ex.Message}");
            throw new Exception("Could not register staff member", ex);
        }
    }

    private async Task RegisterLocation()
    {
        try
        {
            // Create address first
            UpdateProgress("Creating location address...");

            string addressId = await _addressManager.CreateAddressAsync(
                "RO", // Hard-coded country code
                _cityLineEdit.Text.Trim(),
                _addressLineEdit.Text.Trim(),
                "", // Street address 2 left empty
                _postalCodeLineEdit.Text.Trim()
            );

            if (string.IsNullOrEmpty(addressId))
            {
                throw new Exception("Failed to create address - received empty ID");
            }

            _logger.Info($"OnboardingForm: Address created with ID: {addressId}");

            // Now create location with the address
            UpdateProgress("Creating business location...");

            string organizationId = _organizationManager.GetOrganizationId();
            string locationId = await _organizationManager.CreateLocationAsync(
                organizationId,
                _locationNameLineEdit.Text.Trim(),
                addressId,
                _emailLineEdit.Text.Trim(),
                _phoneLineEdit.Text.Trim()
            );

            if (string.IsNullOrEmpty(locationId))
            {
                throw new Exception("Failed to create location - received empty ID");
            }

            _logger.Info($"OnboardingForm: Location created with ID: {locationId}");

            // Finally register the terminal
            UpdateProgress("Registering this device as terminal...");

            // Use appropriate terminal type - this was missing in the original code

            Terminal terminal = await _terminalManager.CreateTerminalAsync(
                organizationId,
                locationId,
                _locationNameLineEdit.Text.Trim()
            );

            if (terminal == null)
            {
                throw new Exception("Failed to create terminal - received null response");
            }

            _logger.Info($"OnboardingForm: Terminal registered with ID: {terminal.Id}");
            UpdateProgress("Terminal registered successfully");
        }
        catch (Exception ex)
        {
            _logger.Error($"OnboardingForm: Failed to register terminal: {ex.Message}");
            throw new Exception("Could not register terminal", ex);
        }
    }

    private void OnUseThisDeviceToggled(bool toggled)
    {
        UpdateLocationFieldsVisibility(toggled);
    }

    private void UpdateLocationFieldsVisibility(bool visible)
    {
        // Check if the User already has email or phone to know which fields to show
        bool hasEmail = !string.IsNullOrEmpty(_authManager.CurrentUser?.Email);
        bool hasPhone = !string.IsNullOrEmpty(_authManager.CurrentUser?.Phone);

        // Show/hide email container based on authentication status
        _emailContainer.Visible = !hasEmail;

        // Show/hide phone container based on authentication status
        _phoneContainer.Visible = !hasPhone;

        // Update visibility of location-related fields based on checkbox state
        _locationNameContainer.Visible = visible;
        _cityContainer.Visible = visible;
        _addressContainer.Visible = visible;
        _postalCodeContainer.Visible = visible;

        _logger.Debug($"OnboardingForm: Location fields visibility set to {visible}");
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

    private bool ValidateInput()
    {
        // Validate personal information
        if (string.IsNullOrWhiteSpace(_firstNameLineEdit.Text))
        {
            ShowError("First name is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_lastNameLineEdit.Text))
        {
            ShowError("Last name is required");
            return false;
        }

        if (_emailContainer.Visible && (string.IsNullOrWhiteSpace(_emailLineEdit.Text) || !_emailLineEdit.Text.Contains("@")))
        {
            ShowError("A valid email address is required");
            return false;
        }

        if (_phoneContainer.Visible && string.IsNullOrWhiteSpace(_phoneLineEdit.Text))
        {
            ShowError("Phone number is required");
            return false;
        }

        // Validate business information
        if (string.IsNullOrWhiteSpace(_businessNameLineEdit.Text))
        {
            ShowError("Business name is required");
            return false;
        }

        if (_businessTypeOptionButton.Selected < 0)
        {
            ShowError("Please select a business type");
            return false;
        }

        // Validate location information if registering a terminal
        if (_useThisDeviceCheckBox.ButtonPressed)
        {
            if (string.IsNullOrWhiteSpace(_locationNameLineEdit.Text))
            {
                ShowError("Location name is required");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_cityLineEdit.Text))
            {
                ShowError("City is required");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_addressLineEdit.Text))
            {
                ShowError("Address is required");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_postalCodeLineEdit.Text))
            {
                ShowError("Postal code is required");
                return false;
            }
        }

        return true;
    }

    private void UpdateProgress(string message)
    {
        _currentStep++;
        float progressPercentage = (_currentStep / (float)TOTAL_STEPS) * 100f;

        _progressBar.Value = progressPercentage;
        ShowStatus(message);

        _logger.Debug($"OnboardingForm: Progress update: {message} - {progressPercentage}%");
    }

    private void ShowStatus(string message)
    {
        _statusLabel.Text = message;
        _statusLabel.AddThemeColorOverride("font_color", Colors.White);
        _statusContainer.Visible = !string.IsNullOrEmpty(message);
    }

    private void ShowError(string errorMessage)
    {
        _statusLabel.Text = $"Error: {errorMessage}";
        _statusLabel.AddThemeColorOverride("font_color", Colors.Red);
        _statusContainer.Visible = true;

        _logger.Warn($"OnboardingForm: Validation error: {errorMessage}");
    }

    private void GoToNextScreen()
    {
        try
        {
            // Mark user as not new anymore - this was missing in the original code
            _authManager.SetUserAsExisting();

            // Navigate to the appropriate screen
            if (_useThisDeviceCheckBox.ButtonPressed)
            {
                GetTree().ChangeSceneToFile("res://Scenes/POSTerminal/POSMain.tscn");
            }
            else
            {
                GetTree().ChangeSceneToFile("res://Scenes/Home.tscn");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"OnboardingForm: Failed to navigate to next screen: {ex.Message}");
            ShowError("Failed to complete onboarding. Please restart the application.");
        }
    }
}
