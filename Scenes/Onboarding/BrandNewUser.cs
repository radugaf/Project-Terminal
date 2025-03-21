using Godot;
using System;
using System.Threading.Tasks;
using Supabase.Gotrue;
using ProjectTerminal.Resources;
using Supabase.Postgrest.Responses;
using Supabase.Postgrest;

public partial class BrandNewUser : Control
{
    private Node _logger;
    private TerminalSessionManager _terminalSessionManager;
    private SupabaseClient _supabaseClient;
    private AuthManager _authManager;

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
    private string _createdOrganizationId;
    private bool _registerAsTerminal = false;

    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "BrandNewUser: Brand New User scene initializing");

        // Get references to managers
        _terminalSessionManager = GetNode<TerminalSessionManager>("/root/TerminalSessionManager");
        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");
        _authManager = GetNode<AuthManager>("/root/AuthManager");

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
        _logger.Call("debug", "BrandNewUser: Device will be registered as a terminal");
    }

    private void OnDeviceNoButtonPressed()
    {
        _registerAsTerminal = false;
        _deviceYesButton.ButtonPressed = false;
        _deviceNoButton.ButtonPressed = true;
        _logger.Call("debug", "BrandNewUser: Device will not be registered as a terminal");
    }

    private async void OnSubmitButtonPressed()
    {
        try
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

            // Get current user
            User currentUser = _terminalSessionManager.CurrentUser;
            if (currentUser == null)
            {
                UpdateStatusLabel("Error: Cannot retrieve current user");
                _logger.Call("error", "BrandNewUser: Cannot retrieve current user");
                _submitButton.Disabled = false;
                return;
            }

            // 1. Update user email
            await UpdateUserEmail(currentUser.Id);

            // 2. Create organization
            _createdOrganizationId = await CreateOrganization(currentUser.Id);

            // 4. Register staff as owner
            await RegisterStaffOwner(_createdOrganizationId, currentUser.Id);

            // 5. Set the new organization ID in the session manager
            _terminalSessionManager.SetOrgId(_createdOrganizationId);
            _logger.Call("info", $"BrandNewUser: Organization ID set in session manager: {_createdOrganizationId}");

            UpdateStatusLabel("Registration complete!");
            _logger.Call("info", "BrandNewUser: Registration completed successfully");

            _authManager.UserNotNewAnyMore();
            _logger.Call("info", "BrandNewUser: User is no longer new");

            GoToNextScreen();
        }
        catch (Exception ex)
        {
            UpdateStatusLabel($"Error: {ex.Message}");
            _logger.Call("error", $"BrandNewUser: Registration failed: {ex.Message}", new Godot.Collections.Dictionary { { "stack_trace", ex.StackTrace } });
            _submitButton.Disabled = false;
        }
    }

    private bool ValidateInputs()
    {
        // Check firstName
        if (string.IsNullOrWhiteSpace(_firstNameLineEdit.Text))
        {
            UpdateStatusLabel("Error: First name is required");
            _logger.Call("warn", "BrandNewUser: Missing first name");
            return false;
        }

        // Check lastName
        if (string.IsNullOrWhiteSpace(_lastNameLineEdit.Text))
        {
            UpdateStatusLabel("Error: Last name is required");
            _logger.Call("warn", "BrandNewUser: Missing last name");
            return false;
        }

        // Check email
        if (string.IsNullOrWhiteSpace(_emailLineEdit.Text) || !_emailLineEdit.Text.Contains("@"))
        {
            UpdateStatusLabel("Error: Valid email is required");
            _logger.Call("warn", "BrandNewUser: Invalid email");
            return false;
        }

        // Check businessName
        if (string.IsNullOrWhiteSpace(_businessNameLineEdit.Text))
        {
            UpdateStatusLabel("Error: Business name is required");
            _logger.Call("warn", "BrandNewUser: Missing business name");
            return false;
        }

        // Check business type selection
        if (_businessTypeOptionButton.Selected == -1)
        {
            UpdateStatusLabel("Error: Please select a business type");
            _logger.Call("warn", "BrandNewUser: No business type selected");
            return false;
        }

        // Check terminal selection
        if (!_deviceYesButton.ButtonPressed && !_deviceNoButton.ButtonPressed)
        {
            UpdateStatusLabel("Error: Please select whether to use this device as a terminal");
            _logger.Call("warn", "BrandNewUser: Terminal selection not made");
            return false;
        }

        return true;
    }

    private async Task UpdateUserEmail(string userId)
    {
        try
        {
            _logger.Call("debug", $"BrandNewUser: Updating user email for user {userId}");

            // Create update attributes
            var attrs = new UserAttributes { Email = _emailLineEdit.Text.Trim() };

            // Update user email
            User response = await _supabaseClient.Auth.Update(attrs) ?? throw new Exception("Failed to update user email");

            _logger.Call("info", $"BrandNewUser: Email updated successfully for user {userId}");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"BrandNewUser: Failed to update user email: {ex.Message}");
            throw new Exception($"Failed to update email: {ex.Message}", ex);
        }
    }

    private async Task<string> CreateOrganization(string userId)
    {
        try
        {
            _logger.Call("debug", "BrandNewUser: Creating new organization");

            // Get business type from option button
            var businessType = (BusinessType)_businessTypeOptionButton.Selected;
            _logger.Call("debug", $"BrandNewUser: Selected business type: {businessType}");

            // Create organization record
            var organization = new Organization
            {
                Name = _businessNameLineEdit.Text.Trim(),
                BusinessType = businessType.ToString().ToLower(),
                Phone = GetUserPhoneNumber(),  // Get from current authenticated user
                Email = _emailLineEdit.Text.Trim(),
                Status = OrganizationStatus.Pending_Review.ToString().ToLower(),
                CreatedBy = userId,
                IsActive = true
            };

            // Insert into database
            ModeledResponse<Organization> response = await _supabaseClient.From<Organization>().Insert(organization, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

            if (response == null || response.ResponseMessage?.IsSuccessStatusCode != true)
            {
                throw new Exception("Failed to create organization record");
            }

            string organizationId = response.Model?.Id;
            _logger.Call("info", $"BrandNewUser: Organization created with ID: {organizationId}");

            // Extra logging to verify ID
            if (string.IsNullOrEmpty(organizationId))
            {
                _logger.Call("error", "BrandNewUser: Organization ID is null or empty after insert");
                throw new Exception("Organization ID is null or empty after creation");
            }

            return organizationId;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"BrandNewUser: Failed to create organization: {ex.Message}");
            throw new Exception($"Failed to create organization: {ex.Message}", ex);
        }
    }

    private async Task RegisterStaffOwner(string organizationId, string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(organizationId))
            {
                _logger.Call("error", "BrandNewUser: Cannot register staff with null/empty organization ID");
                throw new Exception("Invalid organization ID");
            }

            _logger.Call("debug", $"BrandNewUser: Registering user as staff owner for organization {organizationId}");

            // Create staff record
            var staff = new Staff
            {
                UserId = userId,
                OrganizationId = organizationId,
                Role = StaffRole.Owner.ToString().ToLower(),
                JobTitle = "Owner",
                FirstName = _firstNameLineEdit.Text.Trim(),
                LastName = _lastNameLineEdit.Text.Trim(),
                Email = _emailLineEdit.Text.Trim(),
                Phone = GetUserPhoneNumber(),
                IsActive = true
            };

            // Insert into database
            ModeledResponse<Staff> response = await _supabaseClient.From<Staff>().Insert(staff, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

            if (response == null || response.ResponseMessage?.IsSuccessStatusCode != true)
            {
                _logger.Call("error", $"BrandNewUser: Staff insert response error: {response?.ResponseMessage?.ReasonPhrase}");
                throw new Exception("Failed to create staff record");
            }

            string staffId = response.Model?.Id;
            _logger.Call("info", $"BrandNewUser: Staff owner registered with ID: {staffId}");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"BrandNewUser: Failed to register staff owner: {ex.Message}");
            throw new Exception($"Failed to register as staff owner: {ex.Message}", ex);
        }
    }

    private string GetUserPhoneNumber()
    {
        // Extract phone number from current user's metadata
        try
        {
            User user = _terminalSessionManager.CurrentUser;

            // Try to get phone from user object
            if (!string.IsNullOrEmpty(user.Phone))
            {
                return user.Phone;
            }

            // Fallback: check if phone exists in user metadata
            if (user.UserMetadata != null && user.UserMetadata.TryGetValue("phone", out object value))
            {
                return value.ToString();
            }

            _logger.Call("warn", "BrandNewUser: Could not retrieve user phone number");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"BrandNewUser: Error getting user phone number: {ex.Message}");
            return string.Empty;
        }
    }

    private void UpdateStatusLabel(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
            _logger.Call("debug", $"BrandNewUser: Status updated - {message}");
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
                GetTree().CreateTimer(2.0f).Timeout += () => GetTree().ChangeSceneToFile("res://Scenes/Home.tscn");
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"BrandNewUser: Exception in GoToNextScreen: {ex.Message}");
        }
    }
}
