using Godot;
using System;
using System.Threading.Tasks;
using ProjectTerminal.Resources;
using Supabase.Postgrest.Responses;
using Supabase.Postgrest;

public partial class CategoryManager : Node
{
    private Logger _logger;
    private SupabaseClient _supabaseClient;
    private OrganizationManager _organizationManager;

    [Signal]
    public delegate void CategoryCreatedEventHandler(string addressId);

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("AddressManager: Initializing");

        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");
        _organizationManager = GetNode<OrganizationManager>("/root/OrganizationManager");
    }

    public async Task<string> CreateCategoryAsync(string name, string icon, string color)
    {
        try
        {
            var category = new Category
            {
                Name = name,
                Icon = icon,
                Color = color,
                OrganizationId = _organizationManager.GetOrganizationId()
            };

            ModeledResponse<Category> response = await _supabaseClient.GetClient().From<Category>()
                .Insert(category, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

            if (response == null || response.ResponseMessage.IsSuccessStatusCode != true)
            {
                _logger.Error($"CategoryManager: Failed to create category: {response?.ResponseMessage.ReasonPhrase}");
                throw new Exception($"Failed to create category");
            }

            string categoryId = response.Model?.Id;
            _logger.Info($"CategoryManager: Category created with ID: {categoryId}");

            EmitSignal(SignalName.CategoryCreated, categoryId);
            return categoryId;
        }
        catch (Exception ex)
        {
            _logger.Error($"CategoryManager: Category creation failed: {ex.Message}");
            throw;
        }
    }
}
