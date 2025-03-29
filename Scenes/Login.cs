using Godot;
using System.Text.RegularExpressions;
using Supabase.Gotrue;

public partial class Login : Control
{
    private Node _logger;
    private AuthManager _authManager;

    // Email based auth - UI elements
    private CheckBox _rememberMeCheckBox;
    private LineEdit _loginEmailLineEdit;
    private LineEdit _loginPasswordLineEdit;
    private Button _loginButton;
    private LineEdit _registerEmailLineEdit;
    private LineEdit _registerPasswordLineEdit;
    private LineEdit _registerPasswordConfirmLineEdit;
    private Button _registerButton;

    // Phone based auth - UI elements
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

        // Get references to phone-based auth UI elements
        _rememberMeCheckBox = GetNode<CheckBox>("%RememberMeCheckBox");
        _phoneLineEdit = GetNode<LineEdit>("%PhoneLineEdit");
        _otpLabel = GetNode<Label>("%OtpLabel");
        _otpLineEdit = GetNode<LineEdit>("%OTPLineEdit");
        _requestOtpButton = GetNode<Button>("%RequestOtpButton");
        _verifyOtpButton = GetNode<Button>("%VerifyOtpButton");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _loadingBar = GetNode<ProgressBar>("%LoadingBar");

        // Get references to email-based auth UI elements
        _loginEmailLineEdit = GetNode<LineEdit>("%LoginEmailLineEdit");
        _loginPasswordLineEdit = GetNode<LineEdit>("%LoginPasswordLineEdit");
        _loginButton = GetNode<Button>("%LoginButton");
        _registerEmailLineEdit = GetNode<LineEdit>("%RegisterEmailLineEdit");
        _registerPasswordLineEdit = GetNode<LineEdit>("%RegisterPasswordLineEdit");
        _registerPasswordConfirmLineEdit = GetNode<LineEdit>("%RegisterPasswordConfirmLineEdit");
        _registerButton = GetNode<Button>("%RegisterButton");

        // Set initial UI state
        SetInitialState();

        // Get TerminalSessionManager
        _authManager = GetNode<AuthManager>("/root/AuthManager");

        // Connect to SessionChanged signal
        _authManager.Connect(AuthManager.SignalName.SessionChanged, new Callable(this, nameof(OnSessionChanged)));

        // Connect UI event handlers
        _requestOtpButton.Pressed += OnRequestOtpButtonPressed;
        _verifyOtpButton.Pressed += OnVerifyOtpButtonPressed;
        _registerButton.Pressed += OnRegisterButtonPressed;
        _loginButton.Pressed += OnLoginButtonPressed;

        // Check if already logged in
        if (_authManager.IsLoggedIn())
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
            await _authManager.RequestLoginOtpAsync(phoneNumber);

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
            Session session = await _authManager.VerifyLoginOtpAsync(phoneNumber, otpCode, _rememberMeCheckBox.ButtonPressed);

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

    private async void OnRegisterButtonPressed()
    {
        if (_isProcessing)
            return;

        string email = _registerEmailLineEdit.Text.Trim();
        string password = _registerPasswordLineEdit.Text.Trim();
        string confirmPassword = _registerPasswordConfirmLineEdit.Text.Trim();

        // Validate inputs
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        {
            UpdateStatus("Please enter a valid email address", true);
            return;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            UpdateStatus("Password must be at least 6 characters", true);
            return;
        }

        if (password != confirmPassword)
        {
            UpdateStatus("Passwords do not match", true);
            return;
        }

        try
        {
            _isProcessing = true;
            UpdateStatus("Registering account...", false);
            ShowLoading(true);

            _logger.Call("debug", $"Login: Registering new user with email {email}");
            Session session = await _authManager.RegisterWithEmailAsync(email, password, _rememberMeCheckBox.ButtonPressed);

            if (session != null)
            {
                _logger.Call("info", "Login: Registration successful");
                UpdateStatus("Registration successful!", false);

                // Wait briefly to show success message before transitioning
                await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
                GoToMainScreen();
            }
            else
            {
                _logger.Call("warn", "Login: Registration failed - null session returned");
                UpdateStatus("Registration failed. Please try again.", true);
            }
        }
        catch (System.Exception ex)
        {
            _logger.Call("error", $"Login: Registration failed: {ex.Message}");
            UpdateStatus($"Registration failed: {ex.Message}", true);
        }
        finally
        {
            _isProcessing = false;
            ShowLoading(false);
        }
    }

    private async void OnLoginButtonPressed()
    {
        if (_isProcessing)
            return;

        string email = _loginEmailLineEdit.Text.Trim();
        string password = _loginPasswordLineEdit.Text.Trim();

        // Validate inputs
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        {
            UpdateStatus("Please enter a valid email address", true);
            return;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            UpdateStatus("Password must be at least 6 characters", true);
            return;
        }

        try
        {
            _isProcessing = true;
            UpdateStatus("Logging in...", false);
            ShowLoading(true);

            _logger.Call("debug", $"Login: Logging in user with email {email}");
            Session session = await _authManager.LoginWithEmailAsync(email, password);

            if (session != null)
            {
                _logger.Call("info", "Login: Login successful");
                UpdateStatus("Login successful!", false);

                // Wait briefly to show success message before transitioning
                await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
                GoToMainScreen();
            }
            else
            {
                _logger.Call("warn", "Login: Login failed - null session returned");
                UpdateStatus("Login failed. Please try again.", true);
            }
        }
        catch (System.Exception ex)
        {
            _logger.Call("error", $"Login: Login failed: {ex.Message}");
            UpdateStatus($"Login failed: {ex.Message}", true);
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

        if (_authManager.IsLoggedIn())
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

        // Phone auth buttons
        _requestOtpButton.Disabled = show;
        _verifyOtpButton.Disabled = show;

        // Email auth buttons
        _loginButton.Disabled = show;
        _registerButton.Disabled = show;
    }

    private void GoToMainScreen()
    {
        GetTree().CallDeferred("change_scene_to_file", "res://Scenes/Home.tscn");
    }
}
