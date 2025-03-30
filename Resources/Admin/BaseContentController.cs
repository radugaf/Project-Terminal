using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProjectTerminal.Resources.Admin
{
    public abstract partial class BaseContentController : Control, IContentController
    {
        protected Logger _logger;
        protected ContentManager _contentManager;

        public override void _Ready()
        {
            _logger = GetNode<Logger>("/root/Logger");
            _contentManager = AdminPanelService.Instance.ContentManager;

            string controllerName = GetType().Name;
            _logger.Debug($"{controllerName}: Base initialization complete");

            OnReady();
        }

        // Can be overridden by subclasses for normal _Ready behavior
        protected virtual void OnReady() { }

        // IContentController implementation
        public virtual Task InitializeAsync(Dictionary<string, object> parameters = null)
        {
            string controllerName = GetType().Name;
            _logger.Debug($"{controllerName}: Initialized");
            return Task.CompletedTask;
        }

        public virtual Task PrepareForExitAsync()
        {
            string controllerName = GetType().Name;
            _logger.Debug($"{controllerName}: Preparing for exit");
            return Task.CompletedTask;
        }

        public virtual Dictionary<string, object> GetState()
        {
            return [];
        }

        // Helper methods for navigation
        protected Task<Control> NavigateToAsync(string contentId, Dictionary<string, object> parameters = null)
        {
            return _contentManager.ShowContentAsync(contentId, true, parameters);
        }

        protected Task<Control> NavigateBackAsync()
        {
            return _contentManager.NavigateBackAsync();
        }
    }
}
