using Godot;
using ProjectTerminal.Resources.Admin;

public partial class Items : BaseContentController
{
    // Category
    private Button _addCategoryButton;
    private VBoxContainer _categoryList;

    protected override void OnReady()
    {
        base.OnReady();
        _logger.Info("Items: Initializing Items scene");

        // Category
        _addCategoryButton = GetNode<Button>("%AddCategoryButton");
        _categoryList = GetNode<VBoxContainer>("%CategoryList");

        // Connect button signals
        _addCategoryButton.Pressed += OnAddCategoryButtonPressed;

        _logger.Info("Items: Items scene initialized");
    }

    private void OnAddCategoryButtonPressed()
    {
        _logger.Debug("Items: Navigating to AddCategory");
        _ = NavigateToAsync("AddCategory");
    }
}
