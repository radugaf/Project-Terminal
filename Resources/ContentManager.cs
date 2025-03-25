using Godot;
using System.Collections.Generic;

namespace ProjectTerminal.Resources
{
    public partial class ContentManager : Node
    {
        // The container where content will be displayed
        private Control _container;

        // Registered content scenes
        private Dictionary<string, PackedScene> _scenes = new();

        // Navigation stack for back functionality
        private Stack<string> _history = new();

        // Currently displayed content
        private string _currentContent;
        private Control _currentView;

        [Signal]
        public delegate void ContentChangedEventHandler(string contentName, Control contentNode);

        public void Initialize(Control container)
        {
            _container = container;
        }

        /// <summary>
        /// Registers a content scene with the manager
        /// </summary>
        public void RegisterContent(string name, PackedScene scene)
        {
            _scenes[name] = scene;
        }

        /// <summary>
        /// Shows the specified content, adding previous content to history if specified
        /// </summary>
        public Control ShowContent(string name, bool addToHistory = true)
        {
            if (!_scenes.ContainsKey(name))
            {
                GD.PrintErr($"ContentManager: Content '{name}' not registered");
                return null;
            }

            // Add current content to history if needed
            if (addToHistory && !string.IsNullOrEmpty(_currentContent))
            {
                _history.Push(_currentContent);
            }

            // Remove current content
            if (_currentView != null)
            {
                _currentView.QueueFree();
                _currentView = null;
            }

            // Instantiate and show new content
            _currentView = _scenes[name].Instantiate<Control>();
            _container.AddChild(_currentView);
            _currentContent = name;

            // Make the content fill the container
            _currentView.AnchorRight = 1;
            _currentView.AnchorBottom = 1;
            _currentView.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            _currentView.SizeFlagsVertical = Control.SizeFlags.Fill;

            EmitSignal(SignalName.ContentChanged, name, _currentView);
            return _currentView;
        }

        /// <summary>
        /// Navigate back to the previous content
        /// </summary>
        public Control NavigateBack()
        {
            if (_history.Count > 0)
            {
                string previous = _history.Pop();
                return ShowContent(previous, false);
            }

            return null;
        }

        /// <summary>
        /// Gets the current content name
        /// </summary>
        public string GetCurrentContent() => _currentContent;

        /// <summary>
        /// Gets the current content view
        /// </summary>
        public Control GetCurrentView() => _currentView;
    }
}
