using Godot;

public partial class UIManager : Node
{
    // Signals
    [Signal]
    public delegate void ScreenDataUpdatedEventHandler();

    // Properties
    public string SystemName { get; private set; }
    public int ScreenCount { get; private set; }
    public Godot.Collections.Array Screens { get; private set; } = new();
    public Vector2I WindowSize { get; private set; }
    public bool IsTouchscreen { get; private set; }

    // Lifecycle
    public override void _Ready()
    {
        RetrieveDisplayInfo();
        GetViewport().Connect("size_changed", new Callable(this, MethodName.OnViewportResized));
    }

    // Public API
    /// <summary>
    /// Gathers fresh display info and stores it in this node's properties.
    /// Emits screen_data_updated so other scripts can react.
    /// </summary>
    public void RetrieveDisplayInfo()
    {
        SystemName = OS.GetName();

        // Number of monitors
        ScreenCount = DisplayServer.GetScreenCount();

        // Gather per-screen info in a list of dictionaries
        Screens.Clear();
        for (int screenIndex = 0; screenIndex < ScreenCount; screenIndex++)
        {
            Vector2I size = DisplayServer.ScreenGetSize(screenIndex);
            int dpi = DisplayServer.ScreenGetDpi(screenIndex);
            DisplayServer.ScreenOrientation orientation = DisplayServer.ScreenGetOrientation(screenIndex);

            var screenInfo = new Godot.Collections.Dictionary
            {
                { "index", screenIndex },
                { "size", size },
                { "dpi", dpi },
                { "orientation", (int)orientation }
            };

            Screens.Add(screenInfo);
        }

        // Window size (the current Godot window size in pixels)
        WindowSize = DisplayServer.WindowGetSize();

        // Whether it's a touchscreen environment (mobile/tablet)
        IsTouchscreen = DisplayServer.IsTouchscreenAvailable();

        // Notify listeners that data was updated
        EmitSignal(SignalName.ScreenDataUpdated);
    }

    /// <summary>
    /// Returns a Dictionary with all the stored display/system properties.
    /// Helpful if you want everything in a single object.
    /// </summary>
    public Godot.Collections.Dictionary GetInfoAsDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            { "system_name", SystemName },
            { "screen_count", ScreenCount },
            { "screens", Screens },
            { "window_size", WindowSize },
            { "is_touchscreen", IsTouchscreen }
        };
    }

    // Private Handlers
    /// <summary>
    /// Called automatically when the window (the root viewport) is resized.
    /// We refresh the display info to keep it up to date.
    /// </summary>
    private void OnViewportResized()
    {
        RetrieveDisplayInfo();
    }
}
