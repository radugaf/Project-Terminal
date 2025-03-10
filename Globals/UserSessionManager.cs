using Godot;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Supabase;
using static Supabase.Gotrue.Constants;
using Microsoft.Extensions.Logging; // Provides SignInType, MobileOtpType, etc.

/// <summary>
/// Manages user sessions and authentication tokens in a single, centralized location.
/// Loads and saves the session from local storage for seamless re-authentication between app restarts.
/// Offers methods for requesting and verifying phone-based OTP, as well as logging out.
/// Initializes the Supabase client once and ensures all authentication details are consistently applied.
/// </summary>
public partial class UserSessionManager : Node
{
    Node _logger;

    /// <summary>
    /// Defines the local file where the session is stored so it can be reused across app sessions.
    /// "user://" is a Godot-specific path that typically maps to a writable folder on the local machine.
    /// </summary>
    private const string SESSION_FILE_PATH = "user://supabase_session.json";

    /// <summary>
    /// Holds the Supabase client instance for the entire application runtime.
    /// This object is central to interacting with Supabase services (Auth, Storage, Realtime, etc.).
    /// </summary>
    private Client _supabase;

    /// <summary>
    /// Stores the current session, including access tokens and user information.
    /// Determines if the user is logged in and can be used to restore login state.
    /// </summary>
    private Supabase.Gotrue.Session _currentSession;

    /// <summary>
    /// Provides read-only access to the current session object.
    /// </summary>
    public Supabase.Gotrue.Session CurrentSession => _currentSession;

    /// <summary>
    /// Provides a quick reference to the currently authenticated user.
    /// Null if no one is logged in or if the session object does not have user data.
    /// </summary>
    public Supabase.Gotrue.User CurrentUser => _currentSession?.User;

    /// <summary>
    /// Emitted whenever a session change occurs, including login and logout events.
    /// Other parts of the application can connect to this signal to respond to authentication changes.
    /// </summary>
    [Signal]
    public delegate void SessionChangedEventHandler();

    /// <summary>
    /// Performs setup tasks on node creation, including reading environment variables,
    /// creating the Supabase client, and attempting to load any previously saved session.
    /// </summary>
    public override async void _Ready()
    {
        GD.Print("Initializing UserSessionManager...");
        _logger = GetNode<Node>("/root/Logger");

        // Reads Supabase environment variables from an EnvLoader autoload node.
        Node envLoader = GetNode("/root/EnvLoader");
        string supabaseUrl = (string)envLoader.Call("get_env", "SUPABASE_URL");
        string supabaseKey = (string)envLoader.Call("get_env", "SUPABASE_KEY");

        // Creates a SupabaseOptions object to set up client behavior.
        // AutoConnectRealtime is set to true, which will connect Realtime on initialization.
        var options = new SupabaseOptions
        {
            AutoConnectRealtime = true
        };

        // Creates a new Supabase.Client using the URL, key, and options.
        // Internally, this configures Auth, Postgrest, Realtime, Functions, and Storage.
        _supabase = new Client(supabaseUrl, supabaseKey, options);

        // Loads any previously saved session from local storage, if present.
        LoadSessionFromDisk();

        // Completes initialization using the loaded tokens (if any).
        await _supabase.InitializeAsync();
    }




    /// <summary>
    /// Requests that Supabase send a one-time password (OTP) via SMS to the specified phone number.
    /// Does not immediately log the user in; the VerifyOtpAsync() method must be called after receiving the OTP.
    /// </summary>
    /// <param name="phoneNumber">A valid phone number in E.164 format (e.g., +1234567890).</param>
    public async Task RequestOtpAsync(string phoneNumber)
    {
        GD.Print("Requesting OTP for " + phoneNumber);
        try
        {
            // SignIn() with SignInType.Phone initiates an OTP flow.
            // An SMS code will be sent to the provided phoneNumber.
            await _supabase.Auth.SignIn(SignInType.Phone, phoneNumber);
            GD.Print("Requesting OTP for " + phoneNumber);
        }
        catch (Exception ex)
        {
            GD.PrintErr("Error while requesting OTP: " + ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Verifies the OTP code sent via SMS for the given phone number.
    /// Upon successful verification, establishes and saves a new session.
    /// Re-initializes or updates the Supabase client with the returned tokens for subsequent requests.
    /// </summary>
    /// <param name="phoneNumber">The same phone number used in RequestOtpAsync.</param>
    /// <param name="otpCode">The code received via SMS.</param>
    /// <returns>The newly obtained session if verification is successful; null otherwise.</returns>
    public async Task<Supabase.Gotrue.Session> VerifyOtpAsync(string phoneNumber, string otpCode)
    {
        GD.Print("Verifying OTP...");
        try
        {
            // Verifies the OTP against Supabase. If successful, a Session is returned.
            var session = await _supabase.Auth.VerifyOTP(phoneNumber, otpCode, MobileOtpType.SMS);

            bool userExists = session != null && session.User != null;
            if (userExists)
            {
                _currentSession = session;
                SaveSessionToDisk();

                GD.Print("OTP verification successful. Found user: " + _currentSession.User.Id);

                EmitSignal(SignalName.SessionChanged);
                return session;
            }

            GD.PrintErr("OTP verification did not return a valid session.");
            return null;
        }
        catch (Exception ex)
        {
            GD.PrintErr("Error while verifying OTP: " + ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Logs out of the current session if one is active.
    /// Removes the session data from memory and from local storage.
    /// Resets the Supabase client to a logged-out state.
    /// </summary>
    public async Task LogoutAsync()
    {
        GD.Print("Logging out...");
        try
        {
            bool hasActiveSession = _supabase.Auth.CurrentSession != null;
            if (hasActiveSession)
            {
                await _supabase.Auth.SignOut();
                GD.Print("Logout performed on Supabase side.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr("Server sign-out reported an error: " + ex.Message);
        }

        _currentSession = null;
        DeleteSessionFile();

        // After clearing the session file, re-initialize the client with no tokens.
        _ = await _supabase.InitializeAsync();
        GD.Print("Client re-initialized without tokens.");

        EmitSignal(SignalName.SessionChanged);
    }

    /// <summary>
    /// Indicates whether a valid session is currently held in memory.
    /// Returns true if the session and user objects are non-null.
    /// Token expiration checks can be added here for more advanced validation.
    /// </summary>
    public bool IsLoggedIn()
    {
        GD.Print("Checking login status...");
        return _currentSession != null && _currentSession.User != null;
    }

    /// <summary>
    /// Saves the current session to disk, allowing a seamless experience across app restarts.
    /// The session is stored in JSON format. Encryption or additional security steps can be added for production.
    /// </summary>
    private void SaveSessionToDisk()
    {
        GD.Print("Saving session to disk...");
        try
        {
            if (_currentSession == null)
            {
                DeleteSessionFile();
                return;
            }

            string sessionJson = JsonSerializer.Serialize(_currentSession);

            // Using Godot.FileAccess to store JSON into user://supabase_session.json 
            FileAccess file = FileAccess.Open(SESSION_FILE_PATH, FileAccess.ModeFlags.Write);
            file.StoreString(sessionJson);

            GD.Print("Session information saved to " + SESSION_FILE_PATH);
        }
        catch (Exception ex)
        {
            GD.PrintErr("Failed to save session to disk: " + ex.Message);
        }
    }


    /// <summary>
    /// Attempts to load an existing session from disk.
    /// If a valid session is found, it is placed in memory. However, the Supabase client
    /// itself is not re-initialized here; that occurs in InitializeSupabase().
    /// </summary>
    private void LoadSessionFromDisk()
    {
        _logger.Call("debug", "Trying to load session from disk...");

        try
        {
            // Use FileAccess.FileExists() to check existence
            if (!FileAccess.FileExists(SESSION_FILE_PATH))
            {
                _logger.Call("debug", "No session file found at " + SESSION_FILE_PATH);
                return;
            }

            // Open with FileAccess for reading
            using FileAccess file = FileAccess.Open(SESSION_FILE_PATH, FileAccess.ModeFlags.Read);

            GD.Print("Session file found. Loading session from " + SESSION_FILE_PATH);

            string sessionJson = file.GetAsText();


            if (!string.IsNullOrEmpty(sessionJson))
            {
                var sessionFromDisk = JsonSerializer.Deserialize<Supabase.Gotrue.Session>(sessionJson);

                GD.Print(sessionFromDisk);

                bool validTokens = sessionFromDisk != null && !string.IsNullOrEmpty(sessionFromDisk.AccessToken);

                if (validTokens)
                {
                    _currentSession = sessionFromDisk;
                    GD.Print("Session successfully loaded from " + SESSION_FILE_PATH);
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr("Failed to load session from disk: " + ex.Message);
        }
    }
    /// <summary>
    /// Deletes the local session file if it exists, ensuring that the session cannot be reused after logout.
    /// </summary>
    private void DeleteSessionFile()
    {
        GD.Print("Deleting session file...");
        try
        {
            // Check existence using FileAccess.FileExists() 
            if (Godot.FileAccess.FileExists(SESSION_FILE_PATH))
            {
                // Use DirAccess to remove the file from the user:// directory
                DirAccess dir = DirAccess.Open("user://");
                if (dir != null)
                {
                    // Our file name is the part after user://
                    // e.g. "user://supabase_session.json" => "supabase_session.json"
                    string fileName = "supabase_session.json";
                    dir.Remove(fileName);
                    GD.Print("Session file removed: " + SESSION_FILE_PATH);
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr("Failed to delete session file: " + ex.Message);
        }
    }
}


