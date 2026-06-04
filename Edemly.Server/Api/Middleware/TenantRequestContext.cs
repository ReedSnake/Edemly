using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.Middleware
{
    public static class TenantRequestContext
    {
        public const string CompanyItemKey = "TenantCompany";

        public static Company? GetCurrentCompany(HttpContext? httpContext, ITenantProvider tenantProvider)
        {
            if (tenantProvider.CurrentCompany != null)
            {
                return tenantProvider.CurrentCompany;
            }

            if (httpContext?.Items.TryGetValue(CompanyItemKey, out var item) == true && item is Company company)
            {
                tenantProvider.CurrentCompany = company;
                return company;
            }

            return null;
        }

        public static void SetCurrentCompany(HttpContext httpContext, ITenantProvider tenantProvider, Company company)
        {
            tenantProvider.CurrentCompany = company;
            httpContext.Items[CompanyItemKey] = company;
        }

        public static void Clear(HttpContext httpContext, ITenantProvider tenantProvider)
        {
            tenantProvider.CurrentCompany = null;
            httpContext.Items.Remove(CompanyItemKey);
        }
    }
}