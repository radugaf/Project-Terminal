using Godot;
using System;
using Supabase;
using System.Threading.Tasks;
using ProjectTerminal.Globals.Interfaces;
using ProjectTerminal.Globals.Wrappers;
using Supabase.Gotrue;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;
using Supabase.Realtime;
using static Supabase.Gotrue.Constants;
using Supabase.Interfaces;

public partial class SupabaseClient : Node, ISupabaseClientWrapper
{
    private Logger _logger;
    private Supabase.Client _supabase;
    private ISupabaseClientWrapper _wrapper;

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
            _supabase = new Supabase.Client(supabaseUrl, supabaseKey, new SupabaseOptions
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
    public void InjectWrapper(ISupabaseClientWrapper wrapper)
    {
        _wrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));
    }

    // Delegate all interface methods to the wrapper implementation
    public Task<Session> SignIn(string email, string password) => _wrapper.SignIn(email, password);
    public Task<Session> SignUp(string email, string password) => _wrapper.SignUp(email, password);
    public Task<Session> SignIn(SignInType type, string credential) => _wrapper.SignIn(type, credential);
    public Task<Session> VerifyOTP(string phone, string otpCode, MobileOtpType type) => _wrapper.VerifyOTP(phone, otpCode, type);
    public Task<Session> RefreshSession() => _wrapper.RefreshSession();
    public Task SignOut() => _wrapper.SignOut();
    public Task SetSession(string accessToken, string refreshToken) => _wrapper.SetSession(accessToken, refreshToken);
    public Task<User> Update(UserAttributes attributes) => _wrapper.Update(attributes);
    public ISupabaseTable<T, RealtimeChannel> From<T>() where T : BaseModel, new() => _wrapper.From<T>();
    public Task<BaseResponse> Rpc(string procedureName, object parameters) => _wrapper.Rpc(procedureName, parameters);
    public Task<TResponse> Rpc<TResponse>(string procedureName, object parameters) => _wrapper.Rpc<TResponse>(procedureName, parameters);
    public Task Initialize() => _wrapper.Initialize();
}
