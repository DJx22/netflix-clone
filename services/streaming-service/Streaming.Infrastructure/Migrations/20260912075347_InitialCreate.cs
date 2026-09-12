using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Streaming.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    TitleId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MediaUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.TitleId);
                });

            migrationBuilder.CreateTable(
                name: "PlaybackPositions",
                columns: table => new
                {
                    TitleId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionSeconds = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackPositions", x => new { x.TitleId, x.ProfileId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackPositions_ProfileId",
                table: "PlaybackPositions",
                column: "ProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaAssets");

            migrationBuilder.DropTable(
                name: "PlaybackPositions");
        }
    }
}
