using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProjectTerminal.Resources.Admin
{
    public interface IContentController
    {
        // Initialize the content with optional parameters
        Task InitializeAsync(Dictionary<string, object> parameters = null);

        // Called before the content is removed, allows for cleanup or state saving
        Task PrepareForExitAsync();

        // Get the current state for history navigation
        Dictionary<string, object> GetState();
    }
}
