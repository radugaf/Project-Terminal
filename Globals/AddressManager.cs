using Godot;
using System;
using System.Threading.Tasks;
using ProjectTerminal.Resources;
using Supabase.Postgrest.Responses;
using Supabase.Postgrest;

public partial class AddressManager : Node
{
    private Logger _logger;
    private SupabaseClient _supabaseClient;

    [Signal]
    public delegate void AddressCreatedEventHandler(string addressId);

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("AddressManager: Initializing");

        _supabaseClient = GetNode<SupabaseClient>("/root/SupabaseClient");
    }

    public async Task<string> CreateAddressAsync(string country, string city, string streetAddress1,
                                               string streetAddress2, string postalCode)
    {
        try
        {
            var address = new Address
            {
                Country = country,
                City = city,
                StreetAddress1 = streetAddress1,
                StreetAddress2 = streetAddress2,
                PostalCode = postalCode,
                IsVerified = false
            };

            ModeledResponse<Address> response = await _supabaseClient.From<Address>()
                .Insert(address, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

            if (response == null || response.ResponseMessage.IsSuccessStatusCode != true)
            {
                _logger.Error($"AddressManager: Failed to create address: {response?.ResponseMessage.ReasonPhrase}");
                throw new Exception($"Failed to create address");
            }

            string addressId = response.Model?.Id;
            _logger.Info($"AddressManager: Address created with ID: {addressId}");

            EmitSignal(SignalName.AddressCreated, addressId);
            return addressId;
        }
        catch (Exception ex)
        {
            _logger.Error($"AddressManager: Address creation failed: {ex.Message}");
            throw;
        }
    }
}
