using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edemly.Server.Data.Entities
{
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

    [Table("call")]
    public class Call
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }

        [Required]
        [Column("ChatId")]
        public int ChatId { get; set; }

        [Required]
        [Column("InitiatorId")]
        public int InitiatorId { get; set; }

        [MaxLength(200)]
        [Column("CallUid")]
        public string? CallUid { get; set; }

        [Column("Metadata")]
        public string? Metadata { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("Scope")]
        public string Scope { get; set; } = "Direct";

        [Required]
        [MaxLength(20)]
        [Column("MediaKind")]
        public string MediaKind { get; set; } = "Audio";

        [Required]
        [Column("StartedAt")]
        public DateTime StartedAt { get; set; }

        [Column("AnsweredAt")]
        public DateTime? AnsweredAt { get; set; }

        [Column("EndedAt")]
        public DateTime? EndedAt { get; set; }

        [Column("EndedByUserId")]
        public int? EndedByUserId { get; set; }

        [MaxLength(200)]
        [Column("EndReason")]
        public string? EndReason { get; set; }

        [Column("SystemMessageId")]
        public int? SystemMessageId { get; set; }

        [Column("ActiveChatId")]
        public int? ActiveChatId { get; set; }

        [Required]
        [Column("Status")]
        public CallStatus Status { get; set; }

        [ForeignKey(nameof(ChatId))]
        public Chat? Chat { get; set; }

        [ForeignKey(nameof(InitiatorId))]
        public User? Initiator { get; set; }

        public ICollection<CallParticipant> Participants { get; set; } = new List<CallParticipant>();
    }

    [Table("call_participant")]
    public class CallParticipant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }

        [Required]
        [Column("CallId")]
        public int CallId { get; set; }

        [Required]
        [Column("UserId")]
        public int UserId { get; set; }

        [Required]
        [Column("Status")]
        public CallParticipantStatus Status { get; set; } = CallParticipantStatus.Invited;

        [Column("InvitedAt")]
        public DateTime? InvitedAt { get; set; }

        [Column("JoinedAt")]
        public DateTime? JoinedAt { get; set; }

        [Column("LeftAt")]
        public DateTime? LeftAt { get; set; }

        [Column("IsMuted")]
        public bool IsMuted { get; set; }

        [Column("CurrentLockUserId")]
        public int? CurrentLockUserId { get; set; }

        [ForeignKey(nameof(CallId))]
        public Call? Call { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}
