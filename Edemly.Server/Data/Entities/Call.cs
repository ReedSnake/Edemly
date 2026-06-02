using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edemly.Server.Data.Entities
{
    public enum CallStatus
    {
        Pending,
        InProgress,
        Ended,
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
        [Column("StartedAt")]
        public DateTime StartedAt { get; set; }

        [Column("EndedAt")]
        public DateTime? EndedAt { get; set; }

        [Required]
        [Column("Status")]
        public CallStatus Status { get; set; }

        // Navigation properties (за умови, що вони потрібні)
        [ForeignKey(nameof(ChatId))]
        public Chat? Chat { get; set; }

        [ForeignKey(nameof(InitiatorId))]
        public User? Initiator { get; set; }
    }
}
