using Godot;
using System;
using System.Threading.Tasks;
using System.Text.Json;
using Supabase;
using static Supabase.Gotrue.Constants;

/// <summary>
/// Manages user authentication state and persistence for the POS terminal.
/// Handles secure session storage, retrieval, and Supabase authentication operations.
/// Acts as a central authentication service for the entire application.
/// </summary>
public partial class UserSessionManager : Node
{
    #region Constants and Fields

    /// <summary>
    /// Key used for storing session data in the encrypted store.
    /// </summary>
    private const string SESSION_STORE_KEY = "current_user_session";

    /// <summary>
    /// Key used for storing session expiry timestamp.
    /// </summary>
    private const string SESSION_EXPIRY_KEY = "session_expiry_timestamp";

    /// <summary>
    /// Supabase client instance used for all API operations.
    /// </summary>
    private Client _supabase;

    /// <summary>
    /// Current active session containing access tokens and user information.
    /// </summary>
    private Supabase.Gotrue.Session _currentSession;

    /// <summary>
    /// Reference to the application logger.
    /// </summary>
    private Node _logger;

    /// <summary>
    /// JSON serialization options for consistent formatting.
    /// </summary>
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #endregion

    #region Properties

    /// <summary>
    /// Provides read-only access to the current session object.
    /// </summary>
    public Supabase.Gotrue.Session CurrentSession => _currentSession;

    /// <summary>
    /// Provides quick reference to the currently authenticated user.
    /// Returns null if no user is logged in.
    /// </summary>
    public Supabase.Gotrue.User CurrentUser => _currentSession?.User;

    #endregion

    #region Signals

    /// <summary>
    /// Emitted whenever a session change occurs (login, logout, refresh).
    /// </summary>
    [Signal]
    public delegate void SessionChangedEventHandler();

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the UserSessionManager, sets up Supabase client, and loads any existing session.
    /// </summary>
    public override async void _Ready()
    {
        // Get a reference to the logger
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "Initializing UserSessionManager...");

        try
        {
            // Read environment variables
            Node envLoader = GetNode("/root/EnvLoader");
            string supabaseUrl = (string)envLoader.Call("get_env", "SUPABASE_URL");
            string supabaseKey = (string)envLoader.Call("get_env", "SUPABASE_KEY");

            // Validate environment variables
            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
            {
                _logger.Call("critical", "Missing Supabase environment variables");
                throw new InvalidOperationException("Supabase URL or key not found in environment variables");
            }

            // Create Supabase client
            var options = new SupabaseOptions
            {
                AutoConnectRealtime = true,
                AutoRefreshToken = true
            };

            _supabase = new Client(supabaseUrl, supabaseKey, options);
            _logger.Call("debug", "Supabase client created");

            // Attempt to load saved session
            LoadSession();

            // Initialize Supabase client with loaded session
            await _supabase.InitializeAsync();
            _logger.Call("info", "Supabase client initialized");

            // Schedule periodic session validation
            var timer = new Timer();
            AddChild(timer);
            timer.WaitTime = 300; // Check every 5 minutes
            timer.Timeout += ValidateSessionHealth;
            timer.Start();
        }
        catch (Exception ex)
        {
            _logger.Call("critical", $"Failed to initialize UserSessionManager: {ex.Message}");
            throw;
        }
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
            // Check if token is expired or about to expire (within 10 minutes)
            DateTime? expiresAt = GetSessionExpiryTime();

            if (expiresAt.HasValue)
            {
                var now = DateTime.UtcNow;
                var timeUntilExpiry = expiresAt.Value - now;

                if (timeUntilExpiry.TotalMinutes < 10)
                {
                    _logger.Call("info", "Token expiring soon, refreshing");
                    await RefreshSessionAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"Error validating session: {ex.Message}");
        }
    }

    #endregion

    #region Authentication Methods

    /// <summary>
    /// Requests a one-time password sent via SMS to the specified phone number.
    /// </summary>
    /// <param name="phoneNumber">Phone number in E.164 format (e.g., +1234567890)</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="ArgumentException">Thrown if phone number is invalid</exception>
    /// <exception cref="Exception">Thrown if OTP request fails</exception>
    public async Task RequestOtpAsync(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || !phoneNumber.StartsWith("+"))
        {
            _logger.Call("error", "Invalid phone number format for OTP request");
            throw new ArgumentException("Phone number must be in E.164 format (e.g., +1234567890)");
        }

        _logger.Call("info", $"Requesting OTP for {phoneNumber}");

        try
        {
            await _supabase.Auth.SignIn(SignInType.Phone, phoneNumber);
            _logger.Call("debug", $"OTP requested successfully for {phoneNumber}");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"OTP request failed: {ex.Message}", new Godot.Collections.Dictionary { { "phone", phoneNumber } });
            throw;
        }
    }

    /// <summary>
    /// Verifies an OTP code and establishes a user session if valid.
    /// </summary>
    /// <param name="phoneNumber">Phone number that received the OTP</param>
    /// <param name="otpCode">The OTP code to verify</param>
    /// <returns>The established session if successful, null otherwise</returns>
    /// <exception cref="Exception">Thrown if verification fails</exception>
    public async Task<Supabase.Gotrue.Session> VerifyOtpAsync(string phoneNumber, string otpCode)
    {
        _logger.Call("info", "Verifying OTP");

        try
        {
            var session = await _supabase.Auth.VerifyOTP(phoneNumber, otpCode, MobileOtpType.SMS);

            bool validSession = session != null && session.User != null;
            if (validSession)
            {
                _currentSession = session;
                SaveSession(session);

                _logger.Call("info", $"OTP verification successful. User: {_currentSession.User.Id}");
                EmitSignal(SignalName.SessionChanged);
                return session;
            }

            _logger.Call("warn", "OTP verification did not return a valid session");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"OTP verification failed: {ex.Message}", new Godot.Collections.Dictionary { { "phone", phoneNumber } });
            throw;
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
            _logger.Call("warn", "Cannot refresh session: No refresh token available");
            return;
        }

        try
        {
            _logger.Call("debug", "Refreshing session token");

            var refreshedSession = await _supabase.Auth.RefreshSession();

            if (refreshedSession != null)
            {
                _currentSession = refreshedSession;
                SaveSession(refreshedSession);
                _logger.Call("debug", "Session refreshed successfully");
                EmitSignal(SignalName.SessionChanged);
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"Failed to refresh session: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Logs out the current user and clears session data.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task LogoutAsync()
    {
        _logger.Call("info", "Logging out");

        try
        {
            bool hasActiveSession = _supabase?.Auth?.CurrentSession != null;
            if (hasActiveSession)
            {
                await _supabase.Auth.SignOut();
                _logger.Call("debug", "Logout performed on Supabase side");
            }
        }
        catch (Exception ex)
        {
            _logger.Call("warn", $"Server sign-out reported an error: {ex.Message}");
            // Continue with local logout regardless of server result
        }

        _currentSession = null;
        ClearSession();

        // Reinitialize the client with no tokens
        await _supabase.InitializeAsync();
        _logger.Call("debug", "Client re-initialized without tokens");

        EmitSignal(SignalName.SessionChanged);
    }

    /// <summary>
    /// Checks if the user is currently logged in with a valid session.
    /// </summary>
    /// <returns>True if logged in, false otherwise</returns>
    public bool IsLoggedIn()
    {
        if (_currentSession == null || _currentSession.User == null)
            return false;

        DateTime? expiryTime = GetSessionExpiryTime();
        if (expiryTime.HasValue && DateTime.UtcNow > expiryTime.Value)
        {
            _logger.Call("debug", "Session token is expired");
            return false;
        }

        return true;
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
        if (ProjectSettings.HasSetting($"application/config/{SESSION_EXPIRY_KEY}"))
        {
            string storedTimestamp = (string)ProjectSettings.GetSetting($"application/config/{SESSION_EXPIRY_KEY}");
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
    /// Saves the session securely using Godot's encrypted configuration system.
    /// </summary>
    /// <param name="session">The session to save</param>
    private void SaveSession(Supabase.Gotrue.Session session)
    {
        if (session == null)
        {
            ClearSession();
            return;
        }

        try
        {
            // Serialize the session
            string sessionJson = JsonSerializer.Serialize(session, _jsonOptions);

            // Store the encrypted session data
            ProjectSettings.SetSetting($"application/config/{SESSION_STORE_KEY}", sessionJson);

            // Store session expiry timestamp for quick checking
            if (session.ExpiresIn > 0)
            {
                DateTime expiryTime = session.CreatedAt.AddSeconds(session.ExpiresIn);
                ProjectSettings.SetSetting($"application/config/{SESSION_EXPIRY_KEY}", expiryTime.ToString("o"));
            }

            // Save the configuration
            ProjectSettings.Save();

            _logger.Call("debug", "Session saved securely");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"Failed to save session: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the session from secure storage.
    /// </summary>
    private void LoadSession()
    {
        _logger.Call("debug", "Attempting to load session from secure storage");

        try
        {
            if (!ProjectSettings.HasSetting($"application/config/{SESSION_STORE_KEY}"))
            {
                _logger.Call("debug", "No saved session found");
                return;
            }

            string sessionJson = (string)ProjectSettings.GetSetting($"application/config/{SESSION_STORE_KEY}");

            if (string.IsNullOrEmpty(sessionJson))
            {
                _logger.Call("warn", "Saved session data is empty");
                return;
            }

            Supabase.Gotrue.Session sessionFromStorage = JsonSerializer.Deserialize<Supabase.Gotrue.Session>(sessionJson, _jsonOptions);

            if (sessionFromStorage == null || string.IsNullOrEmpty(sessionFromStorage.AccessToken))
            {
                _logger.Call("warn", "Deserialized session is invalid");
                return;
            }

            // Check if the session is expired
            if (ProjectSettings.HasSetting($"application/config/{SESSION_EXPIRY_KEY}"))
            {
                string expiryTimestamp = (string)ProjectSettings.GetSetting($"application/config/{SESSION_EXPIRY_KEY}");

                if (DateTime.TryParse(expiryTimestamp, out DateTime expiryTime) &&
                    DateTime.UtcNow > expiryTime)
                {
                    _logger.Call("info", "Loaded session is expired, will attempt to refresh");
                    // We still load the session to allow for refresh
                }
            }

            _currentSession = sessionFromStorage;
            _logger.Call("info", "Session loaded successfully");
        }
        catch (JsonException jsonEx)
        {
            _logger.Call("error", $"Failed to parse session JSON: {jsonEx.Message}");
            ClearSession();
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"Error loading session: {ex.Message}");
            ClearSession();
        }
    }

    /// <summary>
    /// Clears all session data from secure storage.
    /// </summary>
    private void ClearSession()
    {
        _logger.Call("debug", "Clearing session data");

        try
        {
            if (ProjectSettings.HasSetting($"application/config/{SESSION_STORE_KEY}"))
            {
                ProjectSettings.SetSetting($"application/config/{SESSION_STORE_KEY}", string.Empty);
            }

            if (ProjectSettings.HasSetting($"application/config/{SESSION_EXPIRY_KEY}"))
            {
                ProjectSettings.SetSetting($"application/config/{SESSION_EXPIRY_KEY}", string.Empty);
            }

            ProjectSettings.Save();
            _logger.Call("debug", "Session data cleared");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"Failed to clear session data: {ex.Message}");
        }
    }

    #endregion

    #region Testing Methods

    /// <summary>
    /// Provides a way to test the session expiry detection.
    /// This is for testing purposes only and should be removed in production.
    /// </summary>
    public void SimulateExpiredSession()
    {
        if (_currentSession != null)
        {
            // Set expiry to now (expired)
            ProjectSettings.SetSetting($"application/config/{SESSION_EXPIRY_KEY}", DateTime.UtcNow.AddMinutes(-5).ToString("o"));
            ProjectSettings.Save();
            _logger.Call("debug", "[TEST] Session expiry simulated");
        }
    }

    /// <summary>
    /// Runs a complete diagnostic check on the session management system.
    /// Returns a detailed report of the system state.
    /// This is for testing purposes only and should be made internal in production.
    /// </summary>
    /// <returns>A diagnostic report string</returns>
    public string RunDiagnostics()
    {
        var diagnostics = new System.Text.StringBuilder();

        diagnostics.AppendLine("=== SESSION MANAGER DIAGNOSTICS ===");
        diagnostics.AppendLine($"Current Time (UTC): {DateTime.UtcNow}");

        try
        {
            // Check if session exists
            bool hasSession = _currentSession != null;
            diagnostics.AppendLine($"Has Session: {hasSession}");

            if (hasSession)
            {
                diagnostics.AppendLine($"User ID: {_currentSession.User?.Id ?? "N/A"}");
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
            bool hasStoredSession = ProjectSettings.HasSetting($"application/config/{SESSION_STORE_KEY}");
            diagnostics.AppendLine($"Has Stored Session: {hasStoredSession}");

            bool hasStoredExpiry = ProjectSettings.HasSetting($"application/config/{SESSION_EXPIRY_KEY}");
            diagnostics.AppendLine($"Has Stored Expiry: {hasStoredExpiry}");

            if (hasStoredExpiry)
            {
                string storedTimestamp = (string)ProjectSettings.GetSetting($"application/config/{SESSION_EXPIRY_KEY}");
                diagnostics.AppendLine($"Stored Expiry Timestamp: {storedTimestamp}");
            }

            // Check login state
            diagnostics.AppendLine($"IsLoggedIn() Reports: {IsLoggedIn()}");

            // Check Supabase state
            diagnostics.AppendLine($"Supabase Client Initialized: {_supabase != null}");
            diagnostics.AppendLine($"Supabase Has Current Session: {_supabase?.Auth?.CurrentSession != null}");
        }
        catch (Exception ex)
        {
            diagnostics.AppendLine($"DIAGNOSTIC ERROR: {ex.Message}");
        }

        diagnostics.AppendLine("=== DIAGNOSTICS COMPLETE ===");

        return diagnostics.ToString();
    }

    #endregion
}
