using Godot;
using System.Text.RegularExpressions;
using Supabase.Gotrue;

public partial class Login : Control
{
    private Node _logger;
    private TerminalSessionManager _terminalSessionManager;

    // UI elements
    private LineEdit _phoneLineEdit;
    private LineEdit _otpLineEdit;
    private Button _requestOtpButton;
    private Button _verifyOtpButton;
    private Label _statusLabel;
    private ProgressBar _loadingBar;
    private Label _otpLabel;

    // Input validation
    private static readonly Regex _phoneRegex = new(@"^\+[1-9]\d{1,14}$");

    // UI state tracking
    private bool _otpRequested = false;
    private bool _isProcessing = false;

    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "Login: Login scene initializing");

        // Get references to UI components
        _phoneLineEdit = GetNode<LineEdit>("%PhoneLineEdit");
        _otpLabel = GetNode<Label>("%OtpLabel");
        _otpLineEdit = GetNode<LineEdit>("%OTPLineEdit");
        _requestOtpButton = GetNode<Button>("%RequestOtpButton");
        _verifyOtpButton = GetNode<Button>("%VerifyOtpButton");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _loadingBar = GetNode<ProgressBar>("%LoadingBar");

        // Set initial UI state
        SetInitialState();

        // Get TerminalSessionManager
        _terminalSessionManager = GetNode<TerminalSessionManager>("/root/TerminalSessionManager");

        // Connect to SessionChanged signal
        _terminalSessionManager.Connect(TerminalSessionManager.SignalName.SessionChanged, new Callable(this, nameof(OnSessionChanged)));

        // Connect UI event handlers
        _requestOtpButton.Pressed += OnRequestOtpButtonPressed;
        _verifyOtpButton.Pressed += OnVerifyOtpButtonPressed;

        // Check if already logged in
        if (_terminalSessionManager.IsStaffLoggedIn())
        {
            _logger.Call("debug", "Login: User already logged in, transitioning to main scene");
            GoToMainScreen();
        }
    }

    private void SetInitialState()
    {
        _otpLabel.Visible = false;
        _otpLineEdit.Visible = false;
        _verifyOtpButton.Visible = false;
        _statusLabel.Text = "";
        _loadingBar.Visible = false;
        _otpRequested = false;
        _isProcessing = false;
    }

    private async void OnRequestOtpButtonPressed()
    {
        if (_isProcessing)
            return;

        string phoneNumber = _phoneLineEdit.Text.Trim();

        // Validate phone number format
        if (!_phoneRegex.IsMatch(phoneNumber))
        {
            UpdateStatus("Please enter a valid phone number in format: +[country code][number]", true);
            return;
        }

        try
        {
            _isProcessing = true;
            UpdateStatus("Requesting OTP...", false);
            ShowLoading(true);

            _logger.Call("debug", $"Login: Requesting OTP for {phoneNumber}");
            await _terminalSessionManager.RequestStaffLoginOtpAsync(phoneNumber);

            // Show OTP verification UI
            _otpLabel.Visible = true;
            _otpLineEdit.Visible = true;
            _verifyOtpButton.Visible = true;
            _requestOtpButton.Text = "Resend Code";
            _otpRequested = true;

            UpdateStatus("Verification code sent! Please check your phone.", false);
            _logger.Call("debug", "Login: OTP requested successfully");
        }
        catch (System.Exception ex)
        {
            _logger.Call("error", $"Login: OTP request failed: {ex.Message}");
            UpdateStatus($"Failed to request verification code: {ex.Message}", true);
        }
        finally
        {
            _isProcessing = false;
            ShowLoading(false);
        }
    }

    private async void OnVerifyOtpButtonPressed()
    {
        if (_isProcessing)
            return;

        string phoneNumber = _phoneLineEdit.Text.Trim();
        string otpCode = _otpLineEdit.Text.Trim();

        if (string.IsNullOrEmpty(otpCode) || otpCode.Length < 6)
        {
            UpdateStatus("Please enter the verification code from your phone", true);
            return;
        }

        try
        {
            _isProcessing = true;
            UpdateStatus("Verifying code...", false);
            ShowLoading(true);

            _logger.Call("debug", $"Login: Verifying OTP for {phoneNumber}");
            Session session = await _terminalSessionManager.VerifyStaffLoginOtpAsync(phoneNumber, otpCode);

            if (session != null)
            {
                _logger.Call("info", "Login: Authentication successful");
                UpdateStatus("Authentication successful!", false);

                // Wait briefly to show success message before transitioning
                await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
                GoToMainScreen();
            }
            else
            {
                _logger.Call("warn", "Login: Authentication failed - null session returned");
                UpdateStatus("Authentication failed. Please try again.", true);
            }
        }
        catch (System.Exception ex)
        {
            _logger.Call("error", $"Login: OTP verification failed: {ex.Message}");
            UpdateStatus($"Verification failed: {ex.Message}", true);
        }
        finally
        {
            _isProcessing = false;
            ShowLoading(false);
        }
    }

    private void OnSessionChanged()
    {
        _logger.Call("debug", "Login: Session changed signal received");

        if (_terminalSessionManager.IsStaffLoggedIn())
        {
            _logger.Call("debug", "Login: User now logged in, transitioning to main scene");
            GoToMainScreen();
        }
        else
        {
            _logger.Call("debug", "Login: User logged out");
            SetInitialState();
        }
    }

    private void UpdateStatus(string message, bool isError)
    {
        _statusLabel.Text = message;
        _statusLabel.AddThemeColorOverride("font_color", isError ? new Color(1, 0.3f, 0.3f) : new Color(0.3f, 1, 0.3f));
    }

    private void ShowLoading(bool show)
    {
        _loadingBar.Visible = show;
        _requestOtpButton.Disabled = show;
        _verifyOtpButton.Disabled = show;
    }

    private void GoToMainScreen()
    {
        GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Home.tscn");
    }
}
