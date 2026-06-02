using System;
using System.Collections.Generic;
using System.Text;

namespace Edemly.Contracts.Files
{
    public class UploadProfilePictureResponseDto
    {
        public string Url { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
