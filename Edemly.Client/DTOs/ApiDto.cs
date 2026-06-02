using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Edemly.Client.DTOs
{
    public class SearchUsersResponseDto
    {
        public List<UserDto> Users { get; set; } = new();
        public int Count { get; set; }
    }

    public class GetUserResponseDto
    {
        public UserDto User { get; set; } = new();
    }

    public class CreateChatResponseDto
    {
        public ChatDto Chat { get; set; } = new();
    }

    public class GetUserInfoResponseDto
    {
        public UserInfoDto User { get; set; } = new();
    }

    public class UploadResponseDto
    {
        public string Url { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class NoteResponseDto
    {
        public string Note { get; set; } = string.Empty;
    }

    public class CreateGroupChatResponseDto
    {
        public ChatDto? Chat { get; set; }
    }

    public class UploadResultDto
    {
        public string Url { get; set; } = string.Empty;
    }

    public class UploadFileResponseDto
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
    public class RemindingDto //I should probably refactor this to separate these dtos into their own files
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime LastTime { get; set; }

        public bool ShouldNotify { get; set; }

        public bool ShowTime { get; set; }

        public bool IsCompleted { get; set; }
    }
    public class RemindingCreateDto
    {
        public int Type { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        public string Content { get; set; } = string.Empty;

        [Required]
        public DateTime LastTime { get; set; }

        public bool ShouldNotify { get; set; } = true;

        public bool ShowTime { get; set; } = true;

        public bool IsCompleted { get; set; } = false;
    }

    public class RemindingUpdateDto
    {
        [Required]
        public int Id { get; set; }

        [MaxLength(255)]
        public string? Name { get; set; }

        public string? Content { get; set; }

        public DateTime? LastTime { get; set; }

        public int? Type { get; set; }

        public bool? ShouldNotify { get; set; }

        public bool? ShowTime { get; set; }

        public bool? IsCompleted { get; set; }
    }
}
