namespace Edemly.Client.Services
{
    public interface ISecureStorageService
    {
        void SaveToken(string token);

        string? LoadToken();

        void ClearToken();

        bool HasToken();
    }
}