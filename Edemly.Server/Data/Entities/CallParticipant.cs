using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edemly.Server.Data.Entities;

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
