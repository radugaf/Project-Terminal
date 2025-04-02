using System;
using System.Threading.Tasks;
using ProjectTerminal.Globals.Interfaces;

namespace ProjectTerminal.Globals.Wrappers
{
    public class SupabaseClientWrapper(Supabase.Client supabaseClient, Logger logger) : ISupabaseClientWrapper
    {
        private readonly Supabase.Client _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
        private readonly Logger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public bool IsInitialized { get; private set; } = false;

        public async Task Initialize()
        {
            await _supabaseClient.InitializeAsync();
            IsInitialized = true;
            _logger.Debug("SupabaseClientWrapper: Client initialized successfully");
        }

        public Supabase.Client GetClient() => _supabaseClient;
    }
}
