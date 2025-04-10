using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Supabase.Gotrue;
using static Supabase.Gotrue.Constants;
using ProjectTerminal.Globals.Interfaces;
using ProjectTerminal.Globals.Services;
using ProjectTerminal.Resources;


namespace ProjectTerminal.Globals.Services
{
    public partial class AuthManager : Node, IAuthManager
    {
        private ISessionManager _sessionManager;
        private ISupabaseClientWrapper _supabaseClient;
        private Logger _logger;
        private ITimeProvider _timeProvider;

        private bool _isClientInitialized = false;
        private Timer _sessionCheckTimer;

        public Session CurrentSession => _sessionManager?.CurrentSession;
        public User CurrentUser => CurrentSession?.User;
        public bool IsNewUser => _sessionManager?.GetUserNewState() ?? false;

        [Signal]
        public delegate void SessionChangedEventHandler();


        #region Initialization

        public AuthManager() { }

        public AuthManager(ISessionManager sessionManager, ISupabaseClientWrapper supabaseClient, Logger logger, ITimeProvider timeProvider)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public override void _Ready()
        {
            if (_sessionManager != null && _supabaseClient != null && _logger != null && _timeProvider != null)
            {
                _logger.Debug("AuthManager: Dependencies already injected, skipping auto-initialization");
                SetupSessionTimer();
                return;
            }

            _logger = GetNode<Logger>("/root/Logger");
            _logger.Info("AuthManager: Initializing");

            if (_sessionManager == null)
            {
                SecureStorage secureStorage = GetNode<SecureStorage>("/root/SecureStorage");
                _timeProvider = new SystemTimeProvider();
                _sessionManager = new SessionManager(secureStorage, _timeProvider, _logger);
            }

            if (_supabaseClient == null)
            {
                SupabaseClient supabaseClientNode = GetNode<SupabaseClient>("/root/SupabaseClient");
                _supabaseClient = supabaseClientNode;
                supabaseClientNode.ClientInitialized += OnClientInitialized;
            }

            LoadUserSessionAsync();
            SetupSessionTimer();
        }

        private void SetupSessionTimer()
        {
            _sessionCheckTimer = new Timer();
            AddChild(_sessionCheckTimer);
            _sessionCheckTimer.WaitTime = 10;
            _sessionCheckTimer.Timeout += ValidateSessionHealth;
            _sessionCheckTimer.Start();
        }

        private void OnClientInitialized()
        {
            _isClientInitialized = true;
            SyncSessionWithSupabaseClient();
        }

        private async void SyncSessionWithSupabaseClient()
        {
            _logger.Debug("AuthManager: Syncing session with Supabase client");

            Session session = _sessionManager.CurrentSession;
            if (session == null || string.IsNullOrEmpty(session.AccessToken) || !_isClientInitialized)
                return;

            try
            {
                if (_sessionManager.IsSessionExpired() && !string.IsNullOrEmpty(session.RefreshToken))
                {
                    await RefreshSessionAsync();
                }
                else
                {
                    await _supabaseClient.GetClient().Auth.SetSession(session.AccessToken, session.RefreshToken, true);
                    _logger.Info("AuthManager: Session synced with Supabase client");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"AuthManager: Failed to sync session: {ex.Message}");
                _sessionManager.ClearSession();
                EmitSessionChanged();
            }
        }

        private async void LoadUserSessionAsync()
        {
            _logger.Debug("AuthManager: Trying to load user session");

            try
            {
                Session session = await _sessionManager.LoadSessionAsync();
                if (session == null)
                {
                    _logger.Debug("AuthManager: No valid session found");
                    return;
                }

                // Check if session is severely expired
                DateTime? expiryTime = _sessionManager.GetSessionExpiryTime();
                if (expiryTime.HasValue && (expiryTime.Value - _timeProvider.UtcNow).TotalDays < -30)
                {
                    _logger.Warn("AuthManager: Loaded severely expired session, clearing");
                    _sessionManager.ClearSession();
                    EmitSessionChanged();
                    return;
                }

                // Try to set up session with Supabase client
                if (_isClientInitialized)
                {
                    try
                    {
                        await _supabaseClient.GetClient().Auth.SetSession(session.AccessToken, session.RefreshToken, true);

                        // Proactively refresh if approaching expiry or expired but recoverable
                        if (expiryTime.HasValue && (expiryTime.Value - _timeProvider.UtcNow).TotalHours < 24)
                        {
                            _ = RefreshSessionAsync();
                        }

                        _logger.Info($"AuthManager: Session loaded successfully, persistent: {_sessionManager.IsPersistentSession}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"AuthManager: Failed to set session: {ex.Message}");
                        _sessionManager.ClearSession();
                        EmitSessionChanged();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"AuthManager: Session loading failed: {ex.Message}");
                _sessionManager.ClearSession();
                EmitSessionChanged();
            }
        }

        private void EmitSessionChanged()
        {
            EmitSignal(SignalName.SessionChanged);
        }

        #endregion

        #region Session Management

        public void ValidateSessionHealth()
        {
            Session session = _sessionManager.CurrentSession;
            if (session == null || !_isClientInitialized)
            {
                _logger.Debug("AuthManager: No session to validate or client not initialized");
                return;
            }

            try
            {
                DateTime? expiresAt = _sessionManager.GetSessionExpiryTime();
                bool isPersistent = _sessionManager.IsPersistentSession;

                if (expiresAt.HasValue)
                {
                    TimeSpan timeUntilExpiry = expiresAt.Value - _timeProvider.UtcNow;
                    int refreshThreshold = _sessionManager.GetRefreshThresholdSeconds();

                    // Check if token is already expired
                    if (timeUntilExpiry.TotalSeconds < 0)
                    {
                        // If severely expired (more than 30 days), clear session and force re-login
                        if (timeUntilExpiry.TotalDays < -30)
                        {
                            _logger.Warn($"AuthManager: Session severely expired ({timeUntilExpiry.TotalDays:F1} days), clearing session");
                            _sessionManager.ClearSession();
                            EmitSessionChanged();
                            return;
                        }

                        // For moderately expired sessions, attempt refresh if we have a refresh token
                        if (!string.IsNullOrEmpty(session.RefreshToken) && isPersistent)
                        {
                            _logger.Info($"AuthManager: Attempting to recover expired session (expired {Math.Abs(timeUntilExpiry.TotalHours):F1}h ago)");
                            _ = RefreshSessionWithRecoveryAsync();
                            return;
                        }
                    }
                    // Standard proactive refresh logic for valid tokens
                    else if (timeUntilExpiry.TotalSeconds < refreshThreshold)
                    {
                        _logger.Info($"AuthManager: Token expires in {timeUntilExpiry.TotalSeconds:F1}s, refreshing");
                        _ = RefreshSessionAsync();
                    }

                    _logger.Debug($"AuthManager: Session expires in {timeUntilExpiry.TotalHours:F1} hours, persistent: {isPersistent}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"AuthManager: Session validation error: {ex.Message}");
            }
        }

        private async Task<bool> RefreshSessionWithRecoveryAsync()
        {
            try
            {
                bool refreshSuccess = await RefreshSessionAsync();
                if (refreshSuccess)
                    return true;

                _logger.Info("AuthManager: Standard refresh failed, attempting recovery flow");

                // Recovery failed, clear session and notify UI
                _sessionManager.ClearSession();
                EmitSessionChanged();
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"AuthManager: Recovery refresh failed: {ex.Message}");
                _sessionManager.ClearSession();
                EmitSessionChanged();
                return false;
            }
        }

        private async Task<bool> IsUserPartOfAnyOrganizationAsync(string userId)
        {
            try
            {
                var response = await _supabaseClient.GetClient().From<Staff>()
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

        public async Task<bool> RefreshSessionAsync()
        {
            Session session = _sessionManager.CurrentSession;
            if (session == null || string.IsNullOrEmpty(session.RefreshToken) || !_isClientInitialized)
            {
                _logger.Warn("AuthManager: No refresh token available or client not initialized");
                return false;
            }

            try
            {
                _logger.Debug("AuthManager: Attempting to refresh session");
                // Attempt to refresh the session using the Supabase client
                try
                {
                    await _supabaseClient.GetClient().Auth.RefreshToken();
                }
                catch (Exception ex)
                {
                    _logger.Error($"AuthManager: Failed to set session: {ex.Message}");
                    return false;
                }

                Session refreshedSession = await _supabaseClient.GetClient().Auth.RefreshSession();

                if (refreshedSession != null)
                {
                    _sessionManager.SaveSession(refreshedSession, _sessionManager.IsPersistentSession);
                    _logger.Debug("AuthManager: Session refreshed successfully");
                    EmitSessionChanged();
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
                return false;
            }
        }

        public bool IsLoggedIn() => CurrentUser != null && !_sessionManager.IsSessionExpired();

        #endregion

        #region Authentication Operations

        public async Task<Session> RegisterWithEmailAsync(string email, string password, bool rememberMe = false)
        {
            _logger.Info($"AuthManager: Registering new user with email {email}");

            try
            {
                Session session = await _supabaseClient.GetClient().Auth.SignUp(email, password);

                if (session?.User == null)
                {
                    _logger.Warn("AuthManager: Invalid session returned from registration");
                    return null;
                }

                _sessionManager.SetUserNewState(true);
                _sessionManager.SaveSession(session, rememberMe);

                _logger.Info($"AuthManager: Registration successful for user {session.User.Id}, persistent: {rememberMe}");
                EmitSessionChanged();
                return session;
            }
            catch (Exception ex)
            {
                _logger.Error($"AuthManager: Registration failed: {ex.Message}",
                    new Dictionary<string, object> { { "email", email } });
                throw;
            }
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

                _sessionManager.SetUserNewState(false);
                _sessionManager.SaveSession(session, rememberMe);

                _logger.Info($"AuthManager: Login successful for user {session.User.Id}, persistent: {rememberMe}");
                EmitSessionChanged();
                return session;
            }
            catch (Exception ex)
            {
                _logger.Error($"AuthManager: Login failed: {ex.Message}",
                    new Dictionary<string, object> { { "email", email } });
                throw;
            }
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

                await _supabaseClient.GetClient().Auth.SetSession(session.AccessToken, session.RefreshToken, true);

                bool isNewUser = !await IsUserPartOfAnyOrganizationAsync(session.User.Id);
                _sessionManager.SetUserNewState(isNewUser);
                _sessionManager.SaveSession(session, rememberMe);

                _logger.Info($"AuthManager: Login successful for user {session.User.Id}, persistent: {rememberMe}");
                EmitSessionChanged();
                return session;
            }
            catch (Exception ex)
            {
                _logger.Error($"AuthManager: OTP verification failed: {ex.Message}",
                    new Dictionary<string, object> { { "phone", phoneNumber } });
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

                var attrs = new UserAttributes { Email = email.Trim() };
                User response = await _supabaseClient.GetClient().Auth.Update(attrs) ??
                    throw new Exception("Failed to update user email");

                _logger.Info($"AuthManager: Email updated successfully for user {CurrentUser.Id}");

                if (CurrentSession?.User != null)
                {
                    var updatedSession = CurrentSession;
                    updatedSession.User = response;
                    _sessionManager.SaveSession(updatedSession, _sessionManager.IsPersistentSession);
                }

                EmitSessionChanged();
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
            _sessionManager.SetUserNewState(false);
            return _sessionManager.GetUserNewState();
        }

        public async Task LogoutAsync()
        {
            _logger.Info("AuthManager: Logging out user");

            try
            {
                if (_isClientInitialized && CurrentSession != null)
                    await _supabaseClient.GetClient().Auth.SignOut();
            }
            catch (Exception ex)
            {
                _logger.Warn($"AuthManager: Server logout error: {ex.Message}");
            }

            _sessionManager.ClearSession();

            await _supabaseClient.Initialize();
            EmitSessionChanged();
        }

        public string RunAuthDiagnostics()
        {
            var diagnostics = new System.Text.StringBuilder();
            diagnostics.AppendLine("=== AUTH MANAGER DIAGNOSTICS ===");
            diagnostics.AppendLine($"Current Time (UTC): {_timeProvider.UtcNow}");

            try
            {
                diagnostics.AppendLine($"Is New User: {IsNewUser}");
                bool hasUserSession = CurrentSession != null;
                diagnostics.AppendLine($"Has User Session: {hasUserSession}");

                if (hasUserSession)
                {
                    diagnostics.AppendLine($"User ID: {CurrentSession.User?.Id ?? "N/A"}");
                    diagnostics.AppendLine($"Has Access Token: {!string.IsNullOrEmpty(CurrentSession.AccessToken)}");
                    diagnostics.AppendLine($"Has Refresh Token: {!string.IsNullOrEmpty(CurrentSession.RefreshToken)}");
                    diagnostics.AppendLine($"Created At: {CurrentSession.CreatedAt}");
                    diagnostics.AppendLine($"Expires In: {CurrentSession.ExpiresIn} seconds");

                    DateTime? expiryTime = _sessionManager.GetSessionExpiryTime();
                    if (expiryTime.HasValue)
                    {
                        diagnostics.AppendLine($"Calculated Expiry: {expiryTime.Value}");
                        diagnostics.AppendLine($"Is Expired: {_timeProvider.UtcNow > expiryTime.Value}");
                        diagnostics.AppendLine($"Time Until Expiry: {expiryTime.Value - _timeProvider.UtcNow}");
                    }
                    else
                    {
                        diagnostics.AppendLine("Expiry Time: Could not be determined");
                    }
                }

                diagnostics.AppendLine($"IsLoggedIn(): {IsLoggedIn()}");
                diagnostics.AppendLine($"IsPersistentSession: {_sessionManager.IsPersistentSession}");
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

}
