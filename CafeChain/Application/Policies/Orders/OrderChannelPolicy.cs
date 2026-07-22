using CafeChain.Models.Orders;
using CafeChain.Models.Payments;

namespace CafeChain.Application.Policies.Orders
{
    public static class OrderChannels
    {
        public const string PosCounter = "POS_COUNTER";
        public const string WebOrder = "WEB_ORDER";
        public const string Delivery = "DELIVERY";
        public const string LegacyUnknown = "LEGACY_UNKNOWN";
    }

    public static class OrderChannelPolicy
    {
        public static string Classify(string? source, int orderTypeId)
        {
            if (string.Equals(source, "POS", StringComparison.OrdinalIgnoreCase))
                return OrderChannels.PosCounter;

            if (string.Equals(source, "Website", StringComparison.OrdinalIgnoreCase))
            {
                return orderTypeId == Constants.SystemConstants.OrderTypes.Delivery
                    ? OrderChannels.Delivery
                    : OrderChannels.WebOrder;
            }

            return OrderChannels.LegacyUnknown;
        }

        public static bool IsWebOrDelivery(Order order)
            => Classify(order.Source, order.OrderTypeId) is OrderChannels.WebOrder or OrderChannels.Delivery;

        public static string GetPaymentDisplay(IEnumerable<Payment> payments)
            => GetPaymentDisplay(payments
                .Where(x => x.PaymentStatusId is Constants.SystemConstants.PaymentStatuses.Paid
                    or Constants.SystemConstants.PaymentStatuses.Refunded)
                .Select(x => x.PaymentMethod?.Code ?? x.PaymentMethod?.Name));

        public static string GetPaymentDisplay(IEnumerable<string?> paymentMethods)
        {
            var methods = paymentMethods
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (methods.Count > 1)
                return "Thanh toán kết hợp";
            if (methods.Count == 0)
                return "Chưa xác định";

            return methods[0].ToUpperInvariant() switch
            {
                "CASH" or "TIỀN MẶT" => "Tiền mặt",
                "BANK" or "VIETQR" or "CHUYỂN KHOẢN" => "Chuyển khoản VietQR",
                "MOMO" => "Ví điện tử — dữ liệu cũ",
                _ => "Chưa xác định"
            };
        }
    }
}
