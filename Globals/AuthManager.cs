using Godot;
using System;
using System.Threading.Tasks;
using Supabase.Gotrue;
using static Supabase.Gotrue.Constants;
using ProjectTerminal.Resources;
using Supabase.Postgrest.Responses;
using System.Collections.Generic;
using ProjectTerminal.Globals.Interfaces;

public partial class AuthManager : Node
{
    // Storage keys
    private const string USER_SESSION_KEY = "current_user_session";
    private const string SESSION_EXPIRY_KEY = "session_expiry_timestamp";
    private const string USER_NEW_STATE_KEY = "user_new_state";
    private const string PERSISTENT_SESSION_KEY = "is_persistent_session";
    private const int STANDARD_REFRESH_THRESHOLD_SECONDS = 300; // 5 minutes
    private const int PERSISTENT_REFRESH_THRESHOLD_SECONDS = 3600 * 24 * 6; // ~6 days

    // State
    private Session _currentSession;
    private bool _isNewUser;
    private bool _isClientInitialized = false;

    // Dependencies
    private Logger _logger;
    private ISecureStorageWrapper _secureStorage;
    private ISupabaseClientWrapper _supabaseClient;

    // Public properties
    public Session CurrentSession => _currentSession;
    public User CurrentUser => _currentSession.User;
    public bool IsNewUser => _isNewUser;


    [Signal]
    public delegate void SessionChangedEventHandler();

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("AuthManager: Initializing");

        _secureStorage = GetNode<SecureStorage>("/root/SecureStorage");
        SupabaseClient supabaseClientNode = GetNode<SupabaseClient>("/root/SupabaseClient");
        _supabaseClient = supabaseClientNode;

        supabaseClientNode.ClientInitialized += OnClientInitialized;

        LoadUserSessionAsync();

        var timer = new Timer();
        AddChild(timer);
        timer.WaitTime = 600;
        timer.Timeout += ValidateSessionHealth;
        timer.Start();
    }

    private void OnClientInitialized()
    {
        _isClientInitialized = true;
        SyncSessionWithSupabaseClient();
    }

    private async void SyncSessionWithSupabaseClient()
    {
        _logger.Debug("AuthManager: Syncing session with Supabase client");

        if (_currentSession == null || string.IsNullOrEmpty(_currentSession.AccessToken) || !_isClientInitialized)
            return;

        try
        {
            if (IsSessionExpired() && !string.IsNullOrEmpty(_currentSession.RefreshToken))
            {
                await RefreshSessionAsync();
            }
            else
            {
                // Use the interface method instead of Auth property
                await _supabaseClient.GetClient().Auth.SetSession(_currentSession.AccessToken, _currentSession.RefreshToken);
                _logger.Info("AuthManager: Session synced with Supabase client");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"AuthManager: Failed to sync session: {ex.Message}");
            _currentSession = null;
            ClearUserSession();
            EmitSignal(SignalName.SessionChanged);
        }
    }

    private async void ValidateSessionHealth()
    {
        _logger.Debug("AuthManager: Validating session health");

        if (_currentSession == null || !_isClientInitialized)
        {
            _logger.Debug("AuthManager: No session to validate or client not initialized");
            return;
        }

        try
        {
            DateTime? expiresAt = GetSessionExpiryTime();

            if (expiresAt.HasValue)
            {
                TimeSpan timeUntilExpiry = expiresAt.Value - DateTime.UtcNow;

                // Get if this is a persistent session
                bool isPersistent = _secureStorage.RetrieveValue<bool>(PERSISTENT_SESSION_KEY);
                int refreshThreshold = isPersistent ?
                    PERSISTENT_REFRESH_THRESHOLD_SECONDS :
                    STANDARD_REFRESH_THRESHOLD_SECONDS;

                if (timeUntilExpiry.TotalSeconds < refreshThreshold)
                {
                    _logger.Info($"AuthManager: Token expires in {timeUntilExpiry.TotalSeconds}s, refreshing");
                    await RefreshSessionAsync();
                }

                _logger.Debug($"AuthManager: Session expires in {timeUntilExpiry.TotalHours} hours, persistent: {isPersistent}");
            }
        }
        catch (Exception ex)
        {
            // More resilient error handling - don't throw
            _logger.Error($"AuthManager: Session validation error: {ex.Message}");
            // We'll attempt to refresh instead of just failing
            if (_currentSession?.RefreshToken != null)
            {
                try
                {
                    await RefreshSessionAsync();
                }
                catch
                {
                    // Silent failure - we'll try again later
                    _logger.Warn("AuthManager: Failed to refresh session during recovery");
                }
            }
        }
    }

    private async Task<bool> IsUserPartOfAnyOrganizationAsync(string userId)
    {
        try
        {
            ModeledResponse<Staff> response = await _supabaseClient.GetClient().From<Staff>()
                .Where(s => s.UserId == userId)
                .Get();

            return response.Models.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"AuthManager: Organization membership check failed: {ex.Message}");
            throw;
        }
    }

    private DateTime? GetSessionExpiryTime()
    {
        if (_currentSession == null)
            return null;

        if (_secureStorage.HasKey(SESSION_EXPIRY_KEY))
        {
            string storedTimestamp = _secureStorage.RetrieveValue<string>(SESSION_EXPIRY_KEY);
            if (DateTime.TryParse(storedTimestamp, out DateTime timestamp))
            {
                return timestamp.Kind == DateTimeKind.Utc ?
                    timestamp : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
            }
        }

        if (_currentSession.ExpiresIn > 0)
        {
            DateTime createdAtUtc = _currentSession.CreatedAt.Kind == DateTimeKind.Utc
                ? _currentSession.CreatedAt
                : DateTime.SpecifyKind(_currentSession.CreatedAt, DateTimeKind.Utc);

            return createdAtUtc.AddSeconds(_currentSession.ExpiresIn);
        }

        return null;
    }

    private void SaveUserSession(Session session)
    {
        if (session == null)
        {
            ClearUserSession();
            return;
        }

        try
        {
            _secureStorage.StoreObject(USER_SESSION_KEY, session);

            if (session.ExpiresIn > 0)
            {
                DateTime expiryTime = DateTime.UtcNow.AddSeconds(session.ExpiresIn);
                _secureStorage.StoreValue(SESSION_EXPIRY_KEY, expiryTime.ToString("o"));
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"AuthManager: Failed to save session: {ex.Message}");
        }
    }

    private async void LoadUserSessionAsync()
    {
        _logger.Debug("AuthManager: Loading saved session");

        try
        {
            Session sessionFromStorage = _secureStorage.RetrieveObject<Session>(USER_SESSION_KEY);
            if (sessionFromStorage == null || string.IsNullOrEmpty(sessionFromStorage.AccessToken))
            {
                _logger.Debug("AuthManager: No valid session found");
                return;
            }

            _currentSession = sessionFromStorage;
            _isNewUser = _secureStorage.RetrieveValue<bool>(USER_NEW_STATE_KEY);

            bool isPersistent = _secureStorage.RetrieveValue<bool>(PERSISTENT_SESSION_KEY);
            bool needsRefresh = IsSessionExpired();

            // Check if client is initialized
            if (_isClientInitialized)
            {
                if (needsRefresh)
                {
                    bool refreshSucceeded = await RefreshSessionAsync();
                }
                else
                {
                    // Use interface method instead of Auth property
                    await _supabaseClient.GetClient().Auth.SetSession(_currentSession.AccessToken, _currentSession.RefreshToken);
                }
            }
            // No need for the else clause as we're using the OnClientInitialized signal handler

            _logger.Info($"AuthManager: Session loaded successfully, persistent: {isPersistent}");
        }
        catch (Exception ex)
        {
            _logger.Error($"AuthManager: Session loading failed: {ex.Message}");
            ClearUserSession();
        }
    }

    private void ClearUserSession()
    {
        try
        {
            _secureStorage.ClearValue(USER_SESSION_KEY);
            _secureStorage.ClearValue(SESSION_EXPIRY_KEY);
            _secureStorage.ClearValue(USER_NEW_STATE_KEY);
            _isNewUser = false;
        }
        catch (Exception ex)
        {
            _logger.Error($"AuthManager: Failed to clear session: {ex.Message}");
        }
    }

    // ------------------- Public Methods ------------------

    public async Task<Session> RegisterWithEmailAsync(string email, string password, bool rememberMe = false)
    {
        _logger.Info($"AuthManager: Registering new user with email {email}");

        try
        {
            // Sign up with Supabase Auth
            Session session = await _supabaseClient.GetClient().Auth.SignUp(email, password);

            if (session?.User == null)
            {
                _logger.Warn("AuthManager: Invalid session returned from registration");
                return null;
            }

            // Set the user as new
            _isNewUser = true;
            _secureStorage.StoreValue(USER_NEW_STATE_KEY, _isNewUser);

            // Store the persistent session preference
            _secureStorage.StoreValue(PERSISTENT_SESSION_KEY, rememberMe);

            // Store session
            _currentSession = session;
            SaveUserSession(session);

            _logger.Info($"AuthManager: Login successful for user {session.User.Id}, persistent: {rememberMe}");
            EmitSignal(SignalName.SessionChanged);
            return session;
        }
        catch (Exception ex)
        {
            _logger.Error($"AuthManager: Registration failed: {ex.Message}", new Dictionary<string, object> { { "email", email } });
            throw;
        }
    }

    public async Task<Session> VerifyLoginOtpAsync(string phoneNumber, string otpCode, bool rememberMe = false)
    {
        _logger.Info($"AuthManager: Verifying OTP for {phoneNumber}");

        try
        {
            Session session = await _supabaseClient.GetClient().Auth.VerifyOTP(phoneNumber, otpCode, MobileOtpType.SMS);

            if (session?.User == null)
            {
                _logger.Warn("AuthManager: Invalid session returned from OTP verification");
                return null;
            }

            await _supabaseClient.GetClient().Auth.SetSession(session.AccessToken, session.RefreshToken);

            _isNewUser = !await IsUserPartOfAnyOrganizationAsync(session.User.Id);
            _secureStorage.StoreValue(USER_NEW_STATE_KEY, _isNewUser);

            // Store the persistent session preference
            _secureStorage.StoreValue(PERSISTENT_SESSION_KEY, rememberMe);

            _currentSession = session;
            SaveUserSession(session);

            _logger.Info($"AuthManager: Login successful for user {session.User.Id}, persistent: {rememberMe}");
            EmitSignal(SignalName.SessionChanged);
            return session;
        }
        catch (Exception ex)
        {
            _logger.Error($"AuthManager: OTP verification failed: {ex.Message}", new Dictionary<string, object> { { "phone", phoneNumber } });
            throw;
        }
    }

    public async Task<User> UpdateUserEmailAsync(string email)
    {
        try
        {
            if (CurrentUser == null)
            {
                _logger.Error("AuthManager: Cannot update email - no user logged in");
                throw new InvalidOperationException("No user is logged in");
            }

            _logger.Debug($"AuthManager: Updating email for user {CurrentUser.Id}");

            // Create update attributes
            var attrs = new UserAttributes { Email = email.Trim() };

            // Update user email
            User response = await _supabaseClient.GetClient().Auth.Update(attrs) ??
                throw new Exception("Failed to update user email");

            _logger.Info($"AuthManager: Email updated successfully for user {CurrentUser.Id}");

            // Update session if needed
            if (_currentSession?.User != null)
            {
                _currentSession.User = response;
                SaveUserSession(_currentSession);
            }

            EmitSignal(SignalName.SessionChanged);

            return response;
        }
        catch (Exception ex)
        {
            _logger.Error($"AuthManager: Failed to update email: {ex.Message}");
            throw new Exception($"Failed to update email: {ex.Message}", ex);
        }
    }

    public bool SetUserAsExisting()
    {
        _isNewUser = false;
        _secureStorage.StoreValue(USER_NEW_STATE_KEY, _isNewUser);
        return _isNewUser;
    }

    public async Task RequestLoginOtpAsync(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || !phoneNumber.StartsWith("+"))
        {
            _logger.Error("AuthManager: Invalid phone number format");
            throw new ArgumentException("Phone number must be in E.164 format (e.g., +1234567890)");
        }

        _logger.Info($"AuthManager: Requesting OTP for {phoneNumber}");

        try
        {
            await _supabaseClient.GetClient().Auth.SignIn(SignInType.Phone, phoneNumber);
            _logger.Debug($"AuthManager: OTP sent to {phoneNumber}");
        }
        catch (Exception ex)
        {
            _logger.Error($"AuthManager: OTP request failed: {ex.Message}",
                new Dictionary<string, object> { { "phone", phoneNumber } });
            throw;
        }
    }


    public async Task<bool> RefreshSessionAsync()
    {
        if (_currentSession == null || string.IsNullOrEmpty(_currentSession.RefreshToken) || !_isClientInitialized)
        {
            _logger.Warn("AuthManager: No refresh token available or client not initialized");
            return false;
        }

        try
        {
            Session refreshedSession = await _supabaseClient.GetClient().Auth.RefreshSession();

            if (refreshedSession != null)
            {
                _currentSession = refreshedSession;
                SaveUserSession(refreshedSession);
                _logger.Debug("AuthManager: Session refreshed successfully");
                EmitSignal(SignalName.SessionChanged);
                return true;
            }
            else
            {
                _logger.Error("AuthManager: Session refresh returned null");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"AuthManager: Session refresh failed: {ex.Message}");

            // Additional recovery logic for certain types of failures
            // if (ex.Message.Contains("token expired") || ex.Message.Contains("invalid token"))
            // {
            //     // Try to recover with stored credentials if available
            //     return await TryRecoverExpiredSession();
            // }

            return false;
        }
    }

    public async Task LogoutAsync()
    {
        _logger.Info("AuthManager: Logging out user");

        try
        {
            if (_isClientInitialized && _currentSession != null)
                await _supabaseClient.GetClient().Auth.SignOut();
        }
        catch (Exception ex)
        {
            _logger.Warn($"AuthManager: Server logout error: {ex.Message}");
            // Continue with local logout
        }

        _currentSession = null;
        ClearUserSession();

        await _supabaseClient.Initialize();
        EmitSignal(SignalName.SessionChanged);
    }

    public async Task<Session> LoginWithEmailAsync(string email, string password, bool rememberMe = false)
    {
        _logger.Info($"AuthManager: Logging in user with email {email}");

        try
        {
            Session session = await _supabaseClient.GetClient().Auth.SignIn(email, password);

            if (session?.User == null)
            {
                _logger.Warn("AuthManager: Invalid session returned from login");
                return null;
            }

            // Set the user as existing
            _isNewUser = false;
            _secureStorage.StoreValue(USER_NEW_STATE_KEY, _isNewUser);

            // Store the persistent session preference
            _secureStorage.StoreValue(PERSISTENT_SESSION_KEY, rememberMe);

            // Store session
            _currentSession = session;
            SaveUserSession(session);

            _logger.Info($"AuthManager: Login successful for user {session.User.Id}, persistent: {rememberMe}");
            EmitSignal(SignalName.SessionChanged);
            return session;
        }
        catch (Exception ex)
        {
            _logger.Error($"AuthManager: Login failed: {ex.Message}", new Dictionary<string, object> { { "email", email } });
            throw;
        }
    }

    public bool IsLoggedIn()
    {
        return _currentSession.User != null && !IsSessionExpired();
    }

    private bool IsSessionExpired()
    {
        DateTime? expiryTime = GetSessionExpiryTime();
        return expiryTime.HasValue && DateTime.UtcNow > expiryTime.Value;
    }

    public string RunAuthDiagnostics()
    {
        var diagnostics = new System.Text.StringBuilder();
        diagnostics.AppendLine("=== AUTH MANAGER DIAGNOSTICS ===");
        diagnostics.AppendLine($"Current Time (UTC): {DateTime.UtcNow}");

        try
        {
            diagnostics.AppendLine($"Is New User: {_isNewUser}");
            bool hasUserSession = _currentSession != null;
            diagnostics.AppendLine($"Has User Session: {hasUserSession}");

            if (hasUserSession)
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

            diagnostics.AppendLine($"Has Stored Session: {_secureStorage.HasKey(USER_SESSION_KEY)}");
            diagnostics.AppendLine($"Has Stored Expiry: {_secureStorage.HasKey(SESSION_EXPIRY_KEY)}");
            diagnostics.AppendLine($"IsLoggedIn(): {IsLoggedIn()}");
        }
        catch (Exception ex)
        {
            diagnostics.AppendLine($"DIAGNOSTIC ERROR: {ex.Message}");
        }

        diagnostics.AppendLine("=== AUTH DIAGNOSTICS COMPLETE ===");
        return diagnostics.ToString();
    }
}
