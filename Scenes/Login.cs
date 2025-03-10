using Godot;
using System;



/// <summary>
/// A user interface control for handling phone-based OTP login flow.
/// Demonstrates requesting a code and verifying it with the global session manager.
/// If a session is already active, the UI can be bypassed or hidden.
/// </summary>
public partial class Login : Control
{

	Node _logger;

	private LineEdit _phoneLineEdit;
	private LineEdit _otpLineEdit;
	private Button _requestOtpButton;
	private Button _verifyOtpButton;

	/// <summary>
	/// Provides an entry point to the global session manager singleton for checking and modifying the user session.
	/// </summary>
	private UserSessionManager _sessionManager;

	/// <summary>
	/// Retrieves UI references, attaches to button events, and checks whether a session is already active.
	/// If a session is valid, the login UI may be skipped to provide a smooth experience.
	/// </summary>
	public override void _Ready()
	{
		_logger = GetNode<Node>("/root/Logger");
		_logger.Call("info", "Login scene is ready.");

		// Retrieve UI elements and attach event handlers.
		_phoneLineEdit = GetNode<LineEdit>("%PhoneLineEdit");
		_otpLineEdit = GetNode<LineEdit>("%OTPLineEdit");
		_requestOtpButton = GetNode<Button>("%RequestOtpButton");
		_verifyOtpButton = GetNode<Button>("%VerifyOtpButton");

		_requestOtpButton.Pressed += OnRequestOtpButtonPressed;
		_verifyOtpButton.Pressed += OnVerifyOtpButtonPressed;

		_sessionManager = GetNode<UserSessionManager>("/root/UserSessionManager");

		bool isAlreadyLoggedIn = _sessionManager.IsLoggedIn();
		if (isAlreadyLoggedIn)
		{
			GD.Print("A valid session is already present. Current user ID: " + _sessionManager.CurrentUser?.Id);
			// If desired, the login screen can be hidden or another scene can be loaded here.
			GetTree().ChangeSceneToFile("res://Scenes/home.tscn");
		}
	}

	/// <summary>
	/// Triggered when the user presses the button to request an OTP.
	/// Sends a request to Supabase to generate and deliver an OTP via SMS for the entered phone number.
	/// </summary>
	private async void OnRequestOtpButtonPressed()
	{
		string phoneNumber = _phoneLineEdit.Text.Trim();
		bool missingPhone = string.IsNullOrEmpty(phoneNumber);

		if (missingPhone)
		{
			GD.Print("Phone number is empty and cannot be used for OTP requests.");
			return;
		}

		try
		{
			await _sessionManager.RequestOtpAsync(phoneNumber);
			GD.Print("An OTP code has been sent to the provided phone number.");
		}
		catch (Exception ex)
		{
			GD.PrintErr("Requesting OTP encountered an error: " + ex.Message);
		}
	}

	/// <summary>
	/// Triggered when the user presses the button to verify the OTP code.
	/// Calls the session manager to confirm the code is valid and logs the user in if successful.
	/// </summary>
	private async void OnVerifyOtpButtonPressed()
	{
		string phoneNumber = _phoneLineEdit.Text.Trim();
		string otpCode = _otpLineEdit.Text.Trim();

		bool missingData = string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(otpCode);
		if (missingData)
		{
			GD.Print("Both phone number and OTP code must be provided before verification.");
			return;
		}

		try
		{
			var session = await _sessionManager.VerifyOtpAsync(phoneNumber, otpCode);
			bool isSessionValid = session != null && session.User != null;

			if (isSessionValid)
			{
				GD.Print("OTP verification succeeded. User ID: " + session.User.Id);
				// The user is authenticated. This can be followed by scene changes or UI updates.
				GetTree().ChangeSceneToFile("res://Scenes/home.tscn");
			}
			else
			{
				GD.Print("The OTP could not be verified. The session is null or missing user information.");
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr("Verifying OTP encountered an error: " + ex.Message);
		}
	}
}
