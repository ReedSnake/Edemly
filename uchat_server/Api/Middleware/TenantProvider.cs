using uchat_server.Data.Entities;

namespace uchat_server.Api.Middleware
{
    public class TenantProvider : ITenantProvider
    {
        public Company? CurrentCompany { get; set; }
        public bool IsTenant => CurrentCompany != null;
    }
}
