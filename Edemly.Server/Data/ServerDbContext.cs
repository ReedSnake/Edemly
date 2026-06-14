using Edemly.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Data
{
    public class ServerDbContext : DbContext
    {
        public ServerDbContext(DbContextOptions<ServerDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<LoginInfo> LoginInfos { get; set; } = null!;
        public DbSet<Session> Sessions { get; set; } = null!;
        public DbSet<Chat> Chats { get; set; } = null!;
        public DbSet<ChatMember> ChatMembers { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<Note> Notes { get; set; } = null!;
        public DbSet<Reminding> Remindings { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<Call> Calls { get; set; } = null!; // added
        public DbSet<CallParticipant> CallParticipants { get; set; } = null!;

        public DbSet<Company> Companies { get; set; } = null!;
        public DbSet<Email> Emails { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<LoginInfo>()
                .HasIndex(l => l.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.LoginInfoId)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique();

            modelBuilder.Entity<Session>()
                .HasIndex(s => s.UserId)
                .IsUnique();

            modelBuilder.Entity<Session>()
                .HasIndex(s => s.SessionToken)
                .IsUnique();

            modelBuilder.Entity<Note>()
                .HasOne(n => n.TargetUser)
                .WithMany(u => u.NotesAboutUser)
                .HasForeignKey(n => n.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Note>()
                .HasOne(n => n.Creator)
                .WithMany(u => u.NotesCreatedByUser)
                .HasForeignKey(n => n.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Note>()
                .HasIndex(n => new { n.CreatorId, n.TargetUserId })
                .IsUnique();

            modelBuilder.Entity<User>()
                .Property(u => u.SubscriptionStatus)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<Chat>()
                .Property(c => c.Type)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<ChatMember>()
                .Property(cm => cm.Role)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<ChatMember>()
                .HasIndex(cm => new { cm.ChatId, cm.UserId })
                .IsUnique();

            modelBuilder.Entity<ChatMember>()
                .HasIndex(cm => new { cm.UserId, cm.ChatId });

            modelBuilder.Entity<Message>()
                .Property(m => m.Type)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<Message>()
                .HasIndex(m => new { m.ChatId, m.SentAt, m.Id });

            modelBuilder.Entity<Payment>()
                .Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.TransactionId)
                .IsUnique();

            modelBuilder.Entity<Payment>()
                .HasIndex(p => new { p.UserId, p.Date });

            modelBuilder.Entity<Reminding>()
                .HasIndex(r => new { r.UserId, r.LastTime, r.IsCompleted });

            modelBuilder.Entity<Company>()
                .HasIndex(c => c.Name)
                .IsUnique();

            modelBuilder.Entity<Call>().ToTable("call");

            modelBuilder.Entity<Call>()
                .Property(c => c.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<Call>()
                .Property(c => c.Scope)
                .HasMaxLength(20);

            modelBuilder.Entity<Call>()
                .Property(c => c.MediaKind)
                .HasMaxLength(20);

            modelBuilder.Entity<Call>()
                .Property(c => c.EndReason)
                .HasMaxLength(200);

            modelBuilder.Entity<Call>()
                .HasIndex(c => new { c.ChatId, c.Status });

            modelBuilder.Entity<Call>()
                .HasIndex(c => c.CallUid)
                .IsUnique();

            modelBuilder.Entity<Call>()
                .HasIndex(c => c.ActiveChatId)
                .IsUnique();

            modelBuilder.Entity<CallParticipant>().ToTable("call_participant");

            modelBuilder.Entity<CallParticipant>()
                .Property(cp => cp.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<CallParticipant>()
                .HasOne(cp => cp.Call)
                .WithMany(c => c.Participants)
                .HasForeignKey(cp => cp.CallId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CallParticipant>()
                .HasOne(cp => cp.User)
                .WithMany()
                .HasForeignKey(cp => cp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CallParticipant>()
                .HasIndex(cp => new { cp.CallId, cp.UserId })
                .IsUnique();

            modelBuilder.Entity<CallParticipant>()
                .HasIndex(cp => new { cp.UserId, cp.Status });

            modelBuilder.Entity<CallParticipant>()
                .HasIndex(cp => cp.CurrentLockUserId)
                .IsUnique();

            modelBuilder.Entity<Email>().ToTable("email");
        }
    }
}
