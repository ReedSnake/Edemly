using System.ComponentModel.DataAnnotations;

namespace uchat_server.Data.Entities
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
        Txt = 0,

        [Display(Name = "voice")]
        Voice = 1,

        [Display(Name = "video")]
        Video = 2,

        [Display(Name = "photo")]
        Photo = 3,

        [Display(Name = "file")]
        File = 4,           // Загальний файл

        [Display(Name = "document")]
        Document = 5        // Документ (pdf, doc, docx)
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
}