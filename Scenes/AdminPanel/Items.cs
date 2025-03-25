using Godot;
using System;
using ProjectTerminal.Resources;

public partial class Items : Control
{
    private Node _logger;

    // Category
    private Button _addCategoryButton;
    private VBoxContainer _categoryList;
    private HBoxContainer _categoryLine;

    // Reference to ContentManager (will be set by AdminPanel)
    private ContentManager _contentManager;

    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "Items: Initializing Items scene");

        // Category
        _addCategoryButton = GetNode<Button>("%AddCategoryButton");
        _categoryList = GetNode<VBoxContainer>("%CategoryList");

        // Connect button signals
        _addCategoryButton.Pressed += OnAddCategoryButtonPressed;

        // Initialize category list

        // Get ContentManager reference from parent
        Node parent = GetParent();
        while (parent != null && _contentManager == null)
        {
            if (parent is AdminPanel adminPanel)
            {
                // We found the AdminPanel, now get its ContentManager
                foreach (Node child in adminPanel.GetChildren())
                {
                    if (child is ContentManager contentManager)
                    {
                        _contentManager = contentManager;
                        break;
                    }
                }
                break;
            }
            parent = parent.GetParent();
        }

        if (_contentManager == null)
        {
            _logger.Call("error", "Items: Could not find ContentManager in parent hierarchy");
        }

        _logger.Call("info", "Items: Items scene initialized");
    }

    private void OnAddCategoryButtonPressed()
    {
        if (_contentManager != null)
        {
            _logger.Call("debug", "Items: Navigating to AddCategory");
            _contentManager.ShowContent("AddCategory");
        }
        else
        {
            _logger.Call("error", "Items: Cannot navigate, ContentManager is null");
        }
    }
}
