using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.POS
{
    public class PayOSWebhookPayload
    {
        public string OrderCodeText { get; set; } = null!;
        public decimal Amount { get; set; }
        public string TransactionId { get; set; } = null!;
        public string Description { get; set; } = "";
        public string Status { get; set; } = "00";
        public string RawBody { get; set; } = "";
    }

    public class PayOSWebhookProcessResult
    {
        public string Code { get; set; } = null!;
        public string Message { get; set; } = null!;
        public int? OrderId { get; set; }
        public bool ConfirmedPayment { get; set; }

        public static PayOSWebhookProcessResult From(
            string code,
            string message,
            int? orderId = null,
            bool confirmedPayment = false)
        {
            return new PayOSWebhookProcessResult
            {
                Code = code,
                Message = message,
                OrderId = orderId,
                ConfirmedPayment = confirmedPayment
            };
        }
    }

    public interface IPayOSWebhookProcessor
    {
        Task<PayOSWebhookProcessResult> ProcessAsync(PayOSWebhookPayload payload);
    }
}
