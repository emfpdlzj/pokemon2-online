using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pokemon2.Server.Data;

#nullable disable

namespace Pokemon2.Server.Data.Migrations;

[DbContext(typeof(GameDbContext))]
partial class GameDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.1")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Pokemon2.Server.Data.BattleResult", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<string>("MonsterId")
                    .IsRequired()
                    .HasMaxLength(64)
                    .HasColumnType("character varying(64)")
                    .HasColumnName("monster_id");

                b.Property<string>("MonsterName")
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnType("character varying(40)")
                    .HasColumnName("monster_name");

                b.Property<string>("PlayerId")
                    .IsRequired()
                    .HasMaxLength(64)
                    .HasColumnType("character varying(64)")
                    .HasColumnName("player_id");

                b.Property<string>("PlayerName")
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnType("character varying(40)")
                    .HasColumnName("player_name");

                b.Property<string>("RoomId")
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnType("character varying(40)")
                    .HasColumnName("room_id");

                b.Property<long>("ServerTick")
                    .HasColumnType("bigint")
                    .HasColumnName("server_tick");

                b.Property<bool>("Won")
                    .HasColumnType("boolean")
                    .HasColumnName("won");

                b.HasKey("Id")
                    .HasName("PK_battle_results");

                b.HasIndex("RoomId", "CreatedAt")
                    .HasDatabaseName("ix_battle_results_room_id_created_at");

                b.ToTable("battle_results", (string)null);
            });

        modelBuilder.Entity("Pokemon2.Server.Data.PlayerSave", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<string>("CurrentMap")
                    .IsRequired()
                    .HasMaxLength(64)
                    .HasColumnType("character varying(64)")
                    .HasColumnName("current_map");

                b.Property<string>("EventsJson")
                    .IsRequired()
                    .HasColumnType("jsonb")
                    .HasColumnName("events_json");

                b.Property<string>("GameStateJson")
                    .IsRequired()
                    .HasColumnType("jsonb")
                    .HasColumnName("game_state_json");

                b.Property<string>("Mode")
                    .IsRequired()
                    .HasMaxLength(24)
                    .HasColumnType("character varying(24)")
                    .HasColumnName("mode");

                b.Property<string>("PlayerName")
                    .IsRequired()
                    .HasMaxLength(40)
                    .HasColumnType("character varying(40)")
                    .HasColumnName("player_name");

                b.Property<int>("PositionX")
                    .HasColumnType("integer")
                    .HasColumnName("position_x");

                b.Property<int>("PositionY")
                    .HasColumnType("integer")
                    .HasColumnName("position_y");

                b.Property<long>("PlayTimeSeconds")
                    .HasColumnType("bigint")
                    .HasColumnName("play_time_seconds");

                b.Property<int>("SlotNumber")
                    .HasColumnType("integer")
                    .HasColumnName("slot_number");

                b.Property<int?>("StarterCurrentHp")
                    .HasColumnType("integer")
                    .HasColumnName("starter_current_hp");

                b.Property<string>("StarterId")
                    .HasMaxLength(32)
                    .HasColumnType("character varying(32)")
                    .HasColumnName("starter_id");

                b.Property<int?>("StarterLevel")
                    .HasColumnType("integer")
                    .HasColumnName("starter_level");

                b.Property<string>("StarterName")
                    .HasMaxLength(40)
                    .HasColumnType("character varying(40)")
                    .HasColumnName("starter_name");

                b.Property<DateTimeOffset>("UpdatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("updated_at");

                b.Property<string>("UserId")
                    .IsRequired()
                    .HasMaxLength(80)
                    .HasColumnType("character varying(80)")
                    .HasColumnName("user_id");

                b.HasKey("Id")
                    .HasName("PK_player_saves");

                b.HasIndex("UserId", "Mode", "SlotNumber")
                    .IsUnique()
                    .HasDatabaseName("ix_player_saves_user_mode_slot_number");

                b.ToTable("player_saves", (string)null);
            });
#pragma warning restore 612, 618
    }
}
