using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.Middleware
{
    public class TenantProvider : ITenantProvider
    {
        public Company? CurrentCompany { get; set; }
        public bool IsTenant => CurrentCompany != null;
    }
}