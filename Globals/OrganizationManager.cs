using Godot;
using System;
using System.Threading.Tasks;
using ProjectTerminal.Resources;
using Supabase.Postgrest.Responses;
using Supabase.Postgrest;
using Supabase.Gotrue;
public partial class OrganizationManager : Node
{
    private const string ORGANIZATION_ID = "organization_id";
    private Node _logger;
    private SupabaseClient _supabaseClient;
    private AuthManager _authManager;
    private SecureStorage _secureStorage;

    public User CurrentUser => _authManager.CurrentUser;

    [Signal]
    public delegate void OrganizationCreatedEventHandler(string organizationId);

    [Signal]
    public delegate void OrganizationUpdatedEventHandler(string organizationId);

    [Signal]
    public delegate void StaffMemberAddedEventHandler(string staffId, string organizationId);

    [Signal]
    public delegate void LocationCreatedEventHandler(string locationId, string organizationId);

    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "OrganizationManager: Initializing");

        _secureStorage = GetNode<SecureStorage>("/root/SecureStorage");
        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");
        _authManager = GetNode<AuthManager>("/root/AuthManager");
    }

    public async Task<string> CreateOrganizationAsync(string name, BusinessType businessType, string email)
    {
        try
        {
            if (!_authManager.IsLoggedIn())
            {
                _logger.Call("error", "OrganizationManager: Cannot create organization - user not logged in");
                throw new InvalidOperationException("User must be logged in to create an organization");
            }

            _logger.Call("debug", $"OrganizationManager: Creating new organization '{name}'");

            var organization = new Organization
            {
                Name = name.Trim(),
                BusinessType = businessType.ToString().ToLower(),
                Phone = _authManager.CurrentUser.Phone,
                Email = email.Trim(),
                Status = OrganizationStatus.Pending_Review.ToString().ToLower(),
                CreatedBy = _authManager.CurrentUser.Id,
                IsActive = true
            };

            ModeledResponse<Organization> response = await _supabaseClient.From<Organization>()
                .Insert(organization, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

            if (response == null || response.ResponseMessage?.IsSuccessStatusCode != true)
            {
                throw new Exception("Failed to create organization record");
            }

            string organizationId = response.Model?.Id;

            if (string.IsNullOrEmpty(organizationId))
            {
                _logger.Call("error", "OrganizationManager: Organization ID is null or empty after insert");
                throw new Exception("Organization ID is null or empty after creation");
            }

            _logger.Call("info", $"OrganizationManager: Organization '{name}' created with ID: {organizationId}");

            _secureStorage.StoreValue(ORGANIZATION_ID, organizationId);
            _logger.Call("info", $"OrganizationManager: Organization ID '{organizationId}' stored in secure storage");

            EmitSignal(SignalName.OrganizationCreated, organizationId);
            return organizationId;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"OrganizationManager: Failed to create organization: {ex.Message}");
            throw new Exception($"Failed to create organization: {ex.Message}", ex);
        }
    }

    public async Task<string> RegisterStaffOwnerAsync(string organizationId, string firstName, string lastName, string email)
    {
        try
        {
            if (string.IsNullOrEmpty(organizationId))
            {
                _logger.Call("error", "OrganizationManager: Cannot register staff with null/empty organization ID");
                throw new Exception("Invalid organization ID");
            }

            _logger.Call("debug", $"OrganizationManager: Registering user as owner for organization {organizationId}");

            // Get phone from current user
            string phone = _authManager.CurrentUser?.Phone ?? "";

            // Create staff record
            var staff = new Staff
            {
                UserId = _authManager.CurrentUser.Id,
                OrganizationId = organizationId,
                Role = StaffRole.Owner.ToString().ToLower(),
                JobTitle = "Owner",
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Email = email.Trim(),
                Phone = phone,
                IsActive = true
            };

            // Insert into database
            ModeledResponse<Staff> response = await _supabaseClient.From<Staff>()
                .Insert(staff, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

            if (response == null || response.ResponseMessage?.IsSuccessStatusCode != true)
            {
                _logger.Call("error", $"OrganizationManager: Staff insert failed: {response?.ResponseMessage?.ReasonPhrase}");
                throw new Exception("Failed to create staff record");
            }

            string staffId = response.Model?.Id;
            _logger.Call("info", $"OrganizationManager: Staff owner registered with ID: {staffId}");

            EmitSignal(SignalName.StaffMemberAdded, staffId, organizationId);
            return staffId;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"OrganizationManager: Failed to register staff owner: {ex.Message}");
            throw new Exception($"Failed to register staff owner: {ex.Message}", ex);
        }
    }

    public async Task<string> CreateLocationAsync(string organizationId, string name, string addressId, string email, string phone)
    {
        try
        {
            if (string.IsNullOrEmpty(organizationId))
            {
                _logger.Call("error", "OrganizationManager: Cannot create location with null/empty organization ID");
                throw new Exception("Invalid organization ID");
            }

            _logger.Call("debug", $"OrganizationManager: Creating location '{name}' for organization {organizationId}");

            var location = new Location
            {
                OrganizationId = organizationId,
                Name = name,
                Phone = phone,
                Email = email,
                AddressId = addressId,
                BusinessHours = "{}",
                IsActive = false
            };

            ModeledResponse<Location> response = await _supabaseClient.From<Location>()
                .Insert(location, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

            if (response == null || response.ResponseMessage?.IsSuccessStatusCode != true)
            {
                _logger.Call("error", $"OrganizationManager: Failed to create location: {response?.ResponseMessage?.ReasonPhrase}");
                throw new Exception("Failed to create location");
            }

            string locationId = response.Model?.Id;
            _logger.Call("info", $"OrganizationManager: Location '{name}' created with ID: {locationId}");

            EmitSignal(SignalName.LocationCreated, locationId, organizationId);
            return locationId;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"OrganizationManager: Failed to create location: {ex.Message}");
            throw;
        }
    }

    public string GetOrganizationId()
    {
        string organizationId = _secureStorage.RetrieveValue<string>(ORGANIZATION_ID);
        if (string.IsNullOrEmpty(organizationId))
        {
            _logger.Call("error", "OrganizationManager: Organization ID is null or empty");
            throw new Exception("Organization ID is null or empty");
        }

        return organizationId;
    }
}
