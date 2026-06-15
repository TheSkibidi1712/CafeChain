using CafeChain.Application.Interfaces.Customers;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Customers
{
    public class NominatimGeocodingService : IGeocodingService
    {
        private readonly HttpClient _httpClient;

        public NominatimGeocodingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // Nominatim (OpenStreetMap) yêu cầu User-Agent
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CafeChainApp/1.0");
        }

       public async Task<(decimal? lat, decimal? lng)> GetCoordinatesAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return (null, null);

            try
            {
                // Gọi API Nominatim của OpenStreetMap
                string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(address)}&format=json&limit=1";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var nodes = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    // Nếu có kết quả trả về
                    if (nodes.ValueKind == JsonValueKind.Array && nodes.GetArrayLength() > 0)
                    {
                        var firstNode = nodes[0];
                        if (decimal.TryParse(firstNode.GetProperty("lat").GetString(), out decimal lat) &&
                            decimal.TryParse(firstNode.GetProperty("lon").GetString(), out decimal lon))
                        {
                            return (lat, lon);
                        }
                    }
                }
            }
            catch 
            {
                // Bỏ qua lỗi nếu API down hoặc kết nối mạng có vấn đề
            }

            return (null, null); // Trả về null nếu không tìm thấy tọa độ
        }
    }
}
