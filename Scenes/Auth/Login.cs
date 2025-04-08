using Godot;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ProjectTerminal.Globals.Services;
using Supabase.Gotrue;
public partial class Login : Control
{

    private Logger _logger;
    private AuthManager _authManager;
    // UI Elements
    private LineEdit _emailOrPhoneLineEdit;
    private MarginContainer _passwordContainer;
    private LineEdit _passwordLineEdit;
    private MarginContainer _otpCodeContainer;
    private LineEdit _otpCodeLineEdit;
    private CheckBox _rememberMeCheckBox;
    private MarginContainer _statusContainer;
    private Label _statusLabel;
    private Button _loginButton;
    private LinkButton _registerLinkButton;
    private MarginContainer _progressContainer;
    private ProgressBar _progressBar;

    // State tracking
    private enum LoginMode { Initial, Email, Phone, OtpVerification }
    private LoginMode _currentMode = LoginMode.Initial;
    private bool _isProcessing = false;

    // Regex patterns for validation
    private static readonly Regex _emailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    private static readonly Regex _phoneRegex = new(@"^\+407\d{8}$"); // Romanian mobile format

    // Animation constants
    private const float TRANSITION_DURATION = 0.3f;
    private const float FOCUS_ANIMATION_DURATION = 0.2f;
    private const float STATUS_ANIMATION_DURATION = 0.15f;
    private const float ELEMENT_CASCADE_DELAY = 0.1f;

    // UI color constants
    private static readonly Color COLOR_FOCUS = new(1.1f, 1.1f, 1.2f);
    private static readonly Color COLOR_NORMAL = new(1, 1, 1);
    private static readonly Color COLOR_ERROR = new(0.9f, 0.3f, 0.3f);
    private static readonly Color COLOR_SUCCESS = new(0.3f, 0.9f, 0.3f);
    private static readonly Color COLOR_TRANSPARENT = new(1, 1, 1, 0);

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        InitializeServices();
        GetUIReferences();
        ConnectSignals();
        SetInitialState();
    }

    private void InitializeServices()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("LoginTwo: Initializing login screen");

        _authManager = GetNode<AuthManager>("/root/AuthManager");
    }

    private void GetUIReferences()
    {
        _emailOrPhoneLineEdit = GetNode<LineEdit>("%EmailOrPhoneLineEdit");
        _passwordContainer = GetNode<MarginContainer>("%PasswordContainer");
        _passwordLineEdit = GetNode<LineEdit>("%PasswordLineEdit");
        _otpCodeContainer = GetNode<MarginContainer>("%OtpCodeContainer");
        _otpCodeLineEdit = GetNode<LineEdit>("%OtpCodeLineEdit");
        _rememberMeCheckBox = GetNode<CheckBox>("%RememberMeCheckBox");
        _statusContainer = GetNode<MarginContainer>("%StatusContainer");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _loginButton = GetNode<Button>("%LoginButton");
        _registerLinkButton = GetNode<LinkButton>("%RegisterLinkButton");
        _progressContainer = GetNode<MarginContainer>("%ProgressContainer");
        _progressBar = GetNode<ProgressBar>("%ProgressBar");
    }

    private void ConnectSignals()
    {
        // Connect text change and focus events
        _emailOrPhoneLineEdit.TextChanged += OnEmailOrPhoneTextChanged;
        _emailOrPhoneLineEdit.FocusEntered += () => AnimateControlFocus(_emailOrPhoneLineEdit, true);
        _emailOrPhoneLineEdit.FocusExited += OnEmailOrPhoneFocusExited;

        _passwordLineEdit.FocusEntered += () => AnimateControlFocus(_passwordLineEdit, true);
        _passwordLineEdit.FocusExited += () => AnimateControlFocus(_passwordLineEdit, false);

        _otpCodeLineEdit.FocusEntered += () => AnimateControlFocus(_otpCodeLineEdit, true);
        _otpCodeLineEdit.FocusExited += () => AnimateControlFocus(_otpCodeLineEdit, false);

        // Connect button signals
        _loginButton.Pressed += OnLoginButtonPressed;
        _registerLinkButton.Pressed += () => NavigateToScene("res://Scenes/Register.tscn");
    }

    private void OnEmailOrPhoneFocusExited()
    {
        AnimateControlFocus(_emailOrPhoneLineEdit, false);
        ValidateEmailOrPhone();
    }

    private void SetInitialState()
    {
        _currentMode = LoginMode.Initial;
        _emailOrPhoneLineEdit.PlaceholderText = "Email or Phone (+40...)";

        // Initialize all elements hidden
        _passwordContainer.Visible = false;
        _otpCodeContainer.Visible = false;
        _statusContainer.Visible = false;
        _progressContainer.Visible = false;

        // Set initial button state
        _loginButton.Text = "Continue";
        _loginButton.Disabled = string.IsNullOrWhiteSpace(_emailOrPhoneLineEdit.Text);
        _isProcessing = false;
    }

    private void OnEmailOrPhoneTextChanged(string newText)
    {
        _loginButton.Disabled = string.IsNullOrWhiteSpace(newText);

        // Auto-detect input type and switch modes
        if (newText.StartsWith("+40"))
        {
            if (_phoneRegex.IsMatch(newText))
            {
                TransitionToPhoneMode();
            }
        }
        else if (newText.Contains('@'))
        {
            if (_emailRegex.IsMatch(newText))
            {
                TransitionToEmailMode();
            }
        }

        // Clear status when user types
        if (!string.IsNullOrEmpty(_statusLabel.Text))
        {
            AnimateElement(_statusContainer, false);
        }
    }

    private async void AnimateElement(Control element, bool visible, float delay = 0f)
    {
        // Skip if element is already in desired state
        if ((element.Visible && visible) || (!element.Visible && !visible))
            return;

        // Apply optional delay
        if (delay > 0)
            await ToSignal(GetTree().CreateTimer(delay), "timeout");

        // Make element visible immediately if transitioning to visible
        if (visible)
            element.Visible = true;

        // Create tween for smooth transition
        var tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

        if (visible)
        {
            element.Modulate = COLOR_TRANSPARENT;
            tween.TweenProperty(element, "modulate", COLOR_NORMAL, TRANSITION_DURATION);
        }
        else
        {
            tween.TweenProperty(element, "modulate", COLOR_TRANSPARENT, TRANSITION_DURATION);
        }

        await ToSignal(tween, "finished");

        // Hide element after animation if transitioning to hidden
        if (!visible)
            element.Visible = false;
    }

    private void AnimateControlFocus(Control control, bool focused)
    {
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(control, "modulate", focused ? COLOR_FOCUS : COLOR_NORMAL, FOCUS_ANIMATION_DURATION);
    }

    private void ValidateEmailOrPhone()
    {
        string input = _emailOrPhoneLineEdit.Text.Trim();

        if (string.IsNullOrWhiteSpace(input))
            return;

        if (_emailRegex.IsMatch(input))
        {
            TransitionToEmailMode();
        }
        else if (_phoneRegex.IsMatch(input))
        {
            TransitionToPhoneMode();
        }
        else
        {
            UpdateStatus("Please enter a valid email or Romanian phone number (+407xxxxxxxx)", true);
        }
    }

    private void TransitionToEmailMode()
    {
        if (_currentMode == LoginMode.Email)
            return;

        _currentMode = LoginMode.Email;

        // Show password field with animation
        AnimateElement(_passwordContainer, true);
        AnimateElement(_otpCodeContainer, false);

        _loginButton.Text = "Login";
        _logger.Debug("LoginTwo: Switched to email login mode");
    }

    private void TransitionToPhoneMode()
    {
        if (_currentMode == LoginMode.Phone)
            return;

        _currentMode = LoginMode.Phone;

        // Hide password field for phone mode
        AnimateElement(_passwordContainer, false);
        AnimateElement(_otpCodeContainer, false);

        _loginButton.Text = "Send OTP";
        _logger.Debug("LoginTwo: Switched to phone login mode");
    }

    private void TransitionToOtpVerificationMode()
    {
        _currentMode = LoginMode.OtpVerification;

        // Show OTP field with animation
        AnimateElement(_otpCodeContainer, true);
        _emailOrPhoneLineEdit.Editable = false;

        _loginButton.Text = "Verify";
        _logger.Debug("LoginTwo: Switched to OTP verification mode");
    }

    private async void OnLoginButtonPressed()
    {
        if (_isProcessing)
            return;

        _isProcessing = true;
        ShowProgress(true);

        try
        {
            string input = _emailOrPhoneLineEdit.Text.Trim();

            switch (_currentMode)
            {
                case LoginMode.Initial:
                    ValidateEmailOrPhone();
                    break;

                case LoginMode.Email:
                    await HandleEmailLogin(input);
                    break;

                case LoginMode.Phone:
                    await HandlePhoneOtpRequest(input);
                    break;

                case LoginMode.OtpVerification:
                    await HandleOtpVerification(input);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"LoginTwo: Error during login: {ex.Message}");
            UpdateStatus($"An error occurred: {ex.Message}", true);
        }
        finally
        {
            _isProcessing = false;
            ShowProgress(false);
        }
    }

    private async Task HandleEmailLogin(string email)
    {
        string password = _passwordLineEdit.Text;

        if (string.IsNullOrEmpty(password))
        {
            UpdateStatus("Please enter your password", true);
            return;
        }

        _logger.Info($"LoginTwo: Processing email login for {email}");

        try
        {
            // Use AuthManager to handle email login
            var session = await _authManager.LoginWithEmailAsync(email, password, _rememberMeCheckBox.ButtonPressed);

            if (session != null)
            {
                UpdateStatus("Login successful!", false);
                await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
                NavigateAfterLogin();
            }
            else
            {
                UpdateStatus("Login failed. Please check your credentials.", true);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Login failed: {ex.Message}", true);
            throw;
        }
    }

    private async Task HandlePhoneOtpRequest(string phone)
    {
        _logger.Info($"LoginTwo: Requesting OTP for {phone}");

        try
        {
            // Use AuthManager to handle OTP request
            await _authManager.RequestLoginOtpAsync(phone);

            TransitionToOtpVerificationMode();
            UpdateStatus("OTP code sent! Please check your phone.", false);
        }
        catch (Exception ex)
        {
            UpdateStatus($"Failed to send OTP: {ex.Message}", true);
            throw;
        }
    }

    private async Task HandleOtpVerification(string phone)
    {
        string otpCode = _otpCodeLineEdit.Text.Trim();

        if (string.IsNullOrEmpty(otpCode) || otpCode.Length < 6)
        {
            UpdateStatus("Please enter a valid OTP code", true);
            return;
        }

        _logger.Info($"LoginTwo: Verifying OTP for {phone}");

        try
        {
            // Use AuthManager to verify OTP
            var session = await _authManager.VerifyLoginOtpAsync(
                phone,
                otpCode,
                _rememberMeCheckBox.ButtonPressed
            );

            if (session != null)
            {
                UpdateStatus("Login successful!", false);
                await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
                NavigateAfterLogin();
            }
            else
            {
                UpdateStatus("Verification failed. Please try again.", true);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Verification failed: {ex.Message}", true);
            throw;
        }
    }

    private void UpdateStatus(string message, bool isError)
    {
        _statusLabel.Text = message;

        // Apply color based on error state
        Color targetColor = isError ? COLOR_ERROR : COLOR_SUCCESS;
        _statusLabel.AddThemeColorOverride("font_color", targetColor);

        // Show status container with animation
        AnimateElement(_statusContainer, true);

        // Add a subtle bounce effect for emphasis
        var tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_statusContainer, "scale", new Vector2(1.05f, 1.05f), STATUS_ANIMATION_DURATION);
        tween.TweenProperty(_statusContainer, "scale", new Vector2(1f, 1f), STATUS_ANIMATION_DURATION);

        _logger.Debug($"LoginTwo: Status updated - {message}");
    }

    private void ShowProgress(bool show)
    {
        if (show && !_progressContainer.Visible)
        {
            _progressContainer.Modulate = COLOR_TRANSPARENT;
            _progressContainer.Visible = true;

            var tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_progressContainer, "modulate", COLOR_NORMAL, TRANSITION_DURATION);
        }
        else if (!show && _progressContainer.Visible)
        {
            var tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_progressContainer, "modulate", COLOR_TRANSPARENT, TRANSITION_DURATION);
            tween.TweenCallback(Callable.From(() => _progressContainer.Visible = false));
        }

        _loginButton.Disabled = show;
    }

    private void NavigateAfterLogin()
    {
        _logger.Info("LoginTwo: Login successful, navigating to appropriate screen");

        if (_authManager.IsNewUser)
        {
            NavigateToScene("res://Scenes/Onboarding/BrandNewUser.tscn");
        }
        else
        {
            NavigateToScene("res://Scenes/Home.tscn");
        }
    }

    private void NavigateToScene(string scenePath)
    {
        GetTree().ChangeSceneToFile(scenePath);
    }
}
