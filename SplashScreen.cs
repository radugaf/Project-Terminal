using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectTerminal.Globals.Services;

public partial class SplashScreen : Control
{
    private Logger _logger;
    private ProgressBar _progressBar;
    private Label _statusLabel;
    private AuthManager _authManager;
    private SupabaseClient _supabaseClient;

    // Step tracking
    private readonly List<(string Message, float Progress)> _steps = new()
    {
        ("Initializing application...", 0.1f),
        ("Connecting to services...", 0.3f),
        ("Checking session status...", 0.5f),
        ("Authenticating...", 0.8f),
        ("Preparing application...", 0.9f),
        ("Ready!", 1.0f)
    };

    private int _currentStep = 0;
    private Timer _minimumDisplayTimer;

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _progressBar = GetNode<ProgressBar>("%ProgressBar");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _authManager = GetNode<AuthManager>("/root/AuthManager");
        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");

        _logger.Info("SplashScreen: Initializing");

        // Set initial progress
        _progressBar.Value = 0;
        UpdateStatus(0);

        // Create minimum display timer to ensure splash screen shows for at least a second
        _minimumDisplayTimer = new Timer();
        _minimumDisplayTimer.WaitTime = 1.0f;
        _minimumDisplayTimer.OneShot = true;
        AddChild(_minimumDisplayTimer);
        _minimumDisplayTimer.Start();

        // Start the initialization process
        CallDeferred(nameof(StartInitialization));
    }

    private async void StartInitialization()
    {
        try
        {
            // Step 1: Initialize application
            // UpdateStatus(0);
            // await Task.Delay(300); // Brief delay for visual effect

            // Step 2: Connect to services
            UpdateStatus(1);

            // Ensure Supabase client is initialized
            _logger.Info("SplashScreen: Supabase client is initialized: " + _supabaseClient.IsInitialized);
            if (!_supabaseClient.IsInitialized)
            {
                try
                {
                    await _supabaseClient.InitializeClientAsync();
                }
                catch (Exception ex)
                {
                    _logger.Warn($"SplashScreen: Supabase initialization failed, proceeding anyway: {ex.Message}");
                }
            }
            // await Task.Delay(300);

            // Step 3: Check session status
            UpdateStatus(2);
            bool hasSession = _authManager.CurrentSession != null;
            _logger.Debug($"SplashScreen: Session check - HasSession: {hasSession}");
            // await Task.Delay(300);

            // Step 4: Authenticate if session exists
            UpdateStatus(3);
            bool isAuthenticated = false;

            if (hasSession)
            {
                try
                {
                    isAuthenticated = await _authManager.RefreshSessionAsync();
                    _logger.Info($"SplashScreen: Authentication refresh result: {isAuthenticated}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"SplashScreen: Authentication refresh failed: {ex.Message}");
                }
            }
            // await Task.Delay(300);

            // Step 5: Prepare application
            // UpdateStatus(4);
            // await Task.Delay(300);

            // Step 6: Ready!
            // UpdateStatus(5);

            // Wait for minimum display time to complete
            if (_minimumDisplayTimer.TimeLeft > 0)
            {
                await ToSignal(_minimumDisplayTimer, Timer.SignalName.Timeout);
            }

            // Navigate to appropriate screen
            GoToNextScreen(isAuthenticated);
        }
        catch (Exception ex)
        {
            _logger.Error($"SplashScreen: Initialization error: {ex.Message}");
            UpdateStatus(-1, $"Error: {ex.Message}");

            // Still proceed to login after a delay on error
            await ToSignal(GetTree().CreateTimer(3.0f), Timer.SignalName.Timeout);
            GoToNextScreen(false);
        }
    }

    private void UpdateStatus(int stepIndex, string overrideMessage = null)
    {
        _currentStep = stepIndex;

        if (stepIndex >= 0 && stepIndex < _steps.Count)
        {
            (string message, float progress) = _steps[stepIndex];
            _statusLabel.Text = overrideMessage ?? message;
            _progressBar.Value = progress;
            _logger.Debug($"SplashScreen: {_statusLabel.Text} ({progress * 100}%)");
        }
        else
        {
            _statusLabel.Text = overrideMessage ?? "Error occurred";
            _statusLabel.AddThemeColorOverride("font_color", new Color(1, 0.3f, 0.3f));
            _logger.Error($"SplashScreen: Invalid step index: {stepIndex}");
        }
    }

    private void GoToNextScreen(bool isAuthenticated)
    {
        _logger.Info($"SplashScreen: Navigation - IsAuthenticated: {isAuthenticated}, IsNewUser: {_authManager.IsNewUser}");

        if (isAuthenticated)
        {
            if (_authManager.IsNewUser)
            {
                _logger.Info("SplashScreen: Navigating to BrandNewUser");
                GetTree().ChangeSceneToFile("res://Scenes/Onboarding/BrandNewUser.tscn");
            }
            else
            {
                _logger.Info("SplashScreen: Navigating to Home");
                GetTree().ChangeSceneToFile("res://Scenes/Home.tscn");
            }
        }
        else
        {
            _logger.Info("SplashScreen: Navigating to Login");
            GetTree().ChangeSceneToFile("res://Scenes/Auth/Login.tscn");
        }
    }
}
