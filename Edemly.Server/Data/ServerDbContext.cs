using Microsoft.EntityFrameworkCore;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Data
{
    public class ServerDbContext : DbContext
    {
        public ServerDbContext(DbContextOptions<ServerDbContext> options) : base(options)
        {
        }

        // DbSets
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

        // Master list of companies (tenants)
        public DbSet<Company> Companies { get; set; } = null!;
        public DbSet<Email> Emails { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure unique indexes
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

            // Configure relationships for Note (two foreign keys to User)
            modelBuilder.Entity<Note>()
                .HasOne(n => n.User)
                .WithMany(u => u.NotesAboutUser)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Note>()
                .HasOne(n => n.Creator)
                .WithMany(u => u.NotesCreatedByUser)
                .HasForeignKey(n => n.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure enum conversions to strings
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

            modelBuilder.Entity<Message>()
                .Property(m => m.Type)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            // MySQL specific configurations
            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(10, 2);

            // Companies table
            modelBuilder.Entity<Company>()
                .HasIndex(c => c.Name)
                .IsUnique();

            // Calls table
            // Map Call entity to the table name used by the migration (lowercase 'call')
            modelBuilder.Entity<Call>().ToTable("call");

            modelBuilder.Entity<Call>()
                .Property(c => c.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            // Email table
            modelBuilder.Entity<Email>().ToTable("email");
        }
    }
}