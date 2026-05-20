using Microsoft.EntityFrameworkCore;

namespace Pokemon2.Server.Data;

public sealed class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    public DbSet<PlayerSave> PlayerSaves => Set<PlayerSave>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var save = modelBuilder.Entity<PlayerSave>();
        save.ToTable("player_saves");
        save.HasKey(x => x.Id);
        save.Property(x => x.Id).HasColumnName("id");
        save.Property(x => x.SlotNumber).HasColumnName("slot_number");
        save.Property(x => x.Mode).HasColumnName("mode").HasMaxLength(24);
        save.Property(x => x.PlayerName).HasColumnName("player_name").HasMaxLength(40);
        save.Property(x => x.CurrentMap).HasColumnName("current_map").HasMaxLength(64);
        save.Property(x => x.PositionX).HasColumnName("position_x");
        save.Property(x => x.PositionY).HasColumnName("position_y");
        save.Property(x => x.StarterId).HasColumnName("starter_id").HasMaxLength(32);
        save.Property(x => x.StarterName).HasColumnName("starter_name").HasMaxLength(40);
        save.Property(x => x.StarterLevel).HasColumnName("starter_level");
        save.Property(x => x.StarterCurrentHp).HasColumnName("starter_current_hp");
        save.Property(x => x.PlayTimeSeconds).HasColumnName("play_time_seconds");
        save.Property(x => x.EventsJson).HasColumnName("events_json").HasColumnType("jsonb");
        save.Property(x => x.GameStateJson).HasColumnName("game_state_json").HasColumnType("jsonb");
        save.Property(x => x.CreatedAt).HasColumnName("created_at");
        save.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        save.HasIndex(x => new { x.Mode, x.SlotNumber }).IsUnique();
    }
}
