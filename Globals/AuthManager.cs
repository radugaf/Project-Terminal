using Godot;
using System;
using System.Threading.Tasks;
using Supabase.Gotrue;
using static Supabase.Gotrue.Constants;
using ProjectTerminal.Resources;

/// <summary>
/// Manages authentication state, user sessions, and permission checking.
/// Handles staff authentication, session persistence, and authorization.
/// </summary>
public partial class AuthManager : Node
{
    #region Constants and Fields

    /// <summary>
    /// Key used for storing user session data.
    /// </summary>
    private const string USER_SESSION_KEY = "current_user_session";

    /// <summary>
    /// Key used for storing session expiry timestamp.
    /// </summary>
    private const string SESSION_EXPIRY_KEY = "session_expiry_timestamp";

    /// <summary>
    /// Time in seconds before token expiry when we should attempt to refresh it.
    /// </summary>
    private const int REFRESH_THRESHOLD_SECONDS = 300; // 5 minutes

    /// <summary>
    /// Current active session containing access tokens and user information.
    /// </summary>
    private Session _currentSession;

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
    /// Reference to the terminal manager.
    /// </summary>
    private TerminalManager _terminalManager;

    #endregion

    #region Properties

    /// <summary>
    /// Provides read-only access to the current user session object.
    /// </summary>
    public Session CurrentSession => _currentSession;

    /// <summary>
    /// Provides quick reference to the currently authenticated user.
    /// Returns null if no user is logged in.
    /// </summary>
    public User CurrentUser => _currentSession?.User;

    /// <summary>
    /// Gets the current staff role based on user metadata or claims.
    /// </summary>
    public StaffRole CurrentUserRole
    {
        get
        {
            if (_currentSession?.User == null)
                return StaffRole.Staff; // Default to lowest privilege

            // Check user claims or metadata for role information
            // NOTE: Adjust this according to how roles are stored in your Supabase setup
            if (_currentSession.User.AppMetadata.TryGetValue("role", out object roleObj))
            {
                string role = roleObj.ToString().ToLower();

                if (role.Contains("owner"))
                    return StaffRole.Owner;
                else if (role.Contains("manager"))
                    return StaffRole.Manager;
            }

            return StaffRole.Staff;
        }
    }

    #endregion

    #region Signals

    /// <summary>
    /// Emitted whenever a user session change occurs (login, logout, refresh).
    /// </summary>
    [Signal]
    public delegate void SessionChangedEventHandler();

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the AuthManager, sets up dependencies, and loads any existing session.
    /// </summary>
    public override void _Ready()
    {
        // Get a reference to the logger
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "AuthManager: Initializing AuthManager...");

        // Get required dependencies
        _secureStorage = GetNode<SecureStorage>("/root/SecureStorage");
        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");
        _terminalManager = GetNode<TerminalManager>("/root/TerminalManager");

        // Attempt to load saved user session
        LoadUserSession();

        // Schedule periodic session validation
        var timer = new Timer();
        AddChild(timer);
        timer.WaitTime = 300; // Check every 5 minutes
        timer.Timeout += ValidateSessionHealth;
        timer.Start();
    }

    /// <summary>
    /// Validates that the current session is still valid and refreshes if needed.
    /// </summary>
    private async void ValidateSessionHealth()
    {
        if (_currentSession == null)
            return;

        try
        {
            // Check if token is expired or about to expire (within threshold)
            DateTime? expiresAt = GetSessionExpiryTime();

            if (expiresAt.HasValue)
            {
                var now = DateTime.UtcNow;
                var timeUntilExpiry = expiresAt.Value - now;

                if (timeUntilExpiry.TotalSeconds < REFRESH_THRESHOLD_SECONDS)
                {
                    _logger.Call("info", "AuthManager: Token expiring soon, refreshing");
                    await RefreshSessionAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Error validating session: {ex.Message}");
        }
    }

    #endregion

    #region Authentication Methods

    /// <summary>
    /// Requests a one-time password sent via SMS to the specified phone number.
    /// Used for staff login to the terminal.
    /// </summary>
    /// <param name="phoneNumber">Phone number in E.164 format (e.g., +1234567890)</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="ArgumentException">Thrown if phone number is invalid</exception>
    /// <exception cref="Exception">Thrown if OTP request fails</exception>
    public async Task RequestStaffLoginOtpAsync(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || !phoneNumber.StartsWith("+"))
        {
            _logger.Call("error", "AuthManager: Invalid phone number format for OTP request");
            throw new ArgumentException("Phone number must be in E.164 format (e.g., +1234567890)");
        }

        _logger.Call("info", $"AuthManager: Requesting staff login OTP for {phoneNumber}");

        try
        {
            await _supabaseClient.Auth.SignIn(SignInType.Phone, phoneNumber);
            _logger.Call("debug", $"AuthManager: OTP requested successfully for {phoneNumber}");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: OTP request failed: {ex.Message}", new Godot.Collections.Dictionary { { "phone", phoneNumber } });
            throw;
        }
    }

    /// <summary>
    /// Verifies an OTP code and establishes a staff user session if valid.
    /// </summary>
    /// <param name="phoneNumber">Phone number that received the OTP</param>
    /// <param name="otpCode">The OTP code to verify</param>
    /// <returns>The established session if successful, null otherwise</returns>
    /// <exception cref="Exception">Thrown if verification fails</exception>
    public async Task<Session> VerifyStaffLoginOtpAsync(string phoneNumber, string otpCode)
    {
        _logger.Call("info", "AuthManager: Verifying staff login OTP");

        try
        {
            Session session = await _supabaseClient.Auth.VerifyOTP(phoneNumber, otpCode, MobileOtpType.SMS);

            bool validSession = session != null && session.User != null;
            if (!validSession)
            {
                _logger.Call("warn", "AuthManager: OTP verification did not return a valid session");
                return null;
            }

            // Verify this user is associated with the terminal's organization
            if (!await VerifyUserOrganizationAccessAsync(session.User.Id))
            {
                _logger.Call("warn", $"AuthManager: User {session.User.Id} is not authorized for this terminal's organization");
                return null;
            }

            _currentSession = session;
            SaveUserSession(session);

            _logger.Call("info", $"AuthManager: Staff login successful. User: {_currentSession.User.Id}, Role: {CurrentUserRole}");
            EmitSignal(SignalName.SessionChanged);
            return session;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: OTP verification failed: {ex.Message}", new Godot.Collections.Dictionary { { "phone", phoneNumber } });
            throw;
        }
    }

    /// <summary>
    /// Verifies that a user has access to the organization this terminal belongs to.
    /// </summary>
    /// <param name="userId">The user ID to check</param>
    /// <returns>True if the user has access, false otherwise</returns>
    private async Task<bool> VerifyUserOrganizationAccessAsync(string userId)
    {
        // Skip check if terminal is not registered
        if (!_terminalManager.IsTerminalRegistered)
            return true;

        try
        {
            // Query the database to check if user has access to this organization
            var response = await _supabaseClient.From<Staff>()
                .Where(s => s.UserId == userId && s.OrganizationId == _terminalManager.TerminalInfo.OrganizationId)
                .Get();

            // Check if we found a result
            return response.Models.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Failed to verify user organization access: {ex.Message}");
            // In case of error, deny access by default for security
            return false;
        }
    }

    /// <summary>
    /// Refreshes the current session token.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task RefreshSessionAsync()
    {
        if (_currentSession == null || string.IsNullOrEmpty(_currentSession.RefreshToken))
        {
            _logger.Call("warn", "AuthManager: Cannot refresh session: No refresh token available");
            return;
        }

        try
        {
            _logger.Call("debug", "AuthManager: Refreshing session token");

            var refreshedSession = await _supabaseClient.Auth.RefreshSession();

            if (refreshedSession != null)
            {
                _currentSession = refreshedSession;
                SaveUserSession(refreshedSession);
                _logger.Call("debug", "AuthManager: Session refreshed successfully");
                EmitSignal(SignalName.SessionChanged);
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Failed to refresh session: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Logs out the current user and clears session data.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task LogoutAsync()
    {
        _logger.Call("info", "AuthManager: Logging out staff user");

        try
        {
            bool hasActiveSession = _supabaseClient.Auth?.CurrentSession != null;
            if (hasActiveSession)
            {
                await _supabaseClient.Auth.SignOut();
                _logger.Call("debug", "AuthManager: Logout performed on Supabase side");
            }
        }
        catch (Exception ex)
        {
            _logger.Call("warn", $"AuthManager: Server sign-out reported an error: {ex.Message}");
            // Continue with local logout regardless of server result
        }

        _currentSession = null;
        ClearUserSession();

        // Reinitialize the client with no tokens
        await _supabaseClient.ReinitializeClientAsync();
        _logger.Call("debug", "AuthManager: Client re-initialized without tokens");

        EmitSignal(SignalName.SessionChanged);
    }

    /// <summary>
    /// Checks if a staff user is currently logged in with a valid session.
    /// </summary>
    /// <returns>True if logged in, false otherwise</returns>
    public bool IsStaffLoggedIn()
    {
        if (_currentSession == null || _currentSession.User == null)
            return false;

        DateTime? expiryTime = GetSessionExpiryTime();
        if (expiryTime.HasValue && DateTime.UtcNow > expiryTime.Value)
        {
            _logger.Call("debug", "AuthManager: Session token is expired");
            return false;
        }

        return true;
    }

    #endregion

    #region Permission Methods

    /// <summary>
    /// Checks if the current user has permission to perform a specific action at this terminal's location.
    /// </summary>
    /// <param name="permission">The permission to check</param>
    /// <returns>True if the user has the permission, false otherwise</returns>
    public async Task<bool> HasPermissionAsync(string permission)
    {
        if (!IsStaffLoggedIn())
            return false;

        // Owner has all permissions
        if (CurrentUserRole == StaffRole.Owner)
            return true;

        // Get the terminal location
        var locationId = _terminalManager.TerminalInfo?.LocationId;
        if (string.IsNullOrEmpty(locationId))
            return false;

        try
        {
            // Query the permissions table to check if this user has this permission at this location
            var response = await _supabaseClient.From<StaffPermission>()
                .Where(sp => sp.UserId == CurrentUser.Id)
                .Where(sp => sp.Permission == permission)
                .Where(sp => sp.LocationId == locationId)
                .Get();

            // Check if we found a result
            return response.Models.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Failed to check permission: {ex.Message}");
            // In case of error, deny permission by default for security
            return false;
        }
    }

    /// <summary>
    /// Checks if the current user is authorized to access this location.
    /// </summary>
    /// <returns>True if authorized, false otherwise</returns>
    public async Task<bool> IsAuthorizedForLocationAsync()
    {
        if (!IsStaffLoggedIn() || !_terminalManager.IsTerminalRegistered)
            return false;

        // Owner has access to all locations in their organization
        if (CurrentUserRole == StaffRole.Owner)
            return true;

        // Get the terminal location
        var locationId = _terminalManager.TerminalInfo?.LocationId;
        if (string.IsNullOrEmpty(locationId))
            return false;

        try
        {
            // Check if this user is assigned to this location
            var response = await _supabaseClient.From<StaffLocation>()
                .Where(q => q.LocationId == locationId)
                .Get();

            // Check if we found a result
            return response.Models.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Failed to check location authorization: {ex.Message}");
            // In case of error, deny access by default for security
            return false;
        }
    }

    #endregion

    #region Session Storage Methods

    /// <summary>
    /// Returns the calculated expiry time for the current session.
    /// </summary>
    /// <returns>The UTC datetime when the session expires, or null if not available</returns>
    private DateTime? GetSessionExpiryTime()
    {
        if (_currentSession == null)
            return null;

        // Try to get from stored timestamp first
        if (_secureStorage.HasKey(SESSION_EXPIRY_KEY))
        {
            string storedTimestamp = _secureStorage.RetrieveValue<string>(SESSION_EXPIRY_KEY);
            if (DateTime.TryParse(storedTimestamp, out DateTime timestamp))
            {
                return timestamp;
            }
        }

        // Fall back to calculating from session
        if (_currentSession.ExpiresIn > 0)
        {
            return _currentSession.CreatedAt.AddSeconds(_currentSession.ExpiresIn);
        }

        return null;
    }

    /// <summary>
    /// Saves the user session securely.
    /// </summary>
    /// <param name="session">The session to save</param>
    private void SaveUserSession(Session session)
    {
        if (session == null)
        {
            ClearUserSession();
            return;
        }

        try
        {
            // Store the session object
            _secureStorage.StoreObject(USER_SESSION_KEY, session);

            // Store session expiry timestamp for quick checking
            if (session.ExpiresIn > 0)
            {
                DateTime expiryTime = session.CreatedAt.AddSeconds(session.ExpiresIn);
                _secureStorage.StoreValue(SESSION_EXPIRY_KEY, expiryTime.ToString("o"));
            }

            _logger.Call("debug", "AuthManager: User session saved securely");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Failed to save user session: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the user session from secure storage.
    /// </summary>
    private void LoadUserSession()
    {
        _logger.Call("debug", "AuthManager: Attempting to load user session from secure storage");

        try
        {
            Session sessionFromStorage = _secureStorage.RetrieveObject<Session>(USER_SESSION_KEY);

            if (sessionFromStorage == null || string.IsNullOrEmpty(sessionFromStorage.AccessToken))
            {
                _logger.Call("debug", "AuthManager: No valid user session found in storage");
                return;
            }

            // Check if the session is expired
            if (_secureStorage.HasKey(SESSION_EXPIRY_KEY))
            {
                string expiryTimestamp = _secureStorage.RetrieveValue<string>(SESSION_EXPIRY_KEY);

                if (DateTime.TryParse(expiryTimestamp, out DateTime expiryTime) &&
                    DateTime.UtcNow > expiryTime)
                {
                    _logger.Call("info", "AuthManager: Loaded user session is expired, will attempt to refresh");
                    // We still load the session to allow for refresh
                }
            }

            _currentSession = sessionFromStorage;
            _logger.Call("info", "AuthManager: User session loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Error loading user session: {ex.Message}");
            ClearUserSession();
        }
    }

    /// <summary>
    /// Clears all user session data from secure storage.
    /// </summary>
    private void ClearUserSession()
    {
        _logger.Call("debug", "AuthManager: Clearing user session data");

        try
        {
            _secureStorage.ClearValue(USER_SESSION_KEY);
            _secureStorage.ClearValue(SESSION_EXPIRY_KEY);
            _logger.Call("debug", "AuthManager: User session data cleared");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Failed to clear user session data: {ex.Message}");
        }
    }

    #endregion

    #region Diagnostics

    /// <summary>
    /// Runs a diagnostic check on the authentication system.
    /// Returns a detailed report of the auth state.
    /// </summary>
    /// <returns>A diagnostic report string</returns>
    public string RunAuthDiagnostics()
    {
        var diagnostics = new System.Text.StringBuilder();

        diagnostics.AppendLine("=== AUTH MANAGER DIAGNOSTICS ===");
        diagnostics.AppendLine($"Current Time (UTC): {DateTime.UtcNow}");

        try
        {
            // Check user session
            bool hasUserSession = _currentSession != null;
            diagnostics.AppendLine($"Has User Session: {hasUserSession}");

            if (hasUserSession)
            {
                diagnostics.AppendLine($"User ID: {_currentSession.User?.Id ?? "N/A"}");
                diagnostics.AppendLine($"User Role: {CurrentUserRole}");
                diagnostics.AppendLine($"Has Access Token: {!string.IsNullOrEmpty(_currentSession.AccessToken)}");
                diagnostics.AppendLine($"Has Refresh Token: {!string.IsNullOrEmpty(_currentSession.RefreshToken)}");
                diagnostics.AppendLine($"Created At: {_currentSession.CreatedAt}");
                diagnostics.AppendLine($"Expires In: {_currentSession.ExpiresIn} seconds");

                DateTime? expiryTime = GetSessionExpiryTime();
                if (expiryTime.HasValue)
                {
                    diagnostics.AppendLine($"Calculated Expiry: {expiryTime.Value}");
                    diagnostics.AppendLine($"Is Expired: {DateTime.UtcNow > expiryTime.Value}");
                    diagnostics.AppendLine($"Time Until Expiry: {expiryTime.Value - DateTime.UtcNow}");
                }
                else
                {
                    diagnostics.AppendLine("Expiry Time: Could not be determined");
                }
            }

            // Check secure storage
            bool hasStoredSession = _secureStorage.HasKey(USER_SESSION_KEY);
            diagnostics.AppendLine($"Has Stored User Session: {hasStoredSession}");

            bool hasStoredExpiry = _secureStorage.HasKey(SESSION_EXPIRY_KEY);
            diagnostics.AppendLine($"Has Stored Expiry: {hasStoredExpiry}");

            // Check login state
            diagnostics.AppendLine($"IsStaffLoggedIn() Reports: {IsStaffLoggedIn()}");
        }
        catch (Exception ex)
        {
            diagnostics.AppendLine($"DIAGNOSTIC ERROR: {ex.Message}");
        }

        diagnostics.AppendLine("=== AUTH DIAGNOSTICS COMPLETE ===");

        return diagnostics.ToString();
    }

    #endregion
}
