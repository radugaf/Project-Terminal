// ISupabaseClientWrapper.cs
using Supabase.Gotrue;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;
using Supabase.Realtime;
using System.Threading.Tasks;
using static Supabase.Gotrue.Constants;
using Supabase.Interfaces;

namespace ProjectTerminal.Globals.Interfaces
{
    /// <summary>
    /// Defines the operations that can be performed with Supabase.
    /// This interface makes testing easier by allowing mock implementations.
    /// </summary>
    public interface ISupabaseClientWrapper
    {
        // Authentication operations
        Task<Session> SignIn(string email, string password);
        Task<Session> SignUp(string email, string password);
        Task<Session> SignIn(SignInType type, string credential);
        Task<Session> VerifyOTP(string phone, string otpCode, MobileOtpType type);
        Task<Session> RefreshSession();
        Task SignOut();
        Task SetSession(string accessToken, string refreshToken);
        Task<User> Update(UserAttributes attributes);

        // Database operations
        ISupabaseTable<T, RealtimeChannel> From<T>() where T : BaseModel, new();

        // RPC calls
        Task<BaseResponse> Rpc(string procedureName, object parameters);
        Task<TResponse> Rpc<TResponse>(string procedureName, object parameters);

        // Initialization
        Task Initialize();
    }
}
