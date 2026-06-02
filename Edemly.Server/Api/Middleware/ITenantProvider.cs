using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.Middleware
{
    public interface ITenantProvider
    {
        Company? CurrentCompany { get; set; }
        bool IsTenant { get; }
    }
}
