namespace Edemly.Server.Configuration
{
    public class FileStorageSettings
    {
        public string StoragePath { get; set; } = "uploads";
        public string ProfilePicturesFolder { get; set; } = "profile-pictures";
        public string FilesFolder { get; set; } = "files";
        public int MaxFileSizeMB { get; set; } = 50;
        public string BaseUrl { get; set; } = "/uploads";
    }
}