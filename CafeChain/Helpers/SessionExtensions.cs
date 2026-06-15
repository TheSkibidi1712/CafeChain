using System.Text.Json; // Thư viện mặc định của .NET Core
using Newtonsoft.Json;  // Thư viện bên thứ 3 (nếu bạn vẫn muốn dùng)

namespace CafeChain.Helpers
{
    public static class SessionExtensions
    {
        // ==========================================
        // CÁCH 1: Dùng System.Text.Json (Khuyên dùng)
        // ==========================================
        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, System.Text.Json.JsonSerializer.Serialize(value));
        }

        public static T? Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : System.Text.Json.JsonSerializer.Deserialize<T>(value);
        }

        // ==========================================
        // CÁCH 2: Dùng Newtonsoft (Để không bị lỗi code cũ của bạn)
        // ==========================================
        public static void SetObjectAsJson(this ISession session, string key, object value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T? GetObjectFromJson<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : JsonConvert.DeserializeObject<T>(value);
        }
    }
}