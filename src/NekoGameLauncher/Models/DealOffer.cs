namespace NekoGameLauncher.Models;

public sealed class DealOffer
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string SalePrice { get; set; } = string.Empty;
    public string NormalPrice { get; set; } = string.Empty;
    public decimal SavingsPercent { get; set; }
    public bool IsFree { get; set; }
    public string DealUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EndsAt { get; set; } = string.Empty;

    public string PriceLabel => IsFree ? "FREE" : string.IsNullOrWhiteSpace(SalePrice) ? "Offer" : $"${SalePrice}";
}
