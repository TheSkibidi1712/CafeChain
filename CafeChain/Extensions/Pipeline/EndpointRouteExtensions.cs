using CafeChain.Hubs;

namespace CafeChain.Extensions.Pipeline
{
    public static class EndpointRouteExtensions
    {
        public static WebApplication MapCafeChainEndpoints(this WebApplication app)
        {
            app.MapControllerRoute(
                name: "admin_order_history",
                pattern: "Admin/AdminOrderHistory",
                defaults: new
                {
                    area = "Admin",
                    controller = "AdminOrder",
                    action = "History"
                });

            app.MapControllerRoute(
                name: "areas",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapHub<OrderHub>("/orderHub");
            app.MapHub<PaymentHub>("/paymentHub");
            app.MapHub<PrintBridgeHub>("/hubs/print-bridge");
            app.MapHub<InventoryNotificationHub>("/hubs/inventory-notifications");

            return app;
        }
    }
}
