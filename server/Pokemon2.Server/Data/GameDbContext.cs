using Microsoft.EntityFrameworkCore;

namespace Pokemon2.Server.Data;

public sealed class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    public DbSet<PlayerSave> PlayerSaves => Set<PlayerSave>();
    public DbSet<BattleResult> BattleResults => Set<BattleResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var save = modelBuilder.Entity<PlayerSave>();
        save.ToTable("player_saves");
        save.HasKey(x => x.Id);
        save.Property(x => x.Id).HasColumnName("id");
        save.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(80);
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
        save.HasIndex(x => new { x.UserId, x.Mode, x.SlotNumber }).IsUnique();

        var battle = modelBuilder.Entity<BattleResult>();
        battle.ToTable("battle_results");
        battle.HasKey(x => x.Id);
        battle.Property(x => x.Id).HasColumnName("id");
        battle.Property(x => x.RoomId).HasColumnName("room_id").HasMaxLength(40);
        battle.Property(x => x.PlayerId).HasColumnName("player_id").HasMaxLength(64);
        battle.Property(x => x.PlayerName).HasColumnName("player_name").HasMaxLength(40);
        battle.Property(x => x.MonsterId).HasColumnName("monster_id").HasMaxLength(64);
        battle.Property(x => x.MonsterName).HasColumnName("monster_name").HasMaxLength(40);
        battle.Property(x => x.Won).HasColumnName("won");
        battle.Property(x => x.ServerTick).HasColumnName("server_tick");
        battle.Property(x => x.CreatedAt).HasColumnName("created_at");
        battle.HasIndex(x => new { x.RoomId, x.CreatedAt });
    }
}
