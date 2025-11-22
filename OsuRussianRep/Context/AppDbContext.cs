using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Models;
using OsuRussianRep.Models.ChatStatics;

namespace OsuRussianRep.Context;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ChatUser> ChatUsers { get; set; }
    public DbSet<ChatUserNickHistory> ChatUserNickHistories { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Word> Words { get; set; }
    public DbSet<WordDay> WordsInDay { get; set; }
    public DbSet<IngestOffset> IngestOffsets { get; set; }
    public DbSet<WordUser> WordUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatUser>()
            .HasKey(u => u.Id);
            
        modelBuilder.Entity<ChatUser>()
            .HasIndex(u => u.Nickname)
            .IsUnique();
        
        modelBuilder.Entity<ChatUser>()
            .HasIndex(u => u.Nickname)
            .IsUnique();

        modelBuilder.Entity<ChatUserNickHistory>(e =>
        {
            // составной ключ
            e.HasKey(x => new { x.ChatUserId, x.Nickname });

            // связь: один ChatUser -> много ChatUserNickHistory
            e.HasOne(x => x.ChatUser)
                .WithMany(u => u.OldNicknames)
                .HasForeignKey(x => x.ChatUserId)
                .OnDelete(DeleteBehavior.Cascade); // хочешь Restrict — поменяй
        });
        
        modelBuilder.Entity<Message>(e =>
        {
            e.HasKey(x => x.Seq);                              // PK = Seq
            e.Property(x => x.Seq).ValueGeneratedOnAdd();      // IDENTITY

            e.HasIndex(x => x.Id).IsUnique();                  // Guid уникален (альт. ключ)
            // Можно и так, если хочешь именно AlternateKey:
            // e.HasAlternateKey(x => x.Id);

            e.Property(x => x.Date).HasColumnType("timestamptz");
            e.Property(x => x.Text).IsRequired().HasMaxLength(8000);
            e.Property(x => x.ChatChannel).IsRequired().HasMaxLength(200);

            e.HasIndex(x => x.Date);
            e.HasIndex(x => new { x.ChatChannel, x.Date });
            e.HasIndex(x => new { x.UserId, x.Date });

            e.HasOne(m => m.User)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        
        modelBuilder.Entity<Word>(e =>
        {
            e.HasIndex(w => w.Lemma).IsUnique();
            e.Property(w => w.Lemma).IsRequired().HasMaxLength(128);
            e.ToTable("Words");
        });

        modelBuilder.Entity<WordDay>(e =>
        {
            e.HasKey(x => new { x.Day, x.WordId });
            e.HasOne(x => x.Word)
                .WithMany(w => w.Days)
                .HasForeignKey(x => x.WordId)
                .OnDelete(DeleteBehavior.Restrict); // ← чтобы не снести статистику удалением слова

            e.ToTable("WordDays");
            e.HasIndex(x => x.Day); // на диапазоны дат
        });

        modelBuilder.Entity<IngestOffset>(e =>
        {
            e.HasKey(x => x.Day);
            e.Property(x => x.LastSeq).IsRequired();
            e.ToTable("IngestOffsets");
        });
        
        modelBuilder.Entity<WordUser>(e =>
        {
            e.HasKey(x => new { x.UserId, x.WordId });
            e.HasOne(x => x.Word)
                .WithMany()
                .HasForeignKey(x => x.WordId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.ToTable("WordUsers");
            e.HasIndex(x => x.UserId);
        });

        base.OnModelCreating(modelBuilder);
    }
}

