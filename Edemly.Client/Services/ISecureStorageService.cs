namespace Edemly.Client.Services
{
    /// <summary>
    /// Сервіс для безпечного зберігання чутливих даних (токени, паролі)
    /// </summary>
    public interface ISecureStorageService
    {
        void SaveToken(string token);
        string? LoadToken();
        void ClearToken();
        bool HasToken();
    }
}
