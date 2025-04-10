using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;

namespace ProjectTerminal.Resources
{
    [Table("addresses")]
    public class Address : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("street_address1")]
        public string StreetAddress1 { get; set; }

        [Column("street_address2")]
        public string StreetAddress2 { get; set; }

        [Column("city")]
        public string City { get; set; }

        [Column("state")]
        public string State { get; set; }

        [Column("postal_code")]
        public string PostalCode { get; set; }

        [Column("country")]
        public string Country { get; set; } = "RO";

        [Column("latitude")]
        public float? Latitude { get; set; }

        [Column("longitude")]
        public float? Longitude { get; set; }

        [Column("is_verified")]
        public bool IsVerified { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("organizations")]
    public class Organization : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("business_type")]
        [EnumDataType(typeof(BusinessType))]
        public string BusinessType { get; set; }

        [Column("tax_id")]
        public string TaxId { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("website")]
        public string Website { get; set; }

        [Column("logo_url")]
        public string LogoUrl { get; set; }

        [Column("billing_address_id")]
        public string BillingAddressId { get; set; }

        [Column("status")]
        [EnumDataType(typeof(OrganizationStatus))]
        public string Status { get; set; }

        [Column("verification_documents")]
        public string VerificationDocuments { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("created_by")]
        public string CreatedBy { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }

    [Table("locations")]
    public class Location : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("organization_id")]
        public string OrganizationId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("address_id")]
        public string AddressId { get; set; }

        [Column("timezone")]
        public string Timezone { get; set; }

        [Column("business_hours")]
        public string BusinessHours { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }

    [Table("terminals")]
    public class Terminal : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("organization_id")]
        public string OrganizationId { get; set; }

        [Column("location_id")]
        public string LocationId { get; set; }

        [Column("terminal_name")]
        public string TerminalName { get; set; }

        [Column("device_id")]
        public string DeviceId { get; set; }

        [Column("device_name")]
        public string DeviceName { get; set; }

        [Column("device_model")]
        public string DeviceModel { get; set; }

        [Column("device_os")]
        public string DeviceOs { get; set; }

        [Column("device_os_version")]
        public string DeviceOsVersion { get; set; }

        [Column("processor_type")]
        public string ProcessorType { get; set; }

        [Column("ip_address")]
        public string IpAddress { get; set; }

        [Column("mac_address")]
        public string MacAddress { get; set; }

        [Column("screen_dpi")]
        public string ScreenDpi { get; set; }

        [Column("screen_orientation")]
        public string ScreenOrientation { get; set; }

        [Column("is_touchscreen")]
        public bool IsTouchscreen { get; set; }

        [Column("screen_scale")]
        public string ScreenScale { get; set; }

        [Column("active")]
        public bool Active { get; set; }

        [Column("last_active")]
        public DateTime? LastActive { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("registered_by")]
        public string RegisteredBy { get; set; }
    }

    [Table("staff")]
    public class Staff : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("organization_id")]
        public string OrganizationId { get; set; }

        [Column("role")]
        [EnumDataType(typeof(StaffRole))]
        public string Role { get; set; }

        [Column("job_title")]
        public string JobTitle { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; }

        [Column("last_name")]
        public string LastName { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("staff_locations")]
    public class StaffLocation : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("staff_id")]
        public string StaffId { get; set; }

        [Column("location_id")]
        public string LocationId { get; set; }

        [Column("is_primary")]
        public bool IsPrimary { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("staff_permissions")]
    public class StaffPermission : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("permission")]
        public string Permission { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("location_id")]
        public string LocationId { get; set; }

        [Column("granted_by")]
        public string GrantedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StaffRole
    {
        [EnumMember(Value = "owner")]
        Owner,
        [EnumMember(Value = "manager")]
        Manager,
        [EnumMember(Value = "staff")]
        Staff
    }


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BusinessType
    {
        [EnumMember(Value = "restaurant")]
        Restaurant,

        [EnumMember(Value = "cafe")]
        Cafe,

        [EnumMember(Value = "retail")]
        Retail,

        [EnumMember(Value = "grocery")]
        Grocery,

        [EnumMember(Value = "salon")]
        Salon
    }


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OrganizationStatus
    {
        [EnumMember(Value = "pending_review")]
        Pending_Review,
        [EnumMember(Value = "approved")]
        Approved,
        [EnumMember(Value = "rejected")]
        Rejected,
        [EnumMember(Value = "suspended")]
        Suspended
    }


    [Table("categories")]
    public class Category : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("icon")]
        public string Icon { get; set; }

        [Column("color")]
        public string Color { get; set; }

        [Column("display_order")]
        public int DisplayOrder { get; set; }

        [Column("organization_id")]
        public string OrganizationId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("items")]
    public class Item : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("price")]
        public decimal Price { get; set; }

        [Column("cost")]
        public decimal Cost { get; set; }

        [Column("tax_rate")]
        public decimal TaxRate { get; set; }

        [Column("sku")]
        public string SKU { get; set; }

        [Column("barcode")]
        public string Barcode { get; set; }

        [Column("image_url")]
        public string ImageUrl { get; set; }

        [Column("category_id")]
        public string CategoryId { get; set; }

        [Column("organization_id")]
        public string OrganizationId { get; set; }

        [Column("inventory_tracking")]
        public bool InventoryTracking { get; set; }

        [Column("current_stock")]
        public decimal CurrentStock { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("item_modifiers")]
    public class ItemModifier : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("item_id")]
        public string ItemId { get; set; }

        [Column("modifier_group_id")]
        public string ModifierGroupId { get; set; }

        [Column("is_required")]
        public bool IsRequired { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("modifier_groups")]
    public class ModifierGroup : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("organization_id")]
        public string OrganizationId { get; set; }

        [Column("selection_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ModifierSelectionType SelectionType { get; set; }

        [Column("min_selections")]
        public int MinSelections { get; set; }

        [Column("max_selections")]
        public int MaxSelections { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("modifiers")]
    public class Modifier : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("modifier_group_id")]
        public string ModifierGroupId { get; set; }

        [Column("price_adjustment")]
        public decimal PriceAdjustment { get; set; }

        [Column("display_order")]
        public int DisplayOrder { get; set; }

        [Column("is_default")]
        public bool IsDefault { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("discounts")]
    public class Discount : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("discount_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DiscountType DiscountType { get; set; }

        [Column("value")]
        public decimal Value { get; set; }

        [Column("organization_id")]
        public string OrganizationId { get; set; }

        [Column("requires_code")]
        public bool RequiresCode { get; set; }

        [Column("code")]
        public string Code { get; set; }

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("tax_rates")]
    public class TaxRate : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("rate")]
        public decimal Rate { get; set; }

        [Column("organization_id")]
        public string OrganizationId { get; set; }

        [Column("is_default")]
        public bool IsDefault { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("orders")]
    public class Order : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("order_number")]
        public string OrderNumber { get; set; }

        [Column("organization_id")]
        public string OrganizationId { get; set; }

        [Column("location_id")]
        public string LocationId { get; set; }

        [Column("terminal_id")]
        public string TerminalId { get; set; }

        [Column("staff_id")]
        public string StaffId { get; set; }

        [Column("customer_id")]
        public string CustomerId { get; set; }

        [Column("order_status")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public OrderStatus Status { get; set; }

        [Column("order_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public OrderType Type { get; set; }

        [Column("subtotal")]
        public decimal Subtotal { get; set; }

        [Column("tax_amount")]
        public decimal TaxAmount { get; set; }

        [Column("discount_amount")]
        public decimal DiscountAmount { get; set; }

        [Column("tip_amount")]
        public decimal TipAmount { get; set; }

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("notes")]
        public string Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }
    }

    [Table("order_items")]
    public class OrderItem : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("order_id")]
        public string OrderId { get; set; }

        [Column("item_id")]
        public string ItemId { get; set; }

        [Column("name")]
        public string Name { get; set; } // Snapshot of item name at time of order

        [Column("quantity")]
        public decimal Quantity { get; set; }

        [Column("unit_price")]
        public decimal UnitPrice { get; set; }

        [Column("discount_amount")]
        public decimal DiscountAmount { get; set; }

        [Column("tax_amount")]
        public decimal TaxAmount { get; set; }

        [Column("total_price")]
        public decimal TotalPrice { get; set; }

        [Column("notes")]
        public string Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("order_item_modifiers")]
    public class OrderItemModifier : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("order_item_id")]
        public string OrderItemId { get; set; }

        [Column("modifier_id")]
        public string ModifierId { get; set; }

        [Column("name")]
        public string Name { get; set; } // Snapshot of modifier name

        [Column("price_adjustment")]
        public decimal PriceAdjustment { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("payments")]
    public class Payment : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("order_id")]
        public string OrderId { get; set; }

        [Column("payment_method")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PaymentMethod PaymentMethod { get; set; }

        [Column("payment_status")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PaymentStatus Status { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("payment_reference")]
        public string PaymentReference { get; set; }

        [Column("transaction_id")]
        public string TransactionId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("customers")]
    public class Customer : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("organization_id")]
        public string OrganizationId { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; }

        [Column("last_name")]
        public string LastName { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        [Column("address")]
        public string Address { get; set; }

        [Column("loyalty_points")]
        public int LoyaltyPoints { get; set; }

        [Column("notes")]
        public string Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("last_visit")]
        public DateTime? LastVisit { get; set; }
    }

    [Table("inventory_transactions")]
    public class InventoryTransaction : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("item_id")]
        public string ItemId { get; set; }

        [Column("organization_id")]
        public string OrganizationId { get; set; }

        [Column("location_id")]
        public string LocationId { get; set; }

        [Column("transaction_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public InventoryTransactionType TransactionType { get; set; }

        [Column("quantity")]
        public decimal Quantity { get; set; }

        [Column("reference_id")]
        public string ReferenceId { get; set; } // Order ID, Purchase Order ID, etc.

        [Column("notes")]
        public string Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("staff_id")]
        public string StaffId { get; set; }
    }

    #region Enums

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ModifierSelectionType
    {
        [EnumMember(Value = "single")]
        Single,
        [EnumMember(Value = "multiple")]
        Multiple
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DiscountType
    {
        [EnumMember(Value = "percentage")]
        Percentage,
        [EnumMember(Value = "fixed_amount")]
        FixedAmount
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OrderStatus
    {
        [EnumMember(Value = "pending")]
        Pending,
        [EnumMember(Value = "in_progress")]
        InProgress,
        [EnumMember(Value = "ready")]
        Ready,
        [EnumMember(Value = "completed")]
        Completed,
        [EnumMember(Value = "cancelled")]
        Cancelled
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OrderType
    {
        [EnumMember(Value = "dine_in")]
        DineIn,
        [EnumMember(Value = "takeaway")]
        Takeaway,
        [EnumMember(Value = "delivery")]
        Delivery
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PaymentMethod
    {
        [EnumMember(Value = "cash")]
        Cash,
        [EnumMember(Value = "credit_card")]
        CreditCard,
        [EnumMember(Value = "debit_card")]
        DebitCard,
        [EnumMember(Value = "mobile_payment")]
        MobilePayment,
        [EnumMember(Value = "gift_card")]
        GiftCard
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PaymentStatus
    {
        [EnumMember(Value = "pending")]
        Pending,
        [EnumMember(Value = "completed")]
        Completed,
        [EnumMember(Value = "failed")]
        Failed,
        [EnumMember(Value = "refunded")]
        Refunded,
        [EnumMember(Value = "partially_refunded")]
        PartiallyRefunded
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum InventoryTransactionType
    {
        [EnumMember(Value = "purchase")]
        Purchase,
        [EnumMember(Value = "sale")]
        Sale,
        [EnumMember(Value = "adjustment")]
        Adjustment,
        [EnumMember(Value = "waste")]
        Waste,
        [EnumMember(Value = "transfer")]
        Transfer
    }

    #endregion

}

