using Godot;
using System;
using System.Threading.Tasks;
using ProjectTerminal.Resources;
using Supabase.Postgrest.Responses;
using Supabase.Postgrest;
using Godot.Collections;

public partial class TerminalManager : Node
{
    private Logger _logger;
    private SecureStorage _secureStorage;
    private SupabaseClient _supabaseClient;
    private AuthManager _authManager;
    private Node _deviceManager;


    [Signal]
    public delegate void TerminalCreatedEventHandler(string terminalId);

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("TerminalManager: Initializing");

        _secureStorage = GetNode<SecureStorage>("/root/SecureStorage");
        _deviceManager = GetNode<Node>("/root/DeviceManager");
        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");
        _authManager = GetNode<AuthManager>("/root/AuthManager");
    }

    public async Task<Terminal> CreateTerminalAsync(string organizationId, string locationId, string terminalName, TerminalType terminalType)
    {
        try
        {
            if (!_authManager.IsLoggedIn())
            {
                _logger.Error("TerminalManager: Cannot create terminal - user not logged in");
                throw new InvalidOperationException("User must be logged in to create a terminal");
            }

            _logger.Debug($"TerminalManager: Creating terminal '{terminalName}' for location {locationId}");

            // Get device information from DeviceManager
            Dictionary deviceInfo = _deviceManager.Call("get_device_info").AsGodotDictionary();
            Dictionary basicInfo = deviceInfo["basic_info"].AsGodotDictionary();
            Dictionary screenInfo = deviceInfo["screen_info"].AsGodotDictionary();
            Dictionary networkInfo = deviceInfo["network_info"].AsGodotDictionary();
            Dictionary hardwareInfo = deviceInfo["hardware_info"].AsGodotDictionary();

            // Create the terminal with actual device information
            var terminal = new Terminal
            {
                OrganizationId = organizationId,
                LocationId = locationId,
                TerminalName = terminalName,
                TerminalType = terminalType.ToString().ToLower(),
                DeviceId = basicInfo["device_unique_id"].AsString(),
                Active = true,
                RegisteredBy = _authManager.CurrentUser.Id,
                DeviceName = basicInfo["device_name"].AsString(),
                DeviceModel = basicInfo["device_model"].AsString(),
                DeviceOs = basicInfo["device_os_name"].AsString(),
                DeviceOsVersion = basicInfo["device_os_version"].AsString(),
                ProcessorType = hardwareInfo["processor_name"].AsString(),
                IpAddress = networkInfo["ip_address"].AsString(),
                MacAddress = networkInfo["mac_address"].AsString(),
                ScreenDpi = screenInfo["screen_dpi"].AsString(),
                ScreenOrientation = screenInfo["screen_orientation"].AsString(),
                IsTouchscreen = screenInfo["is_touchscreen"].AsBool(),
                ScreenScale = screenInfo["screen_scale"].AsString(),
            };

            ModeledResponse<Terminal> response = await _supabaseClient.GetClient().From<Terminal>()
                .Insert(terminal, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

            if (response == null || response.ResponseMessage.IsSuccessStatusCode != true)
            {
                _logger.Error($"TerminalManager: Failed to create terminal: {response?.ResponseMessage.ReasonPhrase}");
                throw new Exception("Failed to create terminal");
            }

            string terminalId = response.Model?.Id;
            _logger.Info($"TerminalManager: Terminal created with ID: {terminalId}");

            EmitSignal(SignalName.TerminalCreated, terminalId);
            return response.Model;
        }
        catch (Exception ex)
        {
            _logger.Error($"TerminalManager: Failed to create terminal: {ex.Message}");
            throw;
        }
    }

    private void SetupDependencies()
    {

    }

    public static string RunTerminalDiagnostics()
    {
        var diagnostics = new System.Text.StringBuilder();
        diagnostics.AppendLine("=== TERMINAL MANAGER DIAGNOSTICS ===");
        diagnostics.AppendLine($"Current Time (UTC): {DateTime.UtcNow}");

        try
        {
            diagnostics.AppendLine($"Device Name: {OS.GetName()}");
            diagnostics.AppendLine($"Device Model: {OS.GetModelName()}");
            diagnostics.AppendLine($"Unique Device ID: {OS.GetUniqueId()}");
        }
        catch (Exception ex)
        {
            diagnostics.AppendLine($"DIAGNOSTIC ERROR: {ex.Message}");
        }

        diagnostics.AppendLine("=== TERMINAL DIAGNOSTICS COMPLETE ===");
        return diagnostics.ToString();
    }
}
