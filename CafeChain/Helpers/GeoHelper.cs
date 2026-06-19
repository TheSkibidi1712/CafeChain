using System;

namespace CafeChain.Helpers
{
    public static class GeoHelper
    {
        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371d; // Bán kính trái đất tính bằng km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c;
            return d;
        }

        private static double ToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }
    }
}
