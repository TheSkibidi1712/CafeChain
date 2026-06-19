namespace CafeChain.Application.DTOs.Admin
{
    public class DispatchOrderRequest
    {
        public int OrderId { get; set; }
        
        /// <summary>
        /// "INTERNAL" (Nội bộ) hoặc "EXTERNAL" (Đối tác ngoài)
        /// </summary>
        public string ShipperType { get; set; }

        public int? InternalShipperId { get; set; }

        public string? DeliveryPartner { get; set; }

        public string? PartnerOrderCode { get; set; }
    }
}
