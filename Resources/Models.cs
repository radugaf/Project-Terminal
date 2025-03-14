using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

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
        public BusinessType BusinessType { get; set; }

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
        public OrganizationStatus Status { get; set; }

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
        public TerminalType TerminalType { get; set; }

        [Column("device_id")]
        public string DeviceId { get; set; }

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
        public StaffRole Role { get; set; }

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
        Owner,
        Manager,
        Staff
    }

    /// <summary>
    /// Enum for business types
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BusinessType
    {
        Restaurant,
        Cafe,
        Retail,
        Grocery,
        Salon
    }

    /// <summary>
    /// Enum for terminal types
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TerminalType
    {
        Checkout,
        Kitchen,
        Manager,
        Inventory
    }

    /// <summary>
    /// Enum for organization status
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OrganizationStatus
    {
        PendingReview,
        Approved,
        Rejected,
        Suspended
    }
}
