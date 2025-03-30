using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProjectTerminal.Resources.Admin
{
    public partial class ContentManager : Node
    {
        // Singleton instance (accessible via AdminPanelService)
        public static ContentManager Instance { get; private set; }

        // The container where content will be displayed
        private Control _container;

        // Registered content scenes with metadata
        private readonly Dictionary<string, ContentRegistration> _contentRegistry = [];

        // Navigation stack for back functionality
        private readonly Stack<NavigationEntry> _navigationHistory = new();

        // Currently displayed content
        private string _currentContentId;
        private IContentController _currentController;
        private Control _currentView;

        // Logger reference
        private Logger _logger;

        [Signal]
        public delegate void ContentChangingEventHandler(string fromContentId, string toContentId);

        [Signal]
        public delegate void ContentChangedEventHandler(string contentId, Control contentNode);

        [Signal]
        public delegate void NavigationErrorEventHandler(string errorMessage);

        public override void _Ready()
        {
            Instance = this;
            _logger = GetNode<Logger>("/root/Logger");
            _logger.Info("ContentManager: Initialized");
        }

        public void Initialize(Control container)
        {
            _container = container;
            _logger.Debug("ContentManager: Container set");
        }

        public void RegisterContent(string id, PackedScene scene, Dictionary<string, object> metadata = null)
        {
            if (_contentRegistry.ContainsKey(id))
            {
                _logger.Warn($"ContentManager: Content '{id}' already registered, replacing");
            }

            _contentRegistry[id] = new ContentRegistration
            {
                Id = id,
                Scene = scene,
                Metadata = metadata ?? []
            };

            _logger.Debug($"ContentManager: Registered content '{id}'");
        }

        public async Task<Control> ShowContentAsync(string id, bool addToHistory = true, Dictionary<string, object> parameters = null)
        {
            if (!_contentRegistry.ContainsKey(id))
            {
                string errorMsg = $"ContentManager: Content '{id}' not registered";
                _logger.Error(errorMsg);
                EmitSignal(SignalName.NavigationError, errorMsg);
                return null;
            }

            try
            {
                // Signal that content is about to change
                EmitSignal(SignalName.ContentChanging, _currentContentId, id);

                // Allow current content to prepare for unloading
                if (_currentController != null)
                {
                    await _currentController.PrepareForExitAsync();
                }

                // Add current content to history if needed
                if (addToHistory && !string.IsNullOrEmpty(_currentContentId))
                {
                    _navigationHistory.Push(new NavigationEntry
                    {
                        ContentId = _currentContentId,
                        Parameters = _currentController?.GetState() ?? []
                    });

                    _logger.Debug($"ContentManager: Added '{_currentContentId}' to navigation history");
                }

                // Remove current content
                if (_currentView != null)
                {
                    _currentView.QueueFree();
                    _currentView = null;
                    _currentController = null;
                }

                // Instantiate and show new content
                ContentRegistration registration = _contentRegistry[id];
                _currentView = registration.Scene.Instantiate<Control>();
                _container.AddChild(_currentView);

                // Configure the view
                // _currentView.AnchorRight = 1;
                // _currentView.AnchorBottom = 1;
                // _currentView.SizeFlagsHorizontal = Control.SizeFlags.Fill;
                // _currentView.SizeFlagsVertical = Control.SizeFlags.Fill;

                // Try to get controller interface
                _currentController = _currentView as IContentController;
                _currentContentId = id;

                // Initialize the controller if available
                if (_currentController != null)
                {
                    await _currentController.InitializeAsync(parameters);
                }

                // Emit content changed signal
                EmitSignal(SignalName.ContentChanged, id, _currentView);
                _logger.Info($"ContentManager: Switched content to '{id}'");

                return _currentView;
            }
            catch (System.Exception ex)
            {
                _logger.Error($"ContentManager: Error showing content '{id}': {ex.Message}");
                EmitSignal(SignalName.NavigationError, $"Failed to load content '{id}': {ex.Message}");
                return null;
            }
        }

        public async Task<Control> NavigateBackAsync()
        {
            if (_navigationHistory.Count == 0)
            {
                _logger.Warn("ContentManager: Can't navigate back - history empty");
                return null;
            }

            NavigationEntry previous = _navigationHistory.Pop();
            return await ShowContentAsync(previous.ContentId, false, previous.Parameters);
        }

        public bool CanNavigateBack() => _navigationHistory.Count > 0;

        public string GetCurrentContentId() => _currentContentId;

        public Control GetCurrentView() => _currentView;

        public IContentController GetCurrentController() => _currentController;

        private class ContentRegistration
        {
            public string Id { get; set; }
            public PackedScene Scene { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
        }

        private class NavigationEntry
        {
            public string ContentId { get; set; }
            public Dictionary<string, object> Parameters { get; set; }
        }
    }
}
