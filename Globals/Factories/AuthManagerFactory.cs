using ProjectTerminal.Globals.Interfaces;
using ProjectTerminal.Globals.Services;

namespace ProjectTerminal.Globals.Factories
{
    public static class AuthManagerFactory
    {
        public static AuthManager CreateDefault() => new();

        public static AuthManager CreateWithDependencies(
            ISessionManager sessionManager,
            ISupabaseClientWrapper supabaseClient,
            Logger logger,
            ITimeProvider timeProvider) => new(sessionManager, supabaseClient, logger, timeProvider);
    }
}
