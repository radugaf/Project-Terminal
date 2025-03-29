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
using ProjectTerminal.Globals.Interfaces;
using ProjectTerminal.Globals.Wrappers;

public partial class SupabaseClient : Node
{
    private Logger _logger;
    private Supabase.Client _supabase;
    private SupabaseOptions _options;
    private ISupabaseClientWrapper _clientWrapper;

    // Original properties - keep these for backward compatibility
    public Supabase.Client Supabase => _supabase;
    public IGotrueClient<User, Session> Auth => _supabase?.Auth;

    // New property to expose the wrapper
    public ISupabaseClientWrapper ClientWrapper => _clientWrapper;

    [Signal]
    public delegate void ClientInitializedEventHandler();
    [Signal]
    public delegate void ClientInitializationFailedEventHandler(string errorMessage);

    public override void _Ready()
    {
        // Get a reference to the logger
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("SupabaseClient: Initializing...");
        CallDeferred(nameof(AutoInitialize));
    }

    private async void AutoInitialize()
    {
        try
        {
            await InitializeClientAsync();

            // Create the wrapper only after successful initialization
            _clientWrapper = new SupabaseClientWrapper(_supabase, _logger);
            _logger.Debug("SupabaseClient: Client wrapper created");
        }
        catch (Exception ex)
        {
            _logger.Call("critical", $"SupabaseClient: Auto-initialization failed: {ex.Message}");
        }
    }

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
            _logger.Debug("SupabaseClient: Supabase client created");

            // Initialize Supabase client
            await _supabase.InitializeAsync();
            _logger.Info("SupabaseClient: Supabase client initialized");

            EmitSignal(SignalName.ClientInitialized);
        }
        catch (Exception ex)
        {
            _logger.Call("critical", $"SupabaseClient: Failed to initialize Supabase client: {ex.Message}");
            EmitSignal(SignalName.ClientInitializationFailed, ex.Message);
            throw;
        }
    }

    public async Task ReinitializeClientAsync()
    {
        if (_supabase != null)
        {
            _logger.Debug("SupabaseClient: Reinitializing Supabase client");
            await _supabase.InitializeAsync();
            _logger.Debug("SupabaseClient: Supabase client reinitialized");
        }
    }

    // Keep original methods for backward compatibility
    public ISupabaseTable<T, RealtimeChannel> From<T>() where T : BaseModel, new()
    {
        if (_clientWrapper == null)
        {
            _logger.Error("SupabaseClient: Attempted to access database before initialization");
            throw new InvalidOperationException("Supabase client not initialized");
        }

        return _clientWrapper.From<T>();
    }

    public async Task<BaseResponse> Rpc(string procedureName, object parameters)
    {
        if (_clientWrapper == null)
        {
            _logger.Error("SupabaseClient: Attempted to call RPC before initialization");
            throw new InvalidOperationException("Supabase client not initialized");
        }

        return await _clientWrapper.Rpc(procedureName, parameters);
    }
}
