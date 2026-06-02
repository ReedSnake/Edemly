using System;
using System.Collections.Generic;
using System.Text;

namespace Edemly.Contracts.Files
{
    public class UploadFileResponseDto
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
