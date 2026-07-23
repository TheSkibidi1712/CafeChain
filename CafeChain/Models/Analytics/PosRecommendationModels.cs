using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Analytics;

public class PosRecommendationCatalog
{
    public long PosRecommendationCatalogId { get; set; }
    public int StoreId { get; set; }
    public int TriggerDrinkId { get; set; }
    public int RecommendedDrinkId { get; set; }
    public decimal Support { get; set; }
    public decimal Confidence { get; set; }
    public decimal Lift { get; set; }
    public decimal Margin { get; set; }
    public int Rank { get; set; }
    public string ModelVersion { get; set; } = "basket-v1";
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public virtual Store Store { get; set; } = null!;
    public virtual Drink TriggerDrink { get; set; } = null!;
    public virtual Drink RecommendedDrink { get; set; } = null!;
}

public class PosRecommendationExposure
{
    public long PosRecommendationExposureId { get; set; }
    public Guid RecommendationSessionId { get; set; }
    public int StoreId { get; set; }
    public int? OrderId { get; set; }
    public string Variant { get; set; } = "CONTROL";
    public string ModelVersion { get; set; } = "basket-v1";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }
    public virtual Store Store { get; set; } = null!;
    public virtual Order? Order { get; set; }
    public virtual ICollection<PosRecommendationExposureItem> Items { get; set; } = new List<PosRecommendationExposureItem>();
}

public class PosRecommendationExposureItem
{
    public long PosRecommendationExposureItemId { get; set; }
    public long PosRecommendationExposureId { get; set; }
    public int TriggerDrinkId { get; set; }
    public int RecommendedDrinkId { get; set; }
    public int Rank { get; set; }
    public bool WasDisplayed { get; set; }
    public bool WasClicked { get; set; }
    public bool WasAdded { get; set; }
    public bool WasPurchased { get; set; }
    public virtual PosRecommendationExposure Exposure { get; set; } = null!;
}

