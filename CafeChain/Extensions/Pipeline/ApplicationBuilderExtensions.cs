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

            app.UseAuthorization();

            return app;
        }

        private static IApplicationBuilder UseCafeChainLocalization(this IApplicationBuilder app)
        {
            var vietnamese = new CultureInfo("vi-VN");
            var english = new CultureInfo("en-US");

            var localizationOptions = new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(vietnamese),
                SupportedCultures = new[] { vietnamese, english },
                SupportedUICultures = new[] { vietnamese, english }
            };

            localizationOptions.RequestCultureProviders = new IRequestCultureProvider[]
            {
                new CookieRequestCultureProvider()
            };

            app.UseRequestLocalization(localizationOptions);

            return app;
        }
    }
}
