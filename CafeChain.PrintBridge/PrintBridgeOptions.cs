namespace CafeChain.PrintBridge
{
    /// <summary>
    /// POCO config binding cho section "PrintBridge" trong appsettings.json.
    /// Mỗi instance Worker đại diện cho 1 máy in tại 1 quán.
    /// </summary>
    public class PrintBridgeOptions
    {
        /// <summary>SignalR Hub URL trên Cloud Backend</summary>
        public string HubUrl { get; set; } = "https://localhost:7001/hubs/print-bridge";

        /// <summary>API Key gửi qua header "X-PrintBridge-Key" để xác thực với Hub</summary>
        public string ApiKey { get; set; } = "";

        /// <summary>Store ID — Worker join group PrintBridge_Store_{StoreId}</summary>
        public int StoreId { get; set; } = 1;

        /// <summary>
        /// Printer target: "Cashier", "Kitchen", "Bar".
        /// Worker chỉ in khi PrintJob.PrinterTarget khớp với giá trị này.
        /// </summary>
        public string PrinterTarget { get; set; } = "Cashier";

        /// <summary>IP máy in nhiệt trên LAN (default: localhost cho test)</summary>
        public string PrinterIp { get; set; } = "localhost";

        /// <summary>TCP port máy in (chuẩn ESC/POS = 9100)</summary>
        public int PrinterPort { get; set; } = 9100;

        /// <summary>Heartbeat interval gửi ReportPrinterStatus (giây)</summary>
        public int HeartbeatIntervalSeconds { get; set; } = 30;

        /// <summary>TCP connection timeout (ms)</summary>
        public int TcpTimeoutMs { get; set; } = 5000;

        /// <summary>Số lần retry khi TCP forward thất bại</summary>
        public int MaxRetries { get; set; } = 3;
    }
}
