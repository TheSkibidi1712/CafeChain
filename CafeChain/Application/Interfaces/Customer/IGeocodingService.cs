namespace CafeChain.Application.Interfaces.Customers
{
    public interface IGeocodingService
    {
        /// <summary>
        /// Chuyển đổi một địa chỉ dạng chuỗi thành toạ độ (Latitude, Longitude).
        /// </summary>
        Task<(decimal? lat, decimal? lng)> GetCoordinatesAsync(string address);
    }
}
