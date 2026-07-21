using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Application.Services.Admin.Staffs;
using CafeChain.Infrastrusture.Interfaces.Admin.Staffs;
using CafeChain.Models.Locations;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Logging;
using Moq;

namespace CafeChain.Tests;

public sealed class StaffScopeDisplayTests
{
    [Theory]
    [InlineData("COUNTRY", "Toàn chuỗi")]
    [InlineData("PROVINCE", "Tỉnh/Thành phố")]
    [InlineData("DISTRICT", "Quận/Huyện")]
    [InlineData("WARD", "Phường/Xã")]
    [InlineData("STORE", "Cửa hàng")]
    public void Scope_codes_have_stable_vietnamese_labels(string code, string expected)
    {
        Assert.Equal(expected, ScopeTypeDisplayNames.FromCode(code));
    }

    [Fact]
    public async Task Scope_references_for_non_global_actor_only_return_allowed_stores_and_locations()
    {
        var repository = new Mock<IAdminStaffRepository>();
        var scopeResolver = new Mock<IScopeAuthorizationService>();
        var cloudinary = new Mock<ICloudinaryService>();
        var allowedStore = new Store
        {
            StoreId = 22,
            Name = "Cửa hàng được cấp",
            Active = true,
            ProvinceId = 2,
            DistrictId = 20,
            WardId = 200
        };
        scopeResolver.Setup(x => x.GetAllowedStoresAsync(9))
            .ReturnsAsync(new List<Store> { allowedStore });
        repository.Setup(x => x.GetProvincesAsync()).ReturnsAsync(new List<Province>
        {
            new() { ProvinceId = 1, Name = "Ngoài phạm vi" },
            new() { ProvinceId = 2, Name = "Trong phạm vi" }
        });

        var logger = new Mock<ILogger<AdminStaffService>>();
        var service = new AdminStaffService(repository.Object, cloudinary.Object, scopeResolver.Object, logger.Object);
        var actor = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("StaffId", "9"),
            new Claim(ClaimTypes.Role, RoleConstants.AreaManager)
        }, "Test"));

        var jsonOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var storesJson = JsonSerializer.Serialize(
            await service.GetScopeReferencesAsync((int)ScopeLevel.Store, actor), jsonOptions);
        var provincesJson = JsonSerializer.Serialize(
            await service.GetScopeReferencesAsync((int)ScopeLevel.Province, actor), jsonOptions);
        var countries = await service.GetScopeReferencesAsync((int)ScopeLevel.Country, actor);

        Assert.Contains("Cửa hàng được cấp", storesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Ngoài phạm vi", provincesJson, StringComparison.Ordinal);
        Assert.Contains("Trong phạm vi", provincesJson, StringComparison.Ordinal);
        Assert.Empty(countries);
        repository.Verify(x => x.GetActiveStoresAsync(), Times.Never);
    }
}
