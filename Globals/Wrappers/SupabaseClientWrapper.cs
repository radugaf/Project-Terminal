using System;
using System.Threading.Tasks;
using Supabase.Gotrue;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;
using Supabase.Realtime;
using static Supabase.Gotrue.Constants;
using ProjectTerminal.Globals.Interfaces;
using Supabase.Interfaces;

namespace ProjectTerminal.Globals.Wrappers
{
    public class SupabaseClientWrapper(Supabase.Client supabaseClient, Logger logger) : ISupabaseClientWrapper
    {
        private readonly Supabase.Client _supabaseClient = supabaseClient;
        private readonly Logger _logger = logger;

        public async Task Initialize()
        {
            try
            {
                await _supabaseClient.InitializeAsync();
                _logger.Info("SupabaseClientWrapper: Initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: Initialization failed: {ex.Message}");
                throw;
            }
        }

        public async Task<Session> SignIn(string email, string password)
        {
            try
            {
                return await _supabaseClient.Auth.SignIn(email, password);
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: SignIn failed: {ex.Message}");
                throw;
            }
        }

        public async Task<Session> SignUp(string email, string password)
        {
            try
            {
                return await _supabaseClient.Auth.SignUp(email, password);
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: SignUp failed: {ex.Message}");
                throw;
            }
        }

        public async Task<Session> SignIn(SignInType type, string credential)
        {
            try
            {
                return await _supabaseClient.Auth.SignIn(type, credential);
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: SignIn with type failed: {ex.Message}");
                throw;
            }
        }

        public async Task<Session> VerifyOTP(string phone, string otpCode, MobileOtpType type)
        {
            try
            {
                return await _supabaseClient.Auth.VerifyOTP(phone, otpCode, type);
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: VerifyOTP failed: {ex.Message}");
                throw;
            }
        }

        public async Task<Session> RefreshSession()
        {
            try
            {
                return await _supabaseClient.Auth.RefreshSession();
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: RefreshSession failed: {ex.Message}");
                throw;
            }
        }

        public async Task SignOut()
        {
            try
            {
                await _supabaseClient.Auth.SignOut();
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: SignOut failed: {ex.Message}");
                throw;
            }
        }

        public async Task SetSession(string accessToken, string refreshToken)
        {
            try
            {
                await _supabaseClient.Auth.SetSession(accessToken, refreshToken);
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: SetSession failed: {ex.Message}");
                throw;
            }
        }

        public async Task<User> Update(UserAttributes attributes)
        {
            try
            {
                return await _supabaseClient.Auth.Update(attributes);
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: Update user failed: {ex.Message}");
                throw;
            }
        }

        public ISupabaseTable<T, RealtimeChannel> From<T>() where T : BaseModel, new()
        {
            try
            {
                return _supabaseClient.From<T>();
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: From<{typeof(T).Name}> failed: {ex.Message}");
                throw;
            }
        }

        public async Task<BaseResponse> Rpc(string procedureName, object parameters)
        {
            try
            {
                return await _supabaseClient.Rpc(procedureName, parameters);
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: Rpc call to {procedureName} failed: {ex.Message}");
                throw;
            }
        }

        public async Task<TResponse> Rpc<TResponse>(string procedureName, object parameters)
        {
            try
            {
                return await _supabaseClient.Rpc<TResponse>(procedureName, parameters);
            }
            catch (Exception ex)
            {
                _logger.Error($"SupabaseClientWrapper: Typed Rpc call to {procedureName} failed: {ex.Message}");
                throw;
            }
        }
    }
}
