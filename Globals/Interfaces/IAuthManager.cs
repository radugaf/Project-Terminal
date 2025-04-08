using System;
using System.Threading.Tasks;
using Supabase.Gotrue;

namespace ProjectTerminal.Globals.Interfaces
{
    public interface IAuthManager
    {
        Session CurrentSession { get; }
        User CurrentUser { get; }
        bool IsNewUser { get; }

        Task<Session> RegisterWithEmailAsync(string email, string password, bool rememberMe = false);
        Task<Session> LoginWithEmailAsync(string email, string password, bool rememberMe = false);
        Task<Session> VerifyLoginOtpAsync(string phoneNumber, string otpCode, bool rememberMe = false);
        Task RequestLoginOtpAsync(string phoneNumber);
        Task<User> UpdateUserEmailAsync(string email);
        Task<bool> RefreshSessionAsync();
        Task LogoutAsync();
        bool IsLoggedIn();
        bool SetUserAsExisting();
        string RunAuthDiagnostics();
    }

    // Session management extracted to a separate concern
    public interface ISessionManager
    {
        Session CurrentSession { get; }
        bool IsSessionValid { get; }
        bool IsPersistentSession { get; }

        void SaveSession(Session session, bool isPersistent);
        Task<Session> LoadSessionAsync();
        void ClearSession();
        DateTime? GetSessionExpiryTime();
        bool IsSessionExpired();
        void SetUserNewState(bool isNew);
        bool GetUserNewState();
        int GetRefreshThresholdSeconds();
        DateTime GetLastRefreshTime();
    }

    // Time abstraction for testability
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }

}

