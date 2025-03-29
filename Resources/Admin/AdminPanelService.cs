using Godot;
using System;

namespace ProjectTerminal.Resources.Admin
{
    public partial class AdminPanelService : Node
    {
        private static AdminPanelService _instance;

        private ContentManager _contentManager;
        private Node _logger;

        public static AdminPanelService Instance => _instance;

        public ContentManager ContentManager => _contentManager;

        public override void _Ready()
        {
            if (_instance != null)
            {
                QueueFree();
                return;
            }

            _instance = this;
            _logger = GetNode<Node>("/root/Logger");
            _logger.Call("info", "AdminPanelService: Initialized");
        }

        public void Initialize(ContentManager contentManager)
        {
            _contentManager = contentManager;
            _logger.Call("debug", "AdminPanelService: ContentManager registered");
        }
    }
}
