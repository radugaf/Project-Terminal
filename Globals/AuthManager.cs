using Godot;
using System;
using System.Threading.Tasks;
using Supabase.Gotrue;
using static Supabase.Gotrue.Constants;
using ProjectTerminal.Resources;
using Supabase.Postgrest.Responses;

public partial class AuthManager : Node
{
    // Storage keys
    private const string USER_SESSION_KEY = "current_user_session";
    private const string SESSION_EXPIRY_KEY = "session_expiry_timestamp";
    private const string USER_NEW_STATE_KEY = "user_new_state";
    private const int REFRESH_THRESHOLD_SECONDS = 300; // 5 minutes

    // State
    private Session _currentSession;
    private bool _isNewUser;

    // Dependencies
    private Node _logger;
    private SecureStorage _secureStorage;
    private SupabaseClient _supabaseClient;

    // Public properties
    public Session CurrentSession => _currentSession;
    public User CurrentUser => _currentSession?.User;
    public bool IsNewUser => _isNewUser;


    [Signal]
    public delegate void SessionChangedEventHandler();

    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "AuthManager: Initializing");

        _secureStorage = GetNode<SecureStorage>("/root/SecureStorage");
        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");

        LoadUserSessionAsync();

        var timer = new Timer();
        AddChild(timer);
        timer.WaitTime = 600;
        timer.Timeout += ValidateSessionHealth;
        timer.Start();

        _supabaseClient.ClientInitialized += SyncSessionWithSupabaseClient;
    }

    private async void SyncSessionWithSupabaseClient()
    {
        _logger.Call("debug", "AuthManager: Syncing session with Supabase client");

        if (_currentSession == null || string.IsNullOrEmpty(_currentSession.AccessToken))
            return;

        try
        {
            if (IsSessionExpired() && !string.IsNullOrEmpty(_currentSession.RefreshToken))
            {
                await RefreshSessionAsync();
            }
            else
            {
                await _supabaseClient.Auth.SetSession(_currentSession.AccessToken, _currentSession.RefreshToken);
                _logger.Call("info", "AuthManager: Session synced with Supabase client");
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Failed to sync session: {ex.Message}");
            _currentSession = null;
            ClearUserSession();
            EmitSignal(SignalName.SessionChanged);
        }
    }

    private async void ValidateSessionHealth()
    {
        if (_currentSession == null)
            return;

        try
        {
            DateTime? expiresAt = GetSessionExpiryTime();
            if (expiresAt.HasValue)
            {
                TimeSpan timeUntilExpiry = expiresAt.Value - DateTime.UtcNow;

                if (timeUntilExpiry.TotalSeconds < REFRESH_THRESHOLD_SECONDS)
                {
                    _logger.Call("info", $"AuthManager: Token expires in {timeUntilExpiry.TotalSeconds}s, refreshing");
                    await RefreshSessionAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Session validation failed: {ex.Message}");
        }
    }

    private async Task<bool> IsUserPartOfAnyOrganizationAsync(string userId)
    {
        try
        {
            var response = await _supabaseClient.From<Staff>()
                .Where(s => s.UserId == userId)
                .Get();

            return response.Models.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Organization membership check failed: {ex.Message}");
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
            _logger.Call("error", $"AuthManager: Failed to save session: {ex.Message}");
        }
    }

    private async void LoadUserSessionAsync()
    {
        _logger.Call("debug", "AuthManager: Loading saved session");

        try
        {
            Session sessionFromStorage = _secureStorage.RetrieveObject<Session>(USER_SESSION_KEY);
            if (sessionFromStorage == null || string.IsNullOrEmpty(sessionFromStorage.AccessToken))
            {
                _logger.Call("debug", "AuthManager: No valid session found");
                return;
            }

            _currentSession = sessionFromStorage;
            _isNewUser = _secureStorage.RetrieveValue<bool>(USER_NEW_STATE_KEY);

            bool needsRefresh = IsSessionExpired();

            if (_supabaseClient.Supabase != null)
            {
                if (needsRefresh)
                    await RefreshSessionAsync();
                else
                    await _supabaseClient.Auth.SetSession(_currentSession.AccessToken, _currentSession.RefreshToken);
            }
            else if (needsRefresh)
            {
                _supabaseClient.ClientInitialized += async () => await RefreshSessionAsync();
            }

            _logger.Call("info", "AuthManager: Session loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Session loading failed: {ex.Message}");
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
            _logger.Call("error", $"AuthManager: Failed to clear session: {ex.Message}");
        }
    }

    // ------------------- Public Methods ------------------
    public async Task<User> UpdateUserEmailAsync(string email)
    {
        try
        {
            if (CurrentUser == null)
            {
                _logger.Call("error", "AuthManager: Cannot update email - no user logged in");
                throw new InvalidOperationException("No user is logged in");
            }

            _logger.Call("debug", $"AuthManager: Updating email for user {CurrentUser.Id}");

            // Create update attributes
            var attrs = new UserAttributes { Email = email.Trim() };

            // Update user email
            User response = await _supabaseClient.Auth.Update(attrs) ??
                throw new Exception("Failed to update user email");

            _logger.Call("info", $"AuthManager: Email updated successfully for user {CurrentUser.Id}");

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
            _logger.Call("error", $"AuthManager: Failed to update email: {ex.Message}");
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
            _logger.Call("error", "AuthManager: Invalid phone number format");
            throw new ArgumentException("Phone number must be in E.164 format (e.g., +1234567890)");
        }

        _logger.Call("info", $"AuthManager: Requesting OTP for {phoneNumber}");

        try
        {
            await _supabaseClient.Auth.SignIn(SignInType.Phone, phoneNumber);
            _logger.Call("debug", $"AuthManager: OTP sent to {phoneNumber}");
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: OTP request failed: {ex.Message}",
                new Godot.Collections.Dictionary { { "phone", phoneNumber } });
            throw;
        }
    }

    public async Task<Session> VerifyLoginOtpAsync(string phoneNumber, string otpCode)
    {
        _logger.Call("info", $"AuthManager: Verifying OTP for {phoneNumber}");

        try
        {
            Session session = await _supabaseClient.Auth.VerifyOTP(phoneNumber, otpCode, MobileOtpType.SMS);

            if (session?.User == null)
            {
                _logger.Call("warn", "AuthManager: Invalid session returned from OTP verification");
                return null;
            }

            await _supabaseClient.Auth.SetSession(session.AccessToken, session.RefreshToken);

            _isNewUser = !await IsUserPartOfAnyOrganizationAsync(session.User.Id);
            _secureStorage.StoreValue(USER_NEW_STATE_KEY, _isNewUser);

            _currentSession = session;
            SaveUserSession(session);

            _logger.Call("info", $"AuthManager: Login successful for user {session.User.Id}");
            EmitSignal(SignalName.SessionChanged);
            return session;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: OTP verification failed: {ex.Message}",
                new Godot.Collections.Dictionary { { "phone", phoneNumber } });
            throw;
        }
    }

    public async Task RefreshSessionAsync()
    {
        if (_currentSession == null || string.IsNullOrEmpty(_currentSession.RefreshToken))
        {
            _logger.Call("warn", "AuthManager: No refresh token available");
            return;
        }

        try
        {
            if (_supabaseClient?.Auth == null)
            {
                _logger.Call("error", "AuthManager: Supabase client not initialized");
                throw new InvalidOperationException("Supabase client not initialized");
            }

            Session refreshedSession = await _supabaseClient.Auth.RefreshSession();

            if (refreshedSession != null)
            {
                _currentSession = refreshedSession;
                SaveUserSession(refreshedSession);
                _logger.Call("debug", "AuthManager: Session refreshed successfully");
                EmitSignal(SignalName.SessionChanged);
            }
            else
            {
                _logger.Call("error", "AuthManager: Session refresh returned null");
                throw new InvalidOperationException("Session refresh returned null");
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"AuthManager: Session refresh failed: {ex.Message}");
            throw;
        }
    }

    public async Task LogoutAsync()
    {
        _logger.Call("info", "AuthManager: Logging out user");

        try
        {
            if (_supabaseClient.Auth?.CurrentSession != null)
                await _supabaseClient.Auth.SignOut();
        }
        catch (Exception ex)
        {
            _logger.Call("warn", $"AuthManager: Server logout error: {ex.Message}");
            // Continue with local logout
        }

        _currentSession = null;
        ClearUserSession();

        await _supabaseClient.ReinitializeClientAsync();
        EmitSignal(SignalName.SessionChanged);
    }

    public bool IsLoggedIn()
    {
        return _currentSession?.User != null && !IsSessionExpired();
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
