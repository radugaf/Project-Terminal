using Godot;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ProjectTerminal.Globals.Services;
using Supabase.Gotrue;
public partial class Register : Control
{
    private Logger _logger;
    private AuthManager _authManager;

    // UI Elements
    private LineEdit _emailOrPhoneLineEdit;
    private LineEdit _passwordLineEdit;
    private LineEdit _confirmPasswordLineEdit;
    private LineEdit _otpCodeLineEdit;
    private Button _registerButton;
    private LinkButton _loginLinkButton;
    private MarginContainer _progressContainer;
    private ProgressBar _progressBar;
    private Label _statusLabel;
    private MarginContainer _passwordContainer;
    private MarginContainer _confirmPasswordContainer;
    private MarginContainer _otpContainer;
    private MarginContainer _statusContainer;

    // State tracking
    private enum RegistrationMode { Initial, Email, Phone, OtpVerification }
    private RegistrationMode _currentMode = RegistrationMode.Initial;
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
        _logger.Info("Register: Initializing registration screen");

        _authManager = GetNode<AuthManager>("/root/AuthManager");
    }

    private void GetUIReferences()
    {
        _emailOrPhoneLineEdit = GetNode<LineEdit>("%EmailOrPhoneLineEdit");
        _passwordLineEdit = GetNode<LineEdit>("%PasswordLineEdit");
        _confirmPasswordLineEdit = GetNode<LineEdit>("%ConfirmPasswordLineEdit");
        _otpCodeLineEdit = GetNode<LineEdit>("%OtpCodeLineEdit");
        _registerButton = GetNode<Button>("%RegisterButton");
        _loginLinkButton = GetNode<LinkButton>("%LoginLinkButton");
        _progressContainer = GetNode<MarginContainer>("%ProgressContainer");
        _progressBar = GetNode<ProgressBar>("%ProgressBar");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _passwordContainer = GetNode<MarginContainer>("%PasswordContainer");
        _confirmPasswordContainer = GetNode<MarginContainer>("%ConfirmPasswordContainer");
        _otpContainer = GetNode<MarginContainer>("%OtpCodeContainer");
        _statusContainer = GetNode<MarginContainer>("%StatusContainer");
    }

    private void ConnectSignals()
    {
        // Connect text change and focus events
        _emailOrPhoneLineEdit.TextChanged += OnEmailOrPhoneTextChanged;
        _emailOrPhoneLineEdit.FocusEntered += () => AnimateControlFocus(_emailOrPhoneLineEdit, true);
        _emailOrPhoneLineEdit.FocusExited += OnEmailOrPhoneFocusExited;

        _passwordLineEdit.FocusEntered += () => AnimateControlFocus(_passwordLineEdit, true);
        _passwordLineEdit.FocusExited += () => AnimateControlFocus(_passwordLineEdit, false);

        _confirmPasswordLineEdit.FocusEntered += () => AnimateControlFocus(_confirmPasswordLineEdit, true);
        _confirmPasswordLineEdit.FocusExited += () => AnimateControlFocus(_confirmPasswordLineEdit, false);

        _otpCodeLineEdit.FocusEntered += () => AnimateControlFocus(_otpCodeLineEdit, true);
        _otpCodeLineEdit.FocusExited += () => AnimateControlFocus(_otpCodeLineEdit, false);

        // Connect button signals
        _registerButton.Pressed += OnRegisterButtonPressed;
        _loginLinkButton.Pressed += () => NavigateToScene("res://Scenes/Auth/Login.tscn");
    }

    private void OnEmailOrPhoneTextChanged(string newText)
    {
        _registerButton.Disabled = string.IsNullOrWhiteSpace(newText);

        // Auto-detect input type and switch modes
        if (newText.StartsWith("+40"))
        {
            if (_phoneRegex.IsMatch(newText))
            {
                TransitionToPhoneMode();
            }
        }
        else if (newText.Contains("@"))
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

    private void OnEmailOrPhoneFocusExited()
    {
        AnimateControlFocus(_emailOrPhoneLineEdit, false);
        ValidateEmailOrPhone();
    }

    private void SetInitialState()
    {
        _currentMode = RegistrationMode.Initial;
        _emailOrPhoneLineEdit.PlaceholderText = "Email or Phone (+40...)";

        // Initialize all elements hidden
        _passwordContainer.Visible = false;
        _confirmPasswordContainer.Visible = false;
        _otpContainer.Visible = false;
        _statusContainer.Visible = false;
        _progressContainer.Visible = false;

        // Set initial button state
        _registerButton.Text = "Continue";
        _registerButton.Disabled = string.IsNullOrWhiteSpace(_emailOrPhoneLineEdit.Text);
        _isProcessing = false;
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
        if (_currentMode == RegistrationMode.Email)
            return;

        _currentMode = RegistrationMode.Email;

        // Smooth transitions with cascade
        AnimateElement(_passwordContainer, true);
        AnimateElement(_confirmPasswordContainer, true, ELEMENT_CASCADE_DELAY);
        AnimateElement(_otpContainer, false);

        _registerButton.Text = "Register";
        _logger.Debug("Register: Switched to email registration mode");
    }

    private void TransitionToPhoneMode()
    {
        if (_currentMode == RegistrationMode.Phone)
            return;

        _currentMode = RegistrationMode.Phone;

        // Hide password fields for phone mode
        AnimateElement(_passwordContainer, false);
        AnimateElement(_confirmPasswordContainer, false);
        AnimateElement(_otpContainer, false);

        _registerButton.Text = "Send OTP";
        _logger.Debug("Register: Switched to phone registration mode");
    }

    private void TransitionToOtpVerificationMode()
    {
        _currentMode = RegistrationMode.OtpVerification;

        // Show OTP field
        AnimateElement(_otpContainer, true);
        _emailOrPhoneLineEdit.Editable = false;

        _registerButton.Text = "Verify OTP";
        _logger.Debug("Register: Switched to OTP verification mode");
    }

    private async void OnRegisterButtonPressed()
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
                case RegistrationMode.Initial:
                    ValidateEmailOrPhone();
                    break;

                case RegistrationMode.Email:
                    await HandleEmailRegistration(input);
                    break;

                case RegistrationMode.Phone:
                    await HandlePhoneOtpRequest(input);
                    break;

                case RegistrationMode.OtpVerification:
                    await HandleOtpVerification(input);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Register: Error during registration: {ex.Message}");
            UpdateStatus($"An error occurred: {ex.Message}", true);
        }
        finally
        {
            _isProcessing = false;
            ShowProgress(false);
        }
    }

    private async Task HandleEmailRegistration(string email)
    {
        // Validate password fields
        if (!ValidatePasswordFields())
            return;

        string password = _passwordLineEdit.Text;

        _logger.Info($"Register: Processing email registration for {email}");

        try
        {
            // Use the existing AuthManager to handle registration
            Session session = await _authManager.RegisterWithEmailAsync(email, password, true);

            if (session != null)
            {
                UpdateStatus("Registration successful!", false);
                // Wait briefly to show the success message
                await ToSignal(GetTree().CreateTimer(1.5f), "timeout");
                NavigateToNextScreen();
            }
            else
            {
                UpdateStatus("Registration failed. Please try again.", true);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Registration failed: {ex.Message}", true);
            throw;
        }
    }

    private async Task HandlePhoneOtpRequest(string phone)
    {
        _logger.Info($"Register: Requesting OTP for {phone}");

        try
        {
            // Use the existing AuthManager to handle OTP request
            await _authManager.RequestLoginOtpAsync(phone);

            // Success path - transition to OTP verification
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

        _logger.Info($"Register: Verifying OTP for {phone}");

        try
        {
            // Use the existing AuthManager to verify OTP
            Session session = await _authManager.VerifyLoginOtpAsync(phone, otpCode, true);

            if (session != null)
            {
                UpdateStatus("Phone verification successful!", false);
                // Wait briefly to show the success message
                await ToSignal(GetTree().CreateTimer(1.5f), "timeout");
                NavigateToNextScreen();
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

    private bool ValidatePasswordFields()
    {
        string password = _passwordLineEdit.Text;
        string confirmPassword = _confirmPasswordLineEdit.Text;

        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            UpdateStatus("Password must be at least 8 characters long", true);
            return false;
        }

        if (password != confirmPassword)
        {
            UpdateStatus("Passwords do not match", true);
            return false;
        }

        return true;
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

        _logger.Debug($"Register: Status updated - {message}");
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

        _registerButton.Disabled = show;
    }

    private void NavigateToNextScreen()
    {
        // Navigate to the appropriate next screen based on user state
        _logger.Info("Register: Registration completed, navigating to next screen");

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
