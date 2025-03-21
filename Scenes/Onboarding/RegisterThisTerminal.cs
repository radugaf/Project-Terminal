using Godot;
using System;
using System.Threading.Tasks;
using Supabase.Gotrue;
using ProjectTerminal.Resources;
using Supabase.Postgrest.Responses;
using Supabase.Postgrest;

public partial class RegisterThisTerminal : Control
{
    private Node _logger;
    private Node _deviceManager;

    private TerminalSessionManager _terminalSessionManager;
    private SupabaseClient _supabaseClient;

    private LineEdit _locationNameLineEdit;
    private LineEdit _countryLineEdit;
    private LineEdit _cityLineEdit;
    private LineEdit _streetOneLineEdit;
    private LineEdit _streetTwoLineEdit;
    private LineEdit _postalCodeLineEdit;
    private Label _statusLabel;
    private Button _submitButton;

    // State tracking
    private string _addressId;
    private string _locationId;
    private Terminal _newterminal;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "RegisterThisTerminal: RegisterThisTerminal scene initializing");

        // Get references to managers
        _terminalSessionManager = GetNode<TerminalSessionManager>("/root/TerminalSessionManager");
        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");
        _deviceManager = GetNode<Node>("/root/DeviceManager");

        // Initialize UI elements
        _locationNameLineEdit = GetNode<LineEdit>("%LocationNameLineEdit");
        _countryLineEdit = GetNode<LineEdit>("%CountryLineEdit");
        _cityLineEdit = GetNode<LineEdit>("%CityLineEdit");
        _streetOneLineEdit = GetNode<LineEdit>("%StreetOneLineEdit");
        _streetTwoLineEdit = GetNode<LineEdit>("%StreetTwoLineEdit");
        _postalCodeLineEdit = GetNode<LineEdit>("%PostalCodeLineEdit");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _submitButton = GetNode<Button>("%SubmitButton");

        _submitButton.Pressed += OnSubmitButtonPressed;
    }

    private async void OnSubmitButtonPressed()
    {
        _logger.Call("info", "RegisterThisTerminal: Submit button pressed, starting registration flow");

        // Disable submit button to prevent multiple submissions
        _submitButton.Disabled = true;
        UpdateStatusLabel("Processing...");

        // Validate input fields
        if (!ValidateInputs())
        {
            _submitButton.Disabled = false;
            return;
        }

        User currentUser = _terminalSessionManager.CurrentUser;
        if (currentUser == null)
        {
            UpdateStatusLabel("Error: Cannot retrieve current user");
            _logger.Call("error", "RegisterThisTerminal: Cannot retrieve current user");
            _submitButton.Disabled = false;
            return;
        }

        // 1. Add the new Address to the database
        _addressId = await CreateAddress();

        // 2. Create a new Location for the Terminal
        _locationId = await CreateLocation(_terminalSessionManager.OrgId, _addressId);

        // 3. Create a new Terminal
        _newterminal = await CreateTerminal(_terminalSessionManager.OrgId, _locationId, currentUser.Id);

        // 4. Mark the terminal as registered
        _terminalSessionManager.SetTerminal(_newterminal);

        _logger.Call("info", "RegisterThisTerminal: Terminal registered successfully");
        UpdateStatusLabel("Terminal registered successfully!");

        GoToNextScreen();
    }

    private void UpdateStatusLabel(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
            _logger.Call("debug", $"RegisterThisTerminal: Status updated - {message}");
        }
    }

    private bool ValidateInputs()
    {
        // Check LocationName
        if (string.IsNullOrWhiteSpace(_locationNameLineEdit.Text))
        {
            UpdateStatusLabel("Error: Location name is required");
            _logger.Call("warn", "RegisterThisTerminal: Missing location name");
            return false;
        }

        // Check Country
        if (string.IsNullOrWhiteSpace(_countryLineEdit.Text))
        {
            UpdateStatusLabel("Error: Country is required");
            _logger.Call("warn", "RegisterThisTerminal: Missing country");
            return false;
        }

        // Check City
        if (string.IsNullOrWhiteSpace(_cityLineEdit.Text))
        {
            UpdateStatusLabel("Error: City is required");
            _logger.Call("warn", "RegisterThisTerminal: Missing city");
            return false;
        }

        // Check Street One
        if (string.IsNullOrWhiteSpace(_streetOneLineEdit.Text))
        {
            UpdateStatusLabel("Error: Street address is required");
            _logger.Call("warn", "RegisterThisTerminal: Missing street address");
            return false;
        }

        // Check Street Two
        if (string.IsNullOrWhiteSpace(_streetTwoLineEdit.Text))
        {
            UpdateStatusLabel("Error: Street address is required");
            _logger.Call("warn", "RegisterThisTerminal: Missing street address");
            return false;
        }

        // Check Postal Code
        if (string.IsNullOrWhiteSpace(_postalCodeLineEdit.Text))
        {
            UpdateStatusLabel("Error: Postal code is required");
            _logger.Call("warn", "RegisterThisTerminal: Missing postal code");
            return false;
        }

        return true;
    }

    private async Task<string> CreateAddress()
    {

        var address = new Address
        {
            Country = _countryLineEdit.Text,
            City = _cityLineEdit.Text,
            StreetAddress1 = _streetOneLineEdit.Text,
            StreetAddress2 = _streetTwoLineEdit.Text,
            PostalCode = _postalCodeLineEdit.Text,
            IsVerified = false,
        };

        ModeledResponse<Address> response = await _supabaseClient.From<Address>()
            .Insert(address, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

        if (response == null || response.ResponseMessage.IsSuccessStatusCode != true)
        {
            _logger.Call("error", $"RegisterThisTerminal: Failed to create address - {response.ResponseMessage.ReasonPhrase}");
            throw new Exception($"Failed to create address: {response.ResponseMessage.ReasonPhrase}");
        }

        string addressId = response.Model?.Id;
        _logger.Call("info", $"RegisterThisTerminal: Address created with ID: {addressId}");

        return addressId;
    }

    private async Task<string> CreateLocation(string orgId, string addressId)
    {
        var location = new Location
        {
            OrganizationId = orgId,
            Name = _locationNameLineEdit.Text,
            Phone = _terminalSessionManager.CurrentUser.Phone,
            Email = _terminalSessionManager.CurrentUser.Email,
            AddressId = addressId,
            IsActive = false,
        };

        ModeledResponse<Location> response = await _supabaseClient.From<Location>()
            .Insert(location, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

        if (response == null || response.ResponseMessage.IsSuccessStatusCode != true)
        {
            _logger.Call("error", $"RegisterThisTerminal: Failed to create location - {response.ResponseMessage.ReasonPhrase}");
            throw new Exception($"Failed to create location: {response.ResponseMessage.ReasonPhrase}");
        }

        string locationId = response.Model?.Id;
        _logger.Call("info", $"RegisterThisTerminal: Location created with ID: {locationId}");
        return locationId;
    }

    private async Task<Terminal> CreateTerminal(string orgId, string locationId, string userId)
    {
        var terminal = new Terminal
        {
            OrganizationId = orgId,
            LocationId = locationId,
            TerminalName = "",
            TerminalType = "",
            DeviceId = "",
            Active = true,
            RegisteredBy = userId,
            DeviceName = "",
            DeviceModel = "",
            DeviceOs = "",
            DeviceOsVersion = "",
            ProcessorType = "",
            IpAddress = "",
            ScreenDpi = "",
            ScreenOrientation = "",
            IsTouchscreen = false,
            ScreenScale = "",
        };

        ModeledResponse<Terminal> response = await _supabaseClient.From<Terminal>()
            .Insert(terminal, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

        if (response == null || response.ResponseMessage.IsSuccessStatusCode != true)
        {
            _logger.Call("error", $"RegisterThisTerminal: Failed to create terminal - {response.ResponseMessage.ReasonPhrase}");
            throw new Exception($"Failed to create terminal: {response.ResponseMessage.ReasonPhrase}");
        }

        string terminalId = response.Model?.Id;
        _logger.Call("info", $"RegisterThisTerminal: Terminal created with ID: {terminalId}");

        return response.Model;
    }

    private void GoToNextScreen()
    {
        GetTree().CreateTimer(2.0f).Timeout += () => GetTree().ChangeSceneToFile("res://Scenes/Home.tscn");
    }
}
