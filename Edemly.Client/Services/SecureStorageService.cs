using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Edemly.Client.Services
{
    public class SecureStorageService : ISecureStorageService
    {
        private static SecureStorageService? _instance;
        private readonly string _tokenFilePath;

        public static SecureStorageService Instance => _instance ??= new SecureStorageService();

        private SecureStorageService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "Edemly");

            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            _tokenFilePath = Path.Combine(appFolder, ".token");
        }

        public void SaveToken(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    ClearToken();
                    return;
                }

                var plainBytes = Encoding.UTF8.GetBytes(token);
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    null, // optional entropy
                    DataProtectionScope.CurrentUser // тільки поточний користувач може розшифрувати
                );

                var base64 = Convert.ToBase64String(encryptedBytes);
                File.WriteAllText(_tokenFilePath, base64);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving token: {ex.Message}");
            }
        }

        public string? LoadToken()
        {
            try
            {
                if (!File.Exists(_tokenFilePath))
                    return null;

                var base64 = File.ReadAllText(_tokenFilePath);
                if (string.IsNullOrWhiteSpace(base64))
                    return null;

                var encryptedBytes = Convert.FromBase64String(base64);
                var plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading token: {ex.Message}");
                ClearToken();
                return null;
            }
        }

        public void ClearToken()
        {
            try
            {
                if (File.Exists(_tokenFilePath))
                    File.Delete(_tokenFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing token: {ex.Message}");
            }
        }

        public bool HasToken()
        {
            return File.Exists(_tokenFilePath) && new FileInfo(_tokenFilePath).Length > 0;
        }
    }
}