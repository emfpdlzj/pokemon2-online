using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pokemon2.Server.Data.Migrations;

public partial class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "battle_results",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                room_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                player_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                player_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                monster_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                monster_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                won = table.Column<bool>(type: "boolean", nullable: false),
                server_tick = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_battle_results", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "player_saves",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                slot_number = table.Column<int>(type: "integer", nullable: false),
                mode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                player_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                current_map = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                position_x = table.Column<int>(type: "integer", nullable: false),
                position_y = table.Column<int>(type: "integer", nullable: false),
                starter_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                starter_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                starter_level = table.Column<int>(type: "integer", nullable: true),
                starter_current_hp = table.Column<int>(type: "integer", nullable: true),
                play_time_seconds = table.Column<long>(type: "bigint", nullable: false),
                events_json = table.Column<string>(type: "jsonb", nullable: false),
                game_state_json = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_player_saves", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_battle_results_room_id_created_at",
            table: "battle_results",
            columns: new[] { "room_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_player_saves_user_mode_slot_number",
            table: "player_saves",
            columns: new[] { "user_id", "mode", "slot_number" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "battle_results");

        migrationBuilder.DropTable(
            name: "player_saves");
    }
}
