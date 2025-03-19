using Godot;
using System.Threading.Tasks;
using Supabase.Gotrue;
using ProjectTerminal.Resources;
using Supabase.Postgrest.Responses;
using Supabase.Postgrest;

public partial class RegisterThisTerminal : Control
{
    private Node _logger;

    private TerminalSessionManager _terminalSessionManager;
    private SupabaseClient _supabaseClient;


    private LineEdit _locationNameLineEdit;
    private LineEdit _countryLineEdit;
    private LineEdit _cityLineEdit;
    private LineEdit _streetOneLineEdit;
    private LineEdit _streetTwoLineEdit;
    private Label _statusLabel;
    private Button _submitButton;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "RegisterThisTerminal: RegisterThisTerminal scene initializing");

        // Initialize UI elements
        _locationNameLineEdit = GetNode<LineEdit>("%LocationNameLineEdit");
        _countryLineEdit = GetNode<LineEdit>("%CountryLineEdit");
        _cityLineEdit = GetNode<LineEdit>("%CityLineEdit");
        _streetOneLineEdit = GetNode<LineEdit>("%StreetOneLineEdit");
        _streetTwoLineEdit = GetNode<LineEdit>("%StreetTwoLineEdit");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _submitButton = GetNode<Button>("%SubmitButton");

        _submitButton.Pressed += OnSubmitButtonPressed;
    }

    private async void OnSubmitButtonPressed()
    {
        _logger.Call("info", "BrandNewUser: Submit button pressed, starting registration flow");

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
            _logger.Call("error", "BrandNewUser: Cannot retrieve current user");
            _submitButton.Disabled = false;
            return;
        }

        // 1. Add the new Address to the database
        await CreateAddress(currentUser.Id);
    }

    private void UpdateStatusLabel(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
            _logger.Call("debug", $"BrandNewUser: Status updated - {message}");
        }
    }

    private bool ValidateInputs()
    {
        // Check LocationName
        if (string.IsNullOrWhiteSpace(_locationNameLineEdit.Text))
        {
            UpdateStatusLabel("Error: Location name is required");
            _logger.Call("warn", "BrandNewUser: Missing location name");
            return false;
        }

        // Check Country
        if (string.IsNullOrWhiteSpace(_countryLineEdit.Text))
        {
            UpdateStatusLabel("Error: Country is required");
            _logger.Call("warn", "BrandNewUser: Missing country");
            return false;
        }

        // Check City
        if (string.IsNullOrWhiteSpace(_cityLineEdit.Text))
        {
            UpdateStatusLabel("Error: City is required");
            _logger.Call("warn", "BrandNewUser: Missing city");
            return false;
        }

        // Check Street One
        if (string.IsNullOrWhiteSpace(_streetOneLineEdit.Text))
        {
            UpdateStatusLabel("Error: Street address is required");
            _logger.Call("warn", "BrandNewUser: Missing street address");
            return false;
        }

        // Check Street Two
        if (string.IsNullOrWhiteSpace(_streetTwoLineEdit.Text))
        {
            UpdateStatusLabel("Error: Street address is required");
            _logger.Call("warn", "BrandNewUser: Missing street address");
            return false;
        }
        return true;
    }

    private async Task CreateAddress(string userId)
    {

    }

}
