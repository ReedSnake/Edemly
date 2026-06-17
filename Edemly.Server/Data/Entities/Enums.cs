using System.ComponentModel.DataAnnotations;

using Edemly.Contracts.Messages;

namespace Edemly.Server.Data.Entities
{
    public enum SubscriptionStatus
    {
        [Display(Name = "free")]
        Free,

        [Display(Name = "premium")]
        Premium,

        [Display(Name = "vip")]
        Vip
    }

    public enum ChatType
    {
        [Display(Name = "direct")]
        Direct = 0,      // Приватний чат

        [Display(Name = "group")]
        Group = 1,       // Груповий чат

        [Display(Name = "self")]
        Self = 2         // Чат з самим собою
    }

    public enum ChatMemberRole
    {
        [Display(Name = "creator")]
        Creator,

        [Display(Name = "admin")]
        Admin,

        [Display(Name = "base")]
        Base,

        [Display(Name = "banned")]
        Banned
    }

    public enum MessageType
    {
        [Display(Name = "txt")]
        Txt = MessageTypeCodes.Text,

        [Display(Name = "voice")]
        Voice = MessageTypeCodes.Voice,

        [Display(Name = "video")]
        Video = MessageTypeCodes.Video,

        [Display(Name = "photo")]
        Photo = MessageTypeCodes.Photo,

        [Display(Name = "file")]
        File = MessageTypeCodes.File,           // Загальний файл

        [Display(Name = "document")]
        Document = MessageTypeCodes.Document,       // Документ (pdf, doc, docx)

        [Display(Name = "call")]
        Call = MessageTypeCodes.Call
    }

    public enum PaymentStatus
    {
        [Display(Name = "pending")]
        Pending,

        [Display(Name = "paid")]
        Paid,

        [Display(Name = "failed")]
        Failed,

        [Display(Name = "refunded")]
        Refunded
    }

    public enum RemindingType
    {
        [Display(Name = "important")]
        Important = 0,

        [Display(Name = "work")]
        Work = 1,

        [Display(Name = "personal")]
        Personal = 2,

        [Display(Name = "sports")]
        Sports = 3,

        [Display(Name = "study")]
        Study = 4,

        [Display(Name = "entertainment")]
        Entertainment = 5,
    }
    public enum CallStatus
    {
        Pending,
        InProgress,
        Ended,
        Missed,
        Rejected
    }

    public enum CallParticipantStatus
    {
        Invited,
        Ringing,
        Joined,
        Left,
        Rejected,
        Missed
    }

}
