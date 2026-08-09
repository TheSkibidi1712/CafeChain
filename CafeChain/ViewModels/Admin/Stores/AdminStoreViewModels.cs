using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Stores;

public sealed class AdminStoreFormVM
{
    public int StoreId { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Address { get; set; } = string.Empty;

    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required] public int? ProvinceId { get; set; }
    [Required] public int? WardId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Read-only display values. Status changes only through ToggleStatus.
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AdminStoreIndexItemVM
{
    public int StoreId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public bool Active { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? ProvinceName { get; init; }
    public string? WardName { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public int StaffCount { get; init; }
    public IReadOnlyList<string> ManagerNames { get; init; } = Array.Empty<string>();
}

public sealed class AdminStoreFormDataVM
{
    public AdminStoreFormVM Store { get; init; } = new();
    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Provinces { get; init; }
        = Array.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
}
