using Edemly.Contracts.Notes;
using Edemly.Contracts.Remindings;
using Edemly.Server.Data.Entities;
using System.Linq.Expressions;

namespace Edemly.Server.Application.Common.Mappers
{
    public static class NoteMappings
    {
        public static readonly Expression<Func<Note, NoteDto>> Projection = note => new NoteDto
        {
            Id = note.Id,
            UserId = note.UserId,
            CreatorId = note.CreatorId,
            Content = note.Content
        };

        public static NoteDto ToDto(Note note)
        {
            return new NoteDto
            {
                Id = note.Id,
                UserId = note.UserId,
                CreatorId = note.CreatorId,
                Content = note.Content
            };
        }
    }

    public static class RemindingMappings
    {
        public static readonly Expression<Func<Reminding, RemindingDto>> Projection = reminding => new RemindingDto
        {
            Id = reminding.Id,
            UserId = reminding.UserId,
            Content = reminding.Content,
            CreatedAt = reminding.CreatedAt,
            LastTime = reminding.LastTime,
            ShouldNotify = reminding.ShouldNotify,
            Type = reminding.Type,
            Name = reminding.Name,
            ShowTime = reminding.ShowTime,
            IsCompleted = reminding.IsCompleted
        };

        public static RemindingDto ToDto(Reminding reminding)
        {
            return new RemindingDto
            {
                Id = reminding.Id,
                UserId = reminding.UserId,
                Content = reminding.Content,
                CreatedAt = reminding.CreatedAt,
                LastTime = reminding.LastTime,
                ShouldNotify = reminding.ShouldNotify,
                Type = reminding.Type,
                Name = reminding.Name,
                ShowTime = reminding.ShowTime,
                IsCompleted = reminding.IsCompleted
            };
        }
    }
}