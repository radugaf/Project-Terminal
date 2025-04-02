using Godot;
using System;
using Supabase;
using System.Threading.Tasks;
using ProjectTerminal.Globals.Interfaces;
using ProjectTerminal.Globals.Wrappers;

public partial class SupabaseClient : Node, ISupabaseClientWrapper
{
    private Logger _logger;
    private Client _supabase;
    private ISupabaseClientWrapper _wrapper;

    public bool IsInitialized => _wrapper?.IsInitialized ?? false;

    [Signal]
    public delegate void ClientInitializedEventHandler();
    [Signal]
    public delegate void ClientInitializationFailedEventHandler(string errorMessage);

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("SupabaseClient: Initializing...");
        CallDeferred(nameof(AutoInitialize));
    }

    private async void AutoInitialize()
    {
        try
        {
            await InitializeClientAsync();
        }
        catch (Exception ex)
        {
            _logger.Critical($"SupabaseClient: Auto-initialization failed: {ex.Message}");
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
                string errorMsg = "Supabase URL or key not found in environment variables";
                _logger.Critical(errorMsg);
                EmitSignal(SignalName.ClientInitializationFailed, errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            // Create and configure Supabase client
            _supabase = new Client(supabaseUrl, supabaseKey, new SupabaseOptions
            {
                AutoConnectRealtime = true,
                AutoRefreshToken = true
            });

            // Create the wrapper
            _wrapper = new SupabaseClientWrapper(_supabase, _logger);

            // Initialize the client
            await _wrapper.Initialize();

            _logger.Info("SupabaseClient: Initialization complete");
            EmitSignal(SignalName.ClientInitialized);
        }
        catch (Exception ex)
        {
            _logger.Critical($"SupabaseClient: Failed to initialize: {ex.Message}");
            EmitSignal(SignalName.ClientInitializationFailed, ex.Message);
            throw;
        }
    }

    // For testing purposes
    public void InjectWrapper(ISupabaseClientWrapper wrapper) => _wrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));

    // Access to client for other managers
    public Client GetClient() => _wrapper?.GetClient() ??
        throw new InvalidOperationException("Supabase client is not initialized");

    // Implement interface methods
    public Task Initialize() => _wrapper?.Initialize() ??
        throw new InvalidOperationException("Wrapper is not initialized");
}
