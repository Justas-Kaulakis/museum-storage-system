using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseumStorage.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneEnvironmentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "EnvironmentSettings", newName: "GlobalEnvironmentSettings");

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "StorageZones",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageZones_DeviceId",
                table: "StorageZones",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateTable(
                name: "EnvironmentSettings",
                columns: table => new
                {
                    StorageZoneId = table.Column<long>(type: "INTEGER", nullable: false),
                    FanOnTemperature = table.Column<double>(type: "REAL", nullable: false),
                    FanOffTemperature = table.Column<double>(type: "REAL", nullable: false),
                    FanOnHumidity = table.Column<double>(type: "REAL", nullable: false),
                    FanOffHumidity = table.Column<double>(type: "REAL", nullable: false),
                    TemperatureAlarm = table.Column<double>(type: "REAL", nullable: false),
                    HumidityAlarm = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentSettings", x => x.StorageZoneId);
                    table.ForeignKey(
                        name: "FK_EnvironmentSettings_StorageZones_StorageZoneId",
                        column: x => x.StorageZoneId,
                        principalTable: "StorageZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "StorageZones" ("Code", "Name")
                SELECT 'DEFAULT', 'Numatytoji zona'
                WHERE NOT EXISTS (SELECT 1 FROM "StorageZones");

                INSERT INTO "EnvironmentSettings" (
                    "StorageZoneId", "FanOnTemperature", "FanOffTemperature", "FanOnHumidity",
                    "FanOffHumidity", "TemperatureAlarm", "HumidityAlarm")
                SELECT z."Id", COALESCE(s."FanOnTemperature", 26), COALESCE(s."FanOffTemperature", 25),
                    COALESCE(s."FanOnHumidity", 70), COALESCE(s."FanOffHumidity", 65),
                    COALESCE(s."TemperatureAlarm", 30), COALESCE(s."HumidityAlarm", 80)
                FROM "StorageZones" z
                LEFT JOIN "GlobalEnvironmentSettings" s ON s."Id" = 1;
                """);

            migrationBuilder.DropTable(name: "GlobalEnvironmentSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "EnvironmentSettings", newName: "ZoneEnvironmentSettings");

            migrationBuilder.CreateTable(
                name: "EnvironmentSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    FanOnTemperature = table.Column<double>(type: "REAL", nullable: false),
                    FanOffTemperature = table.Column<double>(type: "REAL", nullable: false),
                    FanOnHumidity = table.Column<double>(type: "REAL", nullable: false),
                    FanOffHumidity = table.Column<double>(type: "REAL", nullable: false),
                    TemperatureAlarm = table.Column<double>(type: "REAL", nullable: false),
                    HumidityAlarm = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentSettings", x => x.Id);
                    table.CheckConstraint("CK_EnvironmentSettings_GlobalId", "Id = 1");
                });

            // The old schema can only hold one configuration. Prefer the default zone when present.
            migrationBuilder.Sql("""
                INSERT INTO "EnvironmentSettings" (
                    "Id", "FanOnTemperature", "FanOffTemperature", "FanOnHumidity",
                    "FanOffHumidity", "TemperatureAlarm", "HumidityAlarm")
                SELECT 1, COALESCE(s."FanOnTemperature", 26), COALESCE(s."FanOffTemperature", 25),
                    COALESCE(s."FanOnHumidity", 70), COALESCE(s."FanOffHumidity", 65),
                    COALESCE(s."TemperatureAlarm", 30), COALESCE(s."HumidityAlarm", 80)
                FROM (SELECT 1)
                LEFT JOIN (
                    SELECT s.*
                    FROM "ZoneEnvironmentSettings" s
                    JOIN "StorageZones" z ON z."Id" = s."StorageZoneId"
                    ORDER BY CASE WHEN z."Code" = 'DEFAULT' THEN 0 ELSE 1 END, z."Id"
                    LIMIT 1
                ) s ON 1 = 1;
                """);

            migrationBuilder.DropTable(name: "ZoneEnvironmentSettings");

            migrationBuilder.DropIndex(
                name: "IX_StorageZones_DeviceId",
                table: "StorageZones");

            migrationBuilder.Sql("""ALTER TABLE "StorageZones" DROP COLUMN "DeviceId";""");
        }
    }
}
