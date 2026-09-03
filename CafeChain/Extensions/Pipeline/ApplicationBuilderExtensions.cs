using CafeChain.Constants;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace CafeChain.Extensions.Pipeline
{
    public static class ApplicationBuilderExtensions
    {
        public static WebApplication UseCafeChainPipeline(this WebApplication app)
        {
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                DefaultContentType = "application/octet-stream"
            });

            app.UseCafeChainLocalization();

            app.UseRouting();

            app.UseCors(CorsPolicyNames.AllowVitePOS);

            app.UseSession();

            app.UseAuthentication();

            app.UseMiddleware<AuthenticationDiagnosticsMiddleware>();

            app.UseAuthorization();

            return app;
        }

        private static IApplicationBuilder UseCafeChainLocalization(this IApplicationBuilder app)
        {
            var cultureInfo = new CultureInfo("vi-VN")
            {
                NumberFormat = CultureInfo.InvariantCulture.NumberFormat
            };

            var localizationOptions = new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(cultureInfo),
                SupportedCultures = new[] { cultureInfo },
                SupportedUICultures = new[] { cultureInfo }
            };

            app.UseRequestLocalization(localizationOptions);

            return app;
        }
    }
}
