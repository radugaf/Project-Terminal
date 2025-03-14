using Godot;
using System;
using System.Threading.Tasks;
using Supabase.Gotrue;
using ProjectTerminal.Resources;

/// <summary>
/// Manages terminal authentication state, session persistence, and POS terminal identity.
/// Acts as the central facade for authentication and identity services.
/// This class maintains the same API as the original TerminalSessionManager but delegates
/// responsibilities to specialized manager classes.
/// </summary>
public partial class TerminalSessionManager : Node
{
    #region Fields

    /// <summary>
    /// Reference to the application logger.
    /// </summary>
    private Node _logger;

    /// <summary>
    /// Reference to the authentication manager.
    /// </summary>
    private AuthManager _authManager;

    /// <summary>
    /// Reference to the terminal manager.
    /// </summary>
    private TerminalManager _terminalManager;

    /// <summary>
    /// Reference to the Supabase client.
    /// </summary>
    private SupabaseClient _supabaseClient;

    #endregion

    #region Properties

    /// <summary>
    /// Provides read-only access to the current user session object.
    /// </summary>
    public Session CurrentSession => _authManager.CurrentSession;

    /// <summary>
    /// Provides quick reference to the currently authenticated user.
    /// Returns null if no user is logged in.
    /// </summary>
    public User CurrentUser => _authManager.CurrentUser;

    /// <summary>
    /// Provides read-only access to the terminal identity information.
    /// </summary>
    public Terminal TerminalInfo => _terminalManager.TerminalInfo;

    /// <summary>
    /// Indicates whether this terminal has been registered to a location.
    /// </summary>
    public bool IsTerminalRegistered => _terminalManager.IsTerminalRegistered;

    /// <summary>
    /// Gets the current staff role based on user metadata or claims.
    /// </summary>
    public StaffRole CurrentUserRole => _authManager.CurrentUserRole;

    #endregion

    #region Signals

    /// <summary>
    /// Emitted whenever a user session change occurs (login, logout, refresh).
    /// </summary>
    [Signal]
    public delegate void SessionChangedEventHandler();

    /// <summary>
    /// Emitted when the terminal identity changes (registration, update).
    /// </summary>
    [Signal]
    public delegate void TerminalIdentityChangedEventHandler();

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the TerminalSessionManager, connects signals from individual managers.
    /// </summary>
    public override void _Ready()
    {
        // Get a reference to the logger
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "TerminalSessionManager: Initializing TerminalSessionManager...");

        // Get references to managers
        _authManager = GetNode<AuthManager>("/root/AuthManager");
        _terminalManager = GetNode<TerminalManager>("/root/TerminalManager");
        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");

        // Connect signals from managers to relay them through this facade
        _authManager.Connect(AuthManager.SignalName.SessionChanged, new Callable(this, nameof(OnSessionChanged)));
        _terminalManager.Connect(TerminalManager.SignalName.TerminalIdentityChanged, new Callable(this, nameof(OnTerminalIdentityChanged)));

        // Initialize Supabase client
        _supabaseClient.ClientInitialized += OnClientInitialized;
        _supabaseClient.ClientInitializationFailed += OnClientInitializationFailed;

        // Call this as deferred to ensure all nodes are fully ready
        CallDeferred(nameof(InitializeSupabase));
    }

    /// <summary>
    /// Initializes the Supabase client.
    /// </summary>
    private async void InitializeSupabase()
    {
        try
        {
            await _supabaseClient.InitializeClientAsync();
        }
        catch (Exception ex)
        {
            _logger.Call("critical", $"TerminalSessionManager: Failed to initialize Supabase: {ex.Message}");
        }
    }

    /// <summary>
    /// Called when the Supabase client is successfully initialized.
    /// </summary>
    private void OnClientInitialized()
    {
        _logger.Call("info", "TerminalSessionManager: Supabase client initialized successfully");
    }

    /// <summary>
    /// Called when the Supabase client initialization fails.
    /// </summary>
    private void OnClientInitializationFailed(string errorMessage)
    {
        _logger.Call("critical", $"TerminalSessionManager: Supabase client initialization failed: {errorMessage}");
    }

    /// <summary>
    /// Called when the authentication session changes.
    /// </summary>
    private void OnSessionChanged()
    {
        // Relay the signal
        EmitSignal(SignalName.SessionChanged);
    }

    /// <summary>
    /// Called when the terminal identity changes.
    /// </summary>
    private void OnTerminalIdentityChanged()
    {
        // Relay the signal
        EmitSignal(SignalName.TerminalIdentityChanged);
    }

    #endregion

    #region Authentication Methods (Delegated to AuthManager)

    /// <summary>
    /// Requests a one-time password sent via SMS to the specified phone number.
    /// Used for staff login to the terminal.
    /// </summary>
    /// <param name="phoneNumber">Phone number in E.164 format (e.g., +1234567890)</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public Task RequestStaffLoginOtpAsync(string phoneNumber)
    {
        return _authManager.RequestStaffLoginOtpAsync(phoneNumber);
    }

    /// <summary>
    /// Verifies an OTP code and establishes a staff user session if valid.
    /// </summary>
    /// <param name="phoneNumber">Phone number that received the OTP</param>
    /// <param name="otpCode">The OTP code to verify</param>
    /// <returns>The established session if successful, null otherwise</returns>
    public Task<Session> VerifyStaffLoginOtpAsync(string phoneNumber, string otpCode)
    {
        return _authManager.VerifyStaffLoginOtpAsync(phoneNumber, otpCode);
    }

    /// <summary>
    /// Refreshes the current session token.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public Task RefreshSessionAsync()
    {
        return _authManager.RefreshSessionAsync();
    }

    /// <summary>
    /// Logs out the current user and clears session data.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public Task LogoutAsync()
    {
        return _authManager.LogoutAsync();
    }

    /// <summary>
    /// Checks if a staff user is currently logged in with a valid session.
    /// </summary>
    /// <returns>True if logged in, false otherwise</returns>
    public bool IsStaffLoggedIn()
    {
        return _authManager.IsStaffLoggedIn();
    }

    #endregion

    #region Terminal Identity Methods (Delegated to TerminalManager)

    /// <summary>
    /// Registers this terminal to a specific location and organization.
    /// This should be called during initial setup of the terminal, typically by an owner or manager.
    /// </summary>
    /// <param name="organizationId">The organization ID to register with</param>
    /// <param name="locationId">The location ID to register with</param>
    /// <param name="terminalName">Human-readable name for this terminal</param>
    /// <param name="terminalType">Type of terminal (e.g., "Checkout", "Kitchen")</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public Task RegisterTerminalAsync(string organizationId, string locationId, string terminalName, TerminalType terminalType)
    {
        return _terminalManager.RegisterTerminalAsync(organizationId, locationId, terminalName, terminalType);
    }

    /// <summary>
    /// Updates the terminal heartbeat to indicate it's still active.
    /// Should be called periodically while the terminal is in use.
    /// </summary>
    public Task UpdateTerminalHeartbeatAsync()
    {
        return _terminalManager.UpdateTerminalHeartbeatAsync();
    }

    /// <summary>
    /// Gets information about the organization this terminal belongs to.
    /// </summary>
    /// <returns>Organization data if available</returns>
    public Task<dynamic> GetOrganizationInfoAsync()
    {
        return _terminalManager.GetOrganizationInfoAsync();
    }

    /// <summary>
    /// Gets information about the location this terminal belongs to.
    /// </summary>
    /// <returns>Location data if available</returns>
    public Task<dynamic> GetLocationInfoAsync()
    {
        return _terminalManager.GetLocationInfoAsync();
    }

    /// <summary>
    /// Unregisters this terminal, removing its association with a location.
    /// This is typically done when moving a terminal to a new location or decommissioning it.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public Task UnregisterTerminalAsync()
    {
        return _terminalManager.UnregisterTerminalAsync();
    }

    #endregion

    #region Permission Methods (Delegated to AuthManager)

    /// <summary>
    /// Checks if the current user has permission to perform a specific action at this terminal's location.
    /// </summary>
    /// <param name="permission">The permission to check</param>
    /// <returns>True if the user has the permission, false otherwise</returns>
    public Task<bool> HasPermissionAsync(string permission)
    {
        return _authManager.HasPermissionAsync(permission);
    }

    /// <summary>
    /// Checks if the current user is authorized to access this location.
    /// </summary>
    /// <returns>True if authorized, false otherwise</returns>
    public Task<bool> IsAuthorizedForLocationAsync()
    {
        return _authManager.IsAuthorizedForLocationAsync();
    }

    #endregion

}
