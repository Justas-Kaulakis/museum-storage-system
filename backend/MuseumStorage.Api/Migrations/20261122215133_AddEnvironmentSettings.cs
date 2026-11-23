using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseumStorage.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvironmentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.InsertData(
                table: "EnvironmentSettings",
                columns: new[] { "Id", "FanOffHumidity", "FanOffTemperature", "FanOnHumidity", "FanOnTemperature", "HumidityAlarm", "TemperatureAlarm" },
                values: new object[] { 1, 65.0, 25.0, 70.0, 26.0, 80.0, 30.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnvironmentSettings");
        }
    }
}
