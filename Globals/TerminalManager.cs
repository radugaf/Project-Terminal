using Godot;
using System;
using System.Threading.Tasks;
using ProjectTerminal.Resources;
using Supabase.Postgrest.Responses;
/// <summary>
/// Manages terminal identity, registration, and location-specific operations.
/// Handles terminal registration, terminal identity persistence, and related operations.
/// </summary>
public partial class TerminalManager : Node
{
    #region Constants and Fields

    /// <summary>
    /// Key used for storing terminal identity information.
    /// </summary>
    private const string TERMINAL_IDENTITY_KEY = "terminal_identity";

    /// <summary>
    /// Current terminal identity information.
    /// </summary>
    private Terminal _terminalIdentity;

    /// <summary>
    /// Reference to the application logger.
    /// </summary>
    private Node _logger;

    /// <summary>
    /// Reference to the secure storage manager.
    /// </summary>
    private SecureStorage _secureStorage;

    /// <summary>
    /// Reference to the Supabase client.
    /// </summary>
    private SupabaseClient _supabaseClient;

    /// <summary>
    /// Reference to the authentication manager.
    /// </summary>
    private AuthManager _authManager;

    /// <summary>
    ///  Reference to the device manager
    /// </summary>
    private Node _deviceManager;


    #endregion

    #region Properties

    /// <summary>
    /// Provides read-only access to the terminal identity information.
    /// </summary>
    public Terminal TerminalInfo => _terminalIdentity;

    /// <summary>
    /// Indicates whether this terminal has been registered to a location.
    /// </summary>
    public bool IsTerminalRegistered => _terminalIdentity != null && !string.IsNullOrEmpty(_terminalIdentity.Id);

    #endregion

    #region Signals

    /// <summary>
    /// Emitted when the terminal identity changes (registration, update).
    /// </summary>
    [Signal]
    public delegate void TerminalIdentityChangedEventHandler();

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the TerminalManager and loads existing terminal identity.
    /// </summary>
    public override void _Ready()
    {
        // Get a reference to the logger
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "TerminalManager: Initializing TerminalManager...");

        // Get required dependencies
        _secureStorage = GetNode<SecureStorage>("/root/SecureStorage");
        _deviceManager = GetNode<Node>("/root/DeviceManager");

        // Load terminal identity
        LoadTerminalIdentity();

        // Defer getting references to other managers to avoid circular dependency issues
        CallDeferred(nameof(SetupDependencies));
    }

    /// <summary>
    /// Sets up references to other manager nodes after all nodes are ready.
    /// </summary>
    private void SetupDependencies()
    {
        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");
        _authManager = GetNode<AuthManager>("/root/AuthManager");
    }

    /// <summary>
    /// Sends a heartbeat to update the terminal's last active timestamp.
    /// </summary>
    private async void SendHeartbeat()
    {
        if (IsTerminalRegistered)
        {
            await UpdateTerminalHeartbeatAsync();
        }
    }

    #endregion

    #region Terminal Identity Methods

    /// <summary>
    /// Registers this terminal to a specific location and organization.
    /// This should be called during initial setup of the terminal, typically by an owner or manager.
    /// </summary>
    public async Task RegisterTerminalAsync(string organizationId, string locationId, string terminalName, TerminalType terminalType)
    {
        if (!_authManager.IsStaffLoggedIn())
        {
            _logger.Call("error", "TerminalManager: Cannot register terminal: No authenticated user");
            throw new InvalidOperationException("Staff authentication required to register terminal");
        }

        // Verify current user has permission to register terminals (Owner or Manager)
        if (_authManager.CurrentUserRole == StaffRole.Staff)
        {
            _logger.Call("error", "TerminalManager: Cannot register terminal: Insufficient permissions");
            throw new InvalidOperationException("Only Owners and Managers can register terminals");
        }

        _logger.Call("info", $"TerminalManager: Registering terminal to organization {organizationId}, location {locationId}");

        try
        {

            // Create a terminal record in the database
            ModeledResponse<Terminal> response = await _supabaseClient.From<Terminal>().Insert(new Terminal
            {
                OrganizationId = organizationId,
                LocationId = locationId,
                TerminalName = terminalName,
                TerminalType = terminalType.ToString().ToLower(),
                Active = true,
                RegisteredBy = _authManager.CurrentUser.Id
            });

            // Verify the insert was successful
            if (response.ResponseMessage?.IsSuccessStatusCode != true)
            {
                throw new Exception("Failed to register terminal in database");
            }

            // Create and save the terminal identity locally
            _terminalIdentity = new Terminal
            {
                OrganizationId = organizationId,
                LocationId = locationId,
                TerminalName = terminalName,
                TerminalType = terminalType.ToString().ToLower(),
                UpdatedAt = DateTime.UtcNow
            };

            SaveTerminalIdentity();

            _logger.Call("info", $"TerminalManager: Terminal registered successfully with Name: {terminalName}, Type: {terminalType}");
            EmitSignal(SignalName.TerminalIdentityChanged);
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"TerminalManager: Failed to register terminal: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Updates the terminal heartbeat to indicate it's still active.
    /// Should be called periodically while the terminal is in use.
    /// </summary>
    public async Task UpdateTerminalHeartbeatAsync()
    {
        if (!IsTerminalRegistered)
        {
            _logger.Call("warn", "TerminalManager: Cannot update heartbeat: Terminal not registered");
            return;
        }

        try
        {
            await _supabaseClient.From<Terminal>()
                .Where(t => t.Id == _terminalIdentity.Id)
                .Set(la => la.LastActive, DateTime.UtcNow)
                .Update();

            _logger.Call("debug", "TerminalManager: Terminal heartbeat updated");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"TerminalManager: Failed to update terminal heartbeat: {ex.Message}");
            // Non-critical error, just log it
        }
    }

    /// <summary>
    /// Gets information about the organization this terminal belongs to.
    /// </summary>
    /// <returns>Organization data if available</returns>
    public async Task<dynamic> GetOrganizationInfoAsync()
    {
        if (!IsTerminalRegistered)
        {
            _logger.Call("warn", "TerminalManager: Cannot get organization info: Terminal not registered");
            return null;
        }

        try
        {
            var response = await _supabaseClient.From<Organization>()
                .Where(o => o.Id == _terminalIdentity.OrganizationId)
                .Single();

            return response;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"TerminalManager: Failed to get organization info: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets information about the location this terminal belongs to.
    /// </summary>
    /// <returns>Location data if available</returns>
    public async Task<dynamic> GetLocationInfoAsync()
    {
        if (!IsTerminalRegistered)
        {
            _logger.Call("warn", "TerminalManager: Cannot get location info: Terminal not registered");
            return null;
        }

        try
        {
            var response = await _supabaseClient.From<Location>()
                .Where(l => l.OrganizationId == _terminalIdentity.OrganizationId)
                .Single();

            return response;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"TerminalManager: Failed to get location info: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Unregisters this terminal, removing its association with a location.
    /// This is typically done when moving a terminal to a new location or decommissioning it.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task UnregisterTerminalAsync()
    {
        if (!IsTerminalRegistered)
        {
            _logger.Call("warn", "TerminalManager: Cannot unregister: Terminal not registered");
            return;
        }

        if (!_authManager.IsStaffLoggedIn())
        {
            _logger.Call("error", "TerminalManager: Cannot unregister terminal: No authenticated user");
            throw new InvalidOperationException("Staff authentication required to unregister terminal");
        }

        // Verify current user has permission to unregister terminals (Owner or Manager)
        if (_authManager.CurrentUserRole == StaffRole.Staff)
        {
            _logger.Call("error", "TerminalManager: Cannot unregister terminal: Insufficient permissions");
            throw new InvalidOperationException("Only Owners and Managers can unregister terminals");
        }

        _logger.Call("info", $"TerminalManager: Unregistering terminal {_terminalIdentity.Id}");

        try
        {
            // Mark the terminal as inactive in the database
            await _supabaseClient.From<Terminal>()
                .Where(t => t.Id == _terminalIdentity.Id)
                .Set(t => t.Active, false)
                .Set(t => t.UpdatedAt, DateTime.UtcNow)
                .Update();

            // Clear local terminal identity
            _terminalIdentity = null;
            ClearTerminalIdentity();

            _logger.Call("info", "TerminalManager: Terminal unregistered successfully");
            EmitSignal(SignalName.TerminalIdentityChanged);
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"TerminalManager: Failed to unregister terminal: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Terminal Identity Storage Methods

    /// <summary>
    /// Saves the terminal identity information securely.
    /// </summary>
    private void SaveTerminalIdentity()
    {
        if (_terminalIdentity == null)
        {
            ClearTerminalIdentity();
            return;
        }

        try
        {
            // Update the last updated timestamp
            _terminalIdentity.UpdatedAt = DateTime.UtcNow;

            // Store the terminal identity
            _secureStorage.StoreObject(TERMINAL_IDENTITY_KEY, _terminalIdentity);

            _logger.Call("debug", "TerminalManager: Terminal identity saved securely");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"TerminalManager: Failed to save terminal identity: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the terminal identity from secure storage.
    /// </summary>
    private void LoadTerminalIdentity()
    {
        _logger.Call("debug", "TerminalManager: Attempting to load terminal identity from secure storage");

        try
        {
            Terminal identityFromStorage = _secureStorage.RetrieveObject<Terminal>(TERMINAL_IDENTITY_KEY);

            if (identityFromStorage == null || string.IsNullOrEmpty(identityFromStorage.Id))
            {
                _logger.Call("debug", "TerminalManager: No valid terminal identity found in storage");
                return;
            }

            _terminalIdentity = identityFromStorage;
            _logger.Call("info", $"TerminalManager: Terminal identity loaded successfully. Terminal ID: {_terminalIdentity.Id}, Location: {_terminalIdentity.LocationId}");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"TerminalManager: Error loading terminal identity: {ex.Message}");
            ClearTerminalIdentity();
        }
    }

    /// <summary>
    /// Clears terminal identity data from secure storage.
    /// </summary>
    private void ClearTerminalIdentity()
    {
        _logger.Call("debug", "TerminalManager: Clearing terminal identity data");

        try
        {
            _secureStorage.ClearValue(TERMINAL_IDENTITY_KEY);
            _logger.Call("debug", "TerminalManager: Terminal identity data cleared");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"TerminalManager: Failed to clear terminal identity data: {ex.Message}");
        }
    }

    #endregion

    #region Diagnostics

    /// <summary>
    /// Runs a diagnostic check on the terminal identity system.
    /// Returns a detailed report of the terminal state.
    /// </summary>
    /// <returns>A diagnostic report string</returns>
    public string RunTerminalDiagnostics()
    {
        var diagnostics = new System.Text.StringBuilder();

        diagnostics.AppendLine("=== TERMINAL MANAGER DIAGNOSTICS ===");
        diagnostics.AppendLine($"Current Time (UTC): {DateTime.UtcNow}");

        try
        {
            // Check terminal identity
            diagnostics.AppendLine($"Has Terminal Identity: {IsTerminalRegistered}");

            if (IsTerminalRegistered)
            {
                diagnostics.AppendLine($"Terminal ID: {_terminalIdentity.Id}");
                diagnostics.AppendLine($"Organization ID: {_terminalIdentity.OrganizationId}");
                diagnostics.AppendLine($"Location ID: {_terminalIdentity.LocationId}");
                diagnostics.AppendLine($"Terminal Name: {_terminalIdentity.TerminalName}");
                diagnostics.AppendLine($"Terminal Type: {_terminalIdentity.TerminalType}");
                diagnostics.AppendLine($"Updated At: {_terminalIdentity.UpdatedAt}");
            }

            // Check secure storage
            bool hasStoredIdentity = _secureStorage.HasKey(TERMINAL_IDENTITY_KEY);
            diagnostics.AppendLine($"Has Stored Terminal Identity: {hasStoredIdentity}");

            // System information
            diagnostics.AppendLine($"Device Name: {OS.GetName()}");
            diagnostics.AppendLine($"Device Model: {OS.GetModelName()}");
            diagnostics.AppendLine($"Unique Device ID: {OS.GetUniqueId()}");
        }
        catch (Exception ex)
        {
            diagnostics.AppendLine($"DIAGNOSTIC ERROR: {ex.Message}");
        }

        diagnostics.AppendLine("=== TERMINAL DIAGNOSTICS COMPLETE ===");

        return diagnostics.ToString();
    }

    #endregion
}
