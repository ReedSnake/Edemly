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

    public class CreateGroupChatResponseDto
    {
        public ChatDto? Chat { get; set; }
    }
}
