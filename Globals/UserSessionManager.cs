using Godot;
using System;
using System.Threading.Tasks;
using System.Text.Json;
using Supabase;
using static Supabase.Gotrue.Constants;
using FileAccess = Godot.FileAccess;

/// <summary>
/// Manages user authentication state and persistence for the POS terminal.
/// Handles session storage, retrieval, and Supabase authentication operations.
/// Acts as a central authentication service for the entire application.
/// </summary>
public partial class UserSessionManager : Node
{
    #region Constants and Fields

    /// <summary>
    /// Path to the session storage file using Godot's user:// virtual directory.
    /// </summary>
    private const string SESSION_FILE_PATH = "user://supabase_session.json";

    /// <summary>
    /// Backup session file path in case primary file becomes corrupted.
    /// </summary>
    private const string BACKUP_SESSION_FILE_PATH = "user://supabase_session_backup.json";

    /// <summary>
    /// Time in milliseconds to wait between file operations.
    /// </summary>
    private const int FILE_OPERATION_DELAY = 100;

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
    /// JSON serialization options for consistent file formatting.
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
            LoadSessionFromDisk();

            // Initialize Supabase client with loaded session
            await _supabase.InitializeAsync();
            _logger.Call("info", "Supabase client initialized");

            // Schedule periodic session validation
            CallDeferred(nameof(ScheduleSessionValidation));
        }
        catch (Exception ex)
        {
            _logger.Call("critical", $"Failed to initialize UserSessionManager: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Sets up a periodic timer to validate session health.
    /// </summary>
    private void ScheduleSessionValidation()
    {
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
            // Check if token is expired or about to expire (within 5 minutes)
            DateTime? expiresAt = null;

            if (_currentSession.ExpiresIn > 0)
            {
                // Calculate expiry time based on ExpiresIn
                expiresAt = DateTime.UtcNow.AddSeconds(_currentSession.ExpiresIn);
            }

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
                await SaveSessionToDiskAsync();

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

            // Check the available methods for refreshing sessions in your Supabase version
            // Adjust based on your Supabase.Auth API
            var refreshedSession = await _supabase.Auth.RefreshSession();

            if (refreshedSession != null)
            {
                _currentSession = refreshedSession;
                await SaveSessionToDiskAsync();
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
        await DeleteSessionFilesAsync();

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
        bool hasValidSession = _currentSession != null && _currentSession.User != null;

        if (hasValidSession)
        {
            // Check if token is expired
            if (_currentSession.ExpiresIn > 0)
            {
                // Assuming ExpiresIn is in seconds from session creation
                var createdAt = _currentSession.CreatedAt;
                var expiresAt = createdAt.AddSeconds(_currentSession.ExpiresIn);
                var now = DateTime.UtcNow;

                if (now > expiresAt)
                {
                    _logger.Call("debug", "Session token is expired");
                    return false;
                }
            }
        }

        return hasValidSession;
    }

    #endregion

    #region Session Storage Methods

    /// <summary>
    /// Saves the current session to disk asynchronously with retry logic.
    /// </summary>
    private async Task SaveSessionToDiskAsync()
    {
        if (_currentSession == null)
        {
            _logger.Call("debug", "No session to save");
            await DeleteSessionFilesAsync();
            return;
        }

        _logger.Call("debug", "Saving session to disk");

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                // Serialize the session to JSON with pretty printing
                string sessionJson = JsonSerializer.Serialize(_currentSession, _jsonOptions);

                // First write to a temporary file
                string tempFilePath = SESSION_FILE_PATH + ".tmp";
                using (FileAccess file = FileAccess.Open(tempFilePath, FileAccess.ModeFlags.Write))
                {
                    if (file == null)
                    {
                        throw new Exception($"Failed to open temp file for writing: {tempFilePath}, Error: {FileAccess.GetOpenError()}");
                    }

                    file.StoreString(sessionJson);
                    file.Flush();
                    file.Close();
                }

                // Short delay to ensure file operations complete
                await Task.Delay(FILE_OPERATION_DELAY);

                // Verify the temp file was written correctly
                if (!FileAccess.FileExists(tempFilePath))
                {
                    throw new Exception("Temp file was not created successfully");
                }

                // Read back the temp file to verify contents
                string verifyContent;
                using (FileAccess verifyFile = FileAccess.Open(tempFilePath, FileAccess.ModeFlags.Read))
                {
                    if (verifyFile == null)
                    {
                        throw new Exception($"Failed to open temp file for verification: {tempFilePath}, Error: {FileAccess.GetOpenError()}");
                    }

                    verifyContent = verifyFile.GetAsText();
                    verifyFile.Close();
                }

                // Basic validation that the JSON contains key session elements
                if (!verifyContent.Contains("accessToken") || !verifyContent.Contains("refreshToken"))
                {
                    throw new Exception("Session file verification failed: missing token data");
                }

                // If verification passes, replace the actual file
                if (FileAccess.FileExists(SESSION_FILE_PATH))
                {
                    // Backup the existing file first
                    using (FileAccess existingFile = FileAccess.Open(SESSION_FILE_PATH, FileAccess.ModeFlags.Read))
                    {
                        if (existingFile != null)
                        {
                            string existingContent = existingFile.GetAsText();
                            existingFile.Close();

                            using (FileAccess backupFile = FileAccess.Open(BACKUP_SESSION_FILE_PATH, FileAccess.ModeFlags.Write))
                            {
                                if (backupFile != null)
                                {
                                    backupFile.StoreString(existingContent);
                                    backupFile.Close();
                                }
                            }
                        }
                    }

                    // Safe delete of the original file
                    DirAccess dir = DirAccess.Open("user://");
                    if (dir != null)
                    {
                        dir.Remove("supabase_session.json");
                    }

                    await Task.Delay(FILE_OPERATION_DELAY);
                }

                // Rename temp file to the actual file
                DirAccess dirRename = DirAccess.Open("user://");
                if (dirRename != null)
                {
                    dirRename.Rename("supabase_session.json.tmp", "supabase_session.json");
                }

                _logger.Call("debug", "Session saved successfully");
                return;
            }
            catch (Exception ex)
            {
                _logger.Call("error", $"Save attempt {attempt} failed: {ex.Message}");

                if (attempt >= 3)
                {
                    _logger.Call("critical", "Failed to save session after multiple attempts");
                    throw;
                }

                // Wait before retrying
                await Task.Delay(FILE_OPERATION_DELAY * attempt);
            }
        }
    }

    /// <summary>
    /// Loads a previously saved session from disk with fallback to backup.
    /// </summary>
    private void LoadSessionFromDisk()
    {
        _logger.Call("debug", "Attempting to load session from disk");

        // Try primary file first
        try
        {
            if (FileAccess.FileExists(SESSION_FILE_PATH))
            {
                _logger.Call("debug", $"Session file found at {SESSION_FILE_PATH}");

                using FileAccess file = FileAccess.Open(SESSION_FILE_PATH, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    _logger.Call("error", $"Failed to open session file: {FileAccess.GetOpenError()}");
                    TryLoadFromBackup();
                    return;
                }

                string sessionJson = file.GetAsText();
                file.Close();

                if (string.IsNullOrEmpty(sessionJson))
                {
                    _logger.Call("warn", "Session file is empty");
                    TryLoadFromBackup();
                    return;
                }

                try
                {
                    Supabase.Gotrue.Session sessionFromDisk = JsonSerializer.Deserialize<Supabase.Gotrue.Session>(sessionJson);
                    if (sessionFromDisk == null || string.IsNullOrEmpty(sessionFromDisk.AccessToken))
                    {
                        _logger.Call("warn", "Deserialized session is invalid");
                        TryLoadFromBackup();
                        return;
                    }

                    _currentSession = sessionFromDisk;
                    _logger.Call("info", "Session successfully loaded");
                }
                catch (JsonException jsonEx)
                {
                    _logger.Call("error", $"Failed to parse session JSON: {jsonEx.Message}");
                    TryLoadFromBackup();
                }
            }
            else
            {
                _logger.Call("debug", "No session file found");
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"Error loading session: {ex.Message}");
            TryLoadFromBackup();
        }
    }

    /// <summary>
    /// Attempts to load session data from the backup file.
    /// </summary>
    private void TryLoadFromBackup()
    {
        _logger.Call("debug", "Attempting to load from backup file");

        try
        {
            if (!FileAccess.FileExists(BACKUP_SESSION_FILE_PATH))
            {
                _logger.Call("debug", "No backup session file found");
                return;
            }

            using FileAccess file = FileAccess.Open(BACKUP_SESSION_FILE_PATH, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                _logger.Call("error", $"Failed to open backup session file: {FileAccess.GetOpenError()}");
                return;
            }

            string sessionJson = file.GetAsText();
            file.Close();

            if (string.IsNullOrEmpty(sessionJson))
            {
                _logger.Call("warn", "Backup session file is empty");
                return;
            }

            var sessionFromDisk = JsonSerializer.Deserialize<Supabase.Gotrue.Session>(sessionJson);

            if (sessionFromDisk == null || string.IsNullOrEmpty(sessionFromDisk.AccessToken))
            {
                _logger.Call("warn", "Deserialized backup session is invalid");
                return;
            }

            _currentSession = sessionFromDisk;
            _logger.Call("info", "Session successfully loaded from backup");

            // Restore the primary file
            _ = SaveSessionToDiskAsync();
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"Error loading backup session: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely deletes all session files.
    /// </summary>
    private async Task DeleteSessionFilesAsync()
    {
        _logger.Call("debug", "Deleting session files");

        try
        {
            string[] filesToDelete = {
                SESSION_FILE_PATH,
                BACKUP_SESSION_FILE_PATH,
                SESSION_FILE_PATH + ".tmp"
            };

            DirAccess dir = DirAccess.Open("user://");
            if (dir != null)
            {
                foreach (var filePath in filesToDelete)
                {
                    // Extract filename from path
                    string fileName = filePath.Replace("user://", "");

                    if (FileAccess.FileExists(filePath))
                    {
                        dir.Remove(fileName);
                        await Task.Delay(FILE_OPERATION_DELAY);
                    }
                }

                _logger.Call("debug", "Session files removed");
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"Failed to delete session files: {ex.Message}");
        }
    }

    #endregion
}
