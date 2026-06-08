namespace Edemly.Server.Infrastructure.Hosting
{
    public interface IPublicUrlProvider
    {
        string? GetPublicBaseUrl();
    }
}