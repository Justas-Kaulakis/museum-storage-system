using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseumStorage.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorageZones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageZones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageZones_Code",
                table: "StorageZones",
                column: "Code",
                unique: true);

            migrationBuilder.InsertData(
                table: "StorageZones",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[] { 1L, "DEFAULT", "Numatytoji zona" });

            migrationBuilder.AddColumn<long>(
                name: "StorageZoneId",
                table: "StorageLocations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateIndex(
                name: "IX_StorageLocations_StorageZoneId",
                table: "StorageLocations",
                column: "StorageZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_StorageLocations_StorageZones_StorageZoneId",
                table: "StorageLocations",
                column: "StorageZoneId",
                principalTable: "StorageZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rebuild the location table before dropping the zones it references.
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);
            migrationBuilder.Sql("""
                CREATE TABLE "StorageLocationsBeforeZones" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_StorageLocations" PRIMARY KEY AUTOINCREMENT,
                    "Code" TEXT NOT NULL,
                    "Name" TEXT NOT NULL
                );
                INSERT INTO "StorageLocationsBeforeZones" ("Id", "Code", "Name")
                SELECT "Id", "Code", "Name" FROM "StorageLocations";
                DROP TABLE "StorageLocations";
                ALTER TABLE "StorageLocationsBeforeZones" RENAME TO "StorageLocations";
                CREATE UNIQUE INDEX "IX_StorageLocations_Code" ON "StorageLocations" ("Code");
                DROP TABLE "StorageZones";
                """);
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }
    }
}
