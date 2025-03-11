# Project-Terminal
*A comprehensive POS Terminal powered by Godot*

### How Session Persistence Works
1. When a user logs in via OTP verification, a session is created with:
    - Access token (for API requests)
    - Refresh token (for getting new access tokens)
    - User information
    - Expiry time


2. This session is stored in ProjectSettings using:
```cs
ProjectSettings.SetSetting($"application/config/{SESSION_STORE_KEY}", sessionJson);
ProjectSettings.SetSetting($"application/config/{SESSION_EXPIRY_KEY}", expiryTime.ToString("o"));
ProjectSettings.Save();
```

3. When the app starts, the session is loaded and validated:
```cs
// Load session data
string sessionJson = (string)ProjectSettings.GetSetting($"application/config/{SESSION_STORE_KEY}");

// Check if it's expired
string expiryTimestamp = (string)ProjectSettings.GetSetting($"application/config/{SESSION_EXPIRY_KEY}");
```

4. If the session is expired or about to expire, the system attempts to refresh it automatically

### Working with UserSessionManager

Checking Authentication Status
```cs
// Always use IsLoggedIn() to check current authentication status
if (_sessionManager.IsLoggedIn())
{
    // User is authenticated, proceed with protected operations
}
else
{
    // Redirect to login screen
    GetTree().ChangeSceneToFile("res://Scenes/Login.tscn");
}
```

Accessing User Information
```cs
// Get current user ID
string userId = _sessionManager.CurrentUser?.Id;

// Check user properties
if (_sessionManager.CurrentUser != null)
{
    string phoneNumber = _sessionManager.CurrentUser.Phone;
    // ...other properties
}
```

Handling Session Changes
```cs
// Connect to the SessionChanged signal
_sessionManager.Connect(UserSessionManager.SignalName.SessionChanged, Callable.From(OnSessionChanged));

// Handle session changes
private void OnSessionChanged()
{
    // Update UI or state based on new session status
    if (_sessionManager.IsLoggedIn())
    {
        // Session established or refreshed
        UpdateUIForLoggedInUser();
    }
    else
    {
        // Session ended
        RedirectToLogin();
    }
}
```

Implementing Logout
```cs
// Always use LogoutAsync for proper cleanup
private async void OnLogoutButtonPressed()
{
    await _sessionManager.LogoutAsync();
    // UI will be updated via the SessionChanged signal
}
```
