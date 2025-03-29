using Godot;
using System;
using ProjectTerminal.Resources;

public partial class RegisterThisTerminal : Control
{
    private Logger _logger;
    private Node _deviceManager;
    private SupabaseClient _supabaseClient;
    private TerminalManager _terminalManager;
    private AddressManager _addressManager;
    private OrganizationManager _organizationManager;

    private LineEdit _locationNameLineEdit;
    private LineEdit _countryLineEdit;
    private LineEdit _cityLineEdit;
    private LineEdit _streetOneLineEdit;
    private LineEdit _streetTwoLineEdit;
    private LineEdit _postalCodeLineEdit;
    private OptionButton _terminalTypeOptionButton;
    private Label _statusLabel;
    private Button _submitButton;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("RegisterThisTerminal: RegisterThisTerminal scene initializing");

        // Get references to managers
        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");
        _deviceManager = GetNode<Node>("/root/DeviceManager");
        _addressManager = GetNode<AddressManager>("/root/AddressManager");
        _organizationManager = GetNode<OrganizationManager>("/root/OrganizationManager");
        _terminalManager = GetNode<TerminalManager>("/root/TerminalManager");

        // Initialize UI elements
        _locationNameLineEdit = GetNode<LineEdit>("%LocationNameLineEdit");
        _countryLineEdit = GetNode<LineEdit>("%CountryLineEdit");
        _cityLineEdit = GetNode<LineEdit>("%CityLineEdit");
        _streetOneLineEdit = GetNode<LineEdit>("%StreetOneLineEdit");
        _streetTwoLineEdit = GetNode<LineEdit>("%StreetTwoLineEdit");
        _postalCodeLineEdit = GetNode<LineEdit>("%PostalCodeLineEdit");
        _terminalTypeOptionButton = GetNode<OptionButton>("%TerminalTypeOptionButton");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _submitButton = GetNode<Button>("%SubmitButton");

        // Populate terminal type options
        PopulateTerminalTypes();

        _submitButton.Pressed += OnSubmitButtonPressed;
    }

    private async void OnSubmitButtonPressed()
    {
        _logger.Info("RegisterThisTerminal: Submit button pressed, starting registration flow");

        _submitButton.Disabled = true;
        UpdateStatusLabel("Processing...");

        if (!ValidateInputs())
        {
            _submitButton.Disabled = false;
            return;
        }

        // 1. Add the new Address to the database
        string addressId = await _addressManager.CreateAddressAsync(
            _countryLineEdit.Text,
            _cityLineEdit.Text,
            _streetOneLineEdit.Text,
            _streetTwoLineEdit.Text,
            _postalCodeLineEdit.Text
        );

        // 2. Create location
        string locationId = await _organizationManager.CreateLocationAsync(
            _organizationManager.GetOrganizationId(),
            _locationNameLineEdit.Text,
            addressId,
            _organizationManager.CurrentUser.Email,
            _organizationManager.CurrentUser.Phone
        );

        // 3. Create terminal
        var terminalType = (TerminalType)_terminalTypeOptionButton.Selected;
        Terminal newTerminal = await _terminalManager.CreateTerminalAsync(
            _organizationManager.GetOrganizationId(),
            locationId,
            $"{_locationNameLineEdit.Text} {terminalType}",
            terminalType
        );

        _logger.Info("RegisterThisTerminal: Terminal registered successfully");
        UpdateStatusLabel("Terminal registered successfully!");

        GoToNextScreen();
    }

    private void UpdateStatusLabel(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
            _logger.Debug($"RegisterThisTerminal: Status updated - {message}");
        }
    }

    private void PopulateTerminalTypes()
    {
        _terminalTypeOptionButton.Clear();

        // Add enum values to dropdown
        foreach (TerminalType type in Enum.GetValues(typeof(TerminalType)))
        {
            _terminalTypeOptionButton.AddItem(type.ToString());
        }

        // Set default selection
        if (_terminalTypeOptionButton.ItemCount > 0)
            _terminalTypeOptionButton.Select(0);
    }

    private bool ValidateInputs()
    {
        // Check LocationName
        if (string.IsNullOrWhiteSpace(_locationNameLineEdit.Text))
        {
            UpdateStatusLabel("Error: Location name is required");
            _logger.Warn("RegisterThisTerminal: Missing location name");
            return false;
        }

        // Check Country
        if (string.IsNullOrWhiteSpace(_countryLineEdit.Text))
        {
            UpdateStatusLabel("Error: Country is required");
            _logger.Warn("RegisterThisTerminal: Missing country");
            return false;
        }

        // Check City
        if (string.IsNullOrWhiteSpace(_cityLineEdit.Text))
        {
            UpdateStatusLabel("Error: City is required");
            _logger.Warn("RegisterThisTerminal: Missing city");
            return false;
        }

        // Check Street One
        if (string.IsNullOrWhiteSpace(_streetOneLineEdit.Text))
        {
            UpdateStatusLabel("Error: Street address is required");
            _logger.Warn("RegisterThisTerminal: Missing street address");
            return false;
        }

        // Check Street Two
        if (string.IsNullOrWhiteSpace(_streetTwoLineEdit.Text))
        {
            UpdateStatusLabel("Error: Street address is required");
            _logger.Warn("RegisterThisTerminal: Missing street address");
            return false;
        }

        // Check Postal Code
        if (string.IsNullOrWhiteSpace(_postalCodeLineEdit.Text))
        {
            UpdateStatusLabel("Error: Postal code is required");
            _logger.Warn("RegisterThisTerminal: Missing postal code");
            return false;
        }

        return true;
    }

    private void GoToNextScreen()
    {
        try
        {
            var tree = GetTree();
            if (tree == null)
            {
                _logger.Error("RegisterThisTerminal: SceneTree is null in GoToNextScreen");
                return;
            }

            tree.CreateTimer(2.0f).Timeout += () =>
            {
                var sceneTree = GetTree();
                if (sceneTree != null)
                {
                    sceneTree.ChangeSceneToFile("res://Scenes/Home.tscn");
                }
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"RegisterThisTerminal: Exception in GoToNextScreen: {ex.Message}");
        }
    }
}
