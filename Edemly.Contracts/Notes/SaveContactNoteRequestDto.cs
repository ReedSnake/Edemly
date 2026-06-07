using System;
using System.Collections.Generic;
using System.Text;

namespace Edemly.Contracts.Notes
{
    public sealed class SaveContactNoteRequestDto
    {
        public int UserId { get; set; }
        public string NoteText { get; set; } = string.Empty;
    }
}
