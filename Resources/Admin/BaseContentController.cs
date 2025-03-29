using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProjectTerminal.Resources.Admin
{
    public abstract partial class BaseContentController : Control, IContentController
    {
        protected Node _logger;
        protected ContentManager _contentManager;

        public override void _Ready()
        {
            _logger = GetNode<Node>("/root/Logger");
            _contentManager = AdminPanelService.Instance.ContentManager;

            string controllerName = GetType().Name;
            _logger.Call("debug", $"{controllerName}: Base initialization complete");

            OnReady();
        }

        // Can be overridden by subclasses for normal _Ready behavior
        protected virtual void OnReady() { }

        // IContentController implementation
        public virtual Task InitializeAsync(Dictionary<string, object> parameters = null)
        {
            string controllerName = GetType().Name;
            _logger.Call("debug", $"{controllerName}: Initialized");
            return Task.CompletedTask;
        }

        public virtual Task PrepareForExitAsync()
        {
            string controllerName = GetType().Name;
            _logger.Call("debug", $"{controllerName}: Preparing for exit");
            return Task.CompletedTask;
        }

        public virtual Dictionary<string, object> GetState()
        {
            return new Dictionary<string, object>();
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
