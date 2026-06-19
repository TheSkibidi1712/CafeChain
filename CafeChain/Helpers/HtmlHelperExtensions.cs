using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;

namespace CafeChain.Helpers
{
    public static class HtmlHelperExtensions
    {
        public static string IsActive(this IHtmlHelper htmlHelper, string controllers, string? actions = null, string? areas = null, string cssClass = "active fw-bold")
        {
            var routeData = htmlHelper.ViewContext.RouteData;
            var currentController = routeData.Values["controller"]?.ToString();
            var currentAction = routeData.Values["action"]?.ToString();
            var currentArea = routeData.Values["area"]?.ToString() ?? routeData.DataTokens["area"]?.ToString();

            bool isControllerMatch = string.IsNullOrEmpty(controllers) || controllers.Split(',').Select(c => c.Trim()).Contains(currentController, StringComparer.OrdinalIgnoreCase);
            bool isActionMatch = string.IsNullOrEmpty(actions) || actions.Split(',').Select(a => a.Trim()).Contains(currentAction, StringComparer.OrdinalIgnoreCase);
            bool isAreaMatch = string.IsNullOrEmpty(areas) || areas.Split(',').Select(a => a.Trim()).Contains(currentArea, StringComparer.OrdinalIgnoreCase);

            if (isControllerMatch && isActionMatch && isAreaMatch)
            {
                return cssClass;
            }

            return "";
        }
    }
}
