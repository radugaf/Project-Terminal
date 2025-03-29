using Godot;
using ProjectTerminal.Resources;

public partial class AddCategory : Control
{
    private Logger _logger;
    private LineEdit _categoryName;
    private ColorPickerButton _categoryColor;
    private Button _submitButton;
    private Button _backButton;

    // Reference to the ContentManager
    private ContentManager _contentManager;

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("AddCategory: Initializing AddCategory scene");

        _categoryName = GetNode<LineEdit>("%NameLineEdit");
        _categoryColor = GetNode<ColorPickerButton>("%ColorPickerButton");
        _submitButton = GetNode<Button>("%SubmitButton");
        _backButton = GetNode<Button>("%BackButton");

        // Connect button signals
        _submitButton.Pressed += OnSubmitButtonPressed;
        _backButton.Pressed += OnBackButtonPressed;

        // Attempt to find ContentManager in the parent hierarchy (fallback)
        if (_contentManager == null)
        {
            Node parent = GetParent();
            while (parent != null && _contentManager == null)
            {
                foreach (Node child in parent.GetChildren())
                {
                    if (child is ContentManager contentManager)
                    {
                        _contentManager = contentManager;
                        GD.Print(_contentManager.Name);
                        break;
                    }
                }
                parent = parent.GetParent();
            }
        }

        _logger.Info("AddCategory: AddCategory scene initialized");
    }

    // Set the ContentManager reference (called from AdminPanel)
    public void SetContentManager(ContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    private void OnSubmitButtonPressed()
    {
        _logger.Debug("AddCategory: Submit button pressed");

        // Here you would typically save the category
        // Then navigate back to the Items view
        if (_contentManager != null)
        {
            _contentManager.NavigateBack(); // Return to the previous view (Items)
        }
    }

    private void OnBackButtonPressed()
    {
        _logger.Debug("AddCategory: Back button pressed");

        if (_contentManager != null)
        {
            _contentManager.NavigateBack();
        }
    }
}
