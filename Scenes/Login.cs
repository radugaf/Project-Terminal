using Godot;
using System.Text.RegularExpressions;

/// <summary>
/// Handles user authentication through OTP (One-Time Password) verification.
/// Manages the login UI and authentication flow for the POS terminal.
/// </summary>
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
        _otpLabel.Visible = false;
        _otpLineEdit.Visible = false;
        _verifyOtpButton.Visible = false;
        _statusLabel.Text = "";
        _loadingBar.Visible = false;
    }
}

