using Godot;
using System;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Realtime;
using System.Threading.Tasks;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;
using Supabase.Interfaces;

/// <summary>
/// Manages the Supabase client connection and provides access to Supabase services.
/// Acts as a centralized point for all Supabase-related operations.
/// </summary>
public partial class SupabaseClient : Node
{
    #region Constants and Fields

    /// <summary>
    /// The Supabase client instance used for all API operations.
    /// </summary>
    private Supabase.Client _supabase;

    /// <summary>
    /// Reference to the application logger.
    /// </summary>
    private Node _logger;

    /// <summary>
    /// Supabase configuration options.
    /// </summary>
    private SupabaseOptions _options;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the Supabase client instance.
    /// </summary>
    public Supabase.Client Supabase => _supabase;

    /// <summary>
    /// Gets the Supabase Auth client.
    /// </summary>
    public IGotrueClient<User, Session> Auth => _supabase?.Auth;

    #endregion

    #region Signals

    /// <summary>
    /// Emitted when the Supabase client is fully initialized.
    /// </summary>
    [Signal]
    public delegate void ClientInitializedEventHandler();

    /// <summary>
    /// Emitted when the Supabase client initialization fails.
    /// </summary>
    [Signal]
    public delegate void ClientInitializationFailedEventHandler(string errorMessage);

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the SupabaseClient node, reads configuration, and creates the Supabase client.
    /// </summary>
    public override void _Ready()
    {
        // Get a reference to the logger
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "SupabaseClient: Initializing SupabaseClient...");
    }

    #endregion

    #region Client Initialization

    /// <summary>
    /// Initializes the Supabase client with the given credentials.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task InitializeClientAsync()
    {
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
                EmitSignal(SignalName.ClientInitializationFailed, "Supabase URL or key not found in environment variables");
                throw new InvalidOperationException("Supabase URL or key not found in environment variables");
            }

            // Create Supabase client
            _options = new SupabaseOptions
            {
                AutoConnectRealtime = true,
                AutoRefreshToken = true
            };

            _supabase = new Supabase.Client(supabaseUrl, supabaseKey, _options);
            _logger.Call("debug", "SupabaseClient: Supabase client created");

            // Initialize Supabase client
            await _supabase.InitializeAsync();
            _logger.Call("info", "SupabaseClient: Supabase client initialized");

            EmitSignal(SignalName.ClientInitialized);
        }
        catch (Exception ex)
        {
            _logger.Call("critical", $"SupabaseClient: Failed to initialize Supabase client: {ex.Message}");
            EmitSignal(SignalName.ClientInitializationFailed, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Reinitializes the Supabase client, typically after session changes.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task ReinitializeClientAsync()
    {
        if (_supabase != null)
        {
            _logger.Call("debug", "SupabaseClient: Reinitializing Supabase client");
            await _supabase.InitializeAsync();
            _logger.Call("debug", "SupabaseClient: Supabase client reinitialized");
        }
    }

    #endregion

    #region Database Operations

    /// <summary>
    /// Gets a reference to a database table for querying.
    /// </summary>
    public ISupabaseTable<T, RealtimeChannel> From<T>() where T : BaseModel, new()
    {
        if (_supabase == null)
        {
            _logger.Call("error", "SupabaseClient: Attempted to access database before initialization");
            throw new InvalidOperationException("Supabase client not initialized");
        }

        return _supabase.From<T>();
    }

    /// <summary>
    /// Executes a stored procedure in the database.
    /// </summary>
    public async Task<BaseResponse> Rpc(string procedureName, object parameters)
    {
        if (_supabase == null)
        {
            _logger.Call("error", "SupabaseClient: Attempted to call RPC before initialization");
            throw new InvalidOperationException("Supabase client not initialized");
        }

        return await _supabase.Rpc(procedureName, parameters);
    }

    #endregion
}
