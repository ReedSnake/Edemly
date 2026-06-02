namespace Edemly.Client.Services
{
    public interface ILanguageService
    {
        bool LoadLanguage(string languageCode);
        string GetText(string category, string key, string? fallback = null);
        string CurrentLanguage { get; }
    }
}