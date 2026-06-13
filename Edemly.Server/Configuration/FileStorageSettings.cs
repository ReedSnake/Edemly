namespace Edemly.Server.Configuration
{
    public class FileStorageSettings
    {
        public string Provider { get; set; } = "Local";
        public string StoragePath { get; set; } = "uploads";
        public string ProfilePicturesFolder { get; set; } = "profile-pictures";
        public string FilesFolder { get; set; } = "files";
        public int MaxFileSizeMB { get; set; } = 50;
        public string BaseUrl { get; set; } = "/uploads";
        public MinioStorageSettings Minio { get; set; } = new();

        public bool UseMinio =>
            string.Equals(Provider, "Minio", StringComparison.OrdinalIgnoreCase);
    }

    public class MinioStorageSettings
    {
        public string Endpoint { get; set; } = "localhost:9000";
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string BucketName { get; set; } = "edemly-uploads";
        public string ObjectPrefix { get; set; } = string.Empty;
        public bool Secure { get; set; }
        public bool AutoCreateBucket { get; set; } = true;
    }
}
