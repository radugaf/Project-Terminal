using Godot;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>
/// Handles user authentication through OTP (One-Time Password) verification.
/// Manages the login UI and authentication flow for the POS terminal.
/// </summary>
public partial class Login : Control
{
    // UI elements
    private LineEdit _phoneLineEdit;
    private LineEdit _otpLineEdit;
    private Button _requestOtpButton;
    private Button _verifyOtpButton;
    private Label _statusLabel;
    private ProgressBar _loadingBar;

    // Service references
    private Node _logger;
    private UserSessionManager _sessionManager;

    // Input validation
    /// <summary>
    /// Regular expression for validating phone numbers in E.164 format (+[country code][number]).
    /// This is required by Supabase for phone-based authentication.
    /// </summary>
    private static readonly Regex _phoneRegex = new(@"^\+[1-9]\d{1,14}$");

    // UI state tracking
    private bool _otpRequested = false;
    private bool _isProcessing = false;

    /// <summary>
    /// Initializes the login screen, sets up UI components and event handlers,
    /// and checks for existing sessions.
    /// </summary>
    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "Login scene initializing");

        // Get references to UI components
        _phoneLineEdit = GetNode<LineEdit>("%PhoneLineEdit");
        _otpLineEdit = GetNode<LineEdit>("%OTPLineEdit");
        _requestOtpButton = GetNode<Button>("%RequestOtpButton");
        _verifyOtpButton = GetNode<Button>("%VerifyOtpButton");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _loadingBar = GetNode<ProgressBar>("%LoadingBar");

        // Set initial UI state
        _otpLineEdit.Visible = false;
        _verifyOtpButton.Visible = false;
        _statusLabel.Text = "";
        _loadingBar.Visible = false;

        // Connect signals
        _requestOtpButton.Pressed += OnRequestOtpButtonPressed;
        _verifyOtpButton.Pressed += OnVerifyOtpButtonPressed;
        _phoneLineEdit.TextChanged += OnPhoneTextChanged;
        _otpLineEdit.TextChanged += OnOtpTextChanged;

        // Get reference to session manager
        _sessionManager = GetNode<UserSessionManager>("/root/UserSessionManager");

        // Check if already logged in
        CheckExistingSession();
    }

    /// <summary>
    /// Checks if a valid user session already exists and redirects to home screen if it does.
    /// This allows for persistent login across app restarts.
    /// </summary>
    private void CheckExistingSession()
    {
        if (_sessionManager.IsLoggedIn())
        {
            _logger.Call("info", $"User already logged in, ID: {_sessionManager.CurrentUser?.Id}");
            // Use CallDeferred to avoid changing scene during _Ready()
            CallDeferred(nameof(ChangeToHomeScene));
        }
        else
        {
            _logger.Call("debug", "No active session found, showing login form");
        }
    }

    /// <summary>
    /// Changes to the home scene after successful authentication.
    /// Called via CallDeferred to avoid scene change errors during node processes.
    /// </summary>
    private void ChangeToHomeScene()
    {
        _logger.Call("info", "Changing to home scene");
        GetTree().ChangeSceneToFile("res://Scenes/Home.tscn");
    }

    /// <summary>
    /// Handles phone number input changes, validating format and updating UI state.
    /// Resets OTP fields if phone number changes after requesting an OTP.
    /// </summary>
    /// <param name="newText">The current text in the phone number field</param>
    private void OnPhoneTextChanged(string newText)
    {
        // Reset OTP form if phone number changes after OTP request
        if (_otpRequested)
        {
            _otpRequested = false;
            _otpLineEdit.Visible = false;
            _verifyOtpButton.Visible = false;
            _statusLabel.Text = "";
        }

        // Validate phone number format
        bool isValid = _phoneRegex.IsMatch(newText);
        _requestOtpButton.Disabled = !isValid || _isProcessing;
    }

    /// <summary>
    /// Handles OTP input changes, enabling verification button when format is valid.
    /// </summary>
    /// <param name="newText">The current text in the OTP field</param>
    private void OnOtpTextChanged(string newText)
    {
        // Enable verify button when OTP is 6 digits
        _verifyOtpButton.Disabled = newText.Length != 6 || _isProcessing;
    }

    /// <summary>
    /// Requests an OTP code be sent to the specified phone number.
    /// Shows loading indicators and provides user feedback throughout the process.
    /// </summary>
    private async void OnRequestOtpButtonPressed()
    {
        string phone = _phoneLineEdit.Text.Trim();

        if (!_phoneRegex.IsMatch(phone))
        {
            ShowError("Please enter a valid phone number in E.164 format (e.g., +12345678901)");
            return;
        }

        SetProcessingState(true);

        try
        {
            // Show loading animation
            _statusLabel.Text = "Sending verification code...";
            _loadingBar.Visible = true;

            await _sessionManager.RequestOtpAsync(phone);

            // Show OTP input
            _otpRequested = true;
            _otpLineEdit.Visible = true;
            _verifyOtpButton.Visible = true;
            _otpLineEdit.GrabFocus();
            _statusLabel.Text = "Verification code sent! Please enter the 6-digit code.";
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"OTP request failed: {ex.Message}", new Godot.Collections.Dictionary { { "phone", phone } });
            ShowError($"Failed to send verification code: {GetUserFriendlyErrorMessage(ex)}");
        }
        finally
        {
            _loadingBar.Visible = false;
            SetProcessingState(false);
        }
    }

    /// <summary>
    /// Verifies the OTP code entered by the user.
    /// Upon successful verification, transitions to the home screen.
    /// </summary>
    private async void OnVerifyOtpButtonPressed()
    {
        string phone = _phoneLineEdit.Text.Trim();
        string otp = _otpLineEdit.Text.Trim();

        if (string.IsNullOrEmpty(otp) || otp.Length != 6)
        {
            ShowError("Please enter the 6-digit verification code");
            return;
        }

        SetProcessingState(true);

        try
        {
            _statusLabel.Text = "Verifying code...";
            _loadingBar.Visible = true;

            var session = await _sessionManager.VerifyOtpAsync(phone, otp);

            if (session != null && session.User != null)
            {
                _logger.Call("info", $"Login successful for user {session.User.Id}");
                _statusLabel.Text = "Login successful! Redirecting...";

                // Wait a moment to show success message before changing scenes
                await Task.Delay(1000);
                CallDeferred(nameof(ChangeToHomeScene));
            }
            else
            {
                ShowError("Verification failed. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"OTP verification failed: {ex.Message}", new Godot.Collections.Dictionary { { "phone", phone } });
            ShowError($"Verification failed: {GetUserFriendlyErrorMessage(ex)}");
        }
        finally
        {
            _loadingBar.Visible = false;
            SetProcessingState(false);
        }
    }

    /// <summary>
    /// Updates UI elements to reflect current processing state.
    /// Disables inputs and buttons during network operations to prevent duplicate requests.
    /// </summary>
    /// <param name="isProcessing">Whether a network operation is in progress</param>
    private void SetProcessingState(bool isProcessing)
    {
        _isProcessing = isProcessing;
        _requestOtpButton.Disabled = isProcessing || !_phoneRegex.IsMatch(_phoneLineEdit.Text);
        _verifyOtpButton.Disabled = isProcessing || _otpLineEdit.Text.Length != 6;
        _phoneLineEdit.Editable = !isProcessing;
        _otpLineEdit.Editable = !isProcessing;
    }

    /// <summary>
    /// Displays an error message to the user and logs it for debugging.
    /// </summary>
    /// <param name="message">The error message to display</param>
    private void ShowError(string message)
    {
        _statusLabel.Text = message;
        _logger.Call("warn", $"Login UI error: {message}");
    }

    /// <summary>
    /// Translates technical error messages into user-friendly language.
    /// Helps staff understand authentication issues without exposing technical details.
    /// </summary>
    /// <param name="ex">The exception that occurred</param>
    /// <returns>A user-friendly error message</returns>
    private string GetUserFriendlyErrorMessage(Exception ex)
    {
        // Parse common Supabase errors into user-friendly messages
        string message = ex.Message.ToLower();

        if (message.Contains("rate limit"))
            return "Too many attempts. Please try again later.";
        if (message.Contains("expired"))
            return "Code expired. Please request a new one.";
        if (message.Contains("invalid"))
            return "Invalid code. Please check and try again.";

        return "An error occurred. Please try again.";
    }
}
