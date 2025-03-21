using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;

namespace ProjectTerminal.Resources
{
    /// <summary>
    /// Represents a physical address in the system
    /// </summary>
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

    /// <summary>
    /// Represents a business organization in the system
    /// </summary>
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

    /// <summary>
    /// Represents a physical location belonging to an organization
    /// </summary>
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

    /// <summary>
    /// Represents a POS terminal registered at a location
    /// </summary>
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

        [Column("terminal_type")]
        [EnumDataType(typeof(TerminalType))]
        public string TerminalType { get; set; }

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

    /// <summary>
    /// Represents a staff member in the system
    /// </summary>
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

    /// <summary>
    /// Associates staff members with locations they can access
    /// </summary>
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

    /// <summary>
    /// Defines specific permissions for staff members at locations
    /// </summary>
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

    /// <summary>
    /// Enum for staff roles in the system
    /// </summary>
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

    /// <summary>
    /// Enum for business types
    /// </summary>
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

    /// <summary>
    /// Enum for terminal types
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TerminalType
    {
        [EnumMember(Value = "checkout")]
        Checkout,
        [EnumMember(Value = "kitchen")]
        Kitchen,
        [EnumMember(Value = "manager")]
        Manager,
        [EnumMember(Value = "inventory")]
        Inventory
    }

    /// <summary>
    /// Enum for organization status
    /// </summary>
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
}

