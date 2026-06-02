using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Edemly.Client.DTOs
{
    public class CreateChatResponseDto
    {
        public ChatDto Chat { get; set; } = new();
    }

    public class CreateGroupChatResponseDto
    {
        public ChatDto? Chat { get; set; }
    }
}
