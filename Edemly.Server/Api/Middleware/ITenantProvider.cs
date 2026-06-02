using uchat_server.Data.Entities;

namespace uchat_server.Api.Middleware
{
    public interface ITenantProvider
    {
        Company? CurrentCompany { get; set; }
        bool IsTenant { get; }
    }
}
