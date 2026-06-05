namespace Edemly.Client.Infrastructure.Storage
{
    public interface ISecureStorageService
    {
        void SaveToken(string token);

        string? LoadToken();

        void ClearToken();

        bool HasToken();
    }
}