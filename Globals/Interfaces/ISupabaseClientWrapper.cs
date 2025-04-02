using System.Threading.Tasks;

namespace ProjectTerminal.Globals.Interfaces
{
    public interface ISupabaseClientWrapper
    {
        Task Initialize();
        bool IsInitialized { get; }
        Supabase.Client GetClient();
    }
}
