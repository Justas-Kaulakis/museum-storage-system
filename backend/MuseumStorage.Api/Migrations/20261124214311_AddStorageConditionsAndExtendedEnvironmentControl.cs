using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseumStorage.Api.Migrations
{
    public partial class AddStorageConditionsAndExtendedEnvironmentControl : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: "FanEnabled", table: "SensorReadings", newName: "CoolingEnabled");
            migrationBuilder.RenameColumn(name: "FanOnTemperature", table: "EnvironmentSettings", newName: "CoolingOnTemperature");
            migrationBuilder.RenameColumn(name: "FanOffTemperature", table: "EnvironmentSettings", newName: "CoolingOffTemperature");
            migrationBuilder.RenameColumn(name: "FanOnHumidity", table: "EnvironmentSettings", newName: "DehumidifierOnHumidity");
            migrationBuilder.RenameColumn(name: "FanOffHumidity", table: "EnvironmentSettings", newName: "DehumidifierOffHumidity");

            foreach (var column in new[] { "HeatingEnabled", "HumidifierEnabled", "DehumidifierEnabled" })
                migrationBuilder.AddColumn<bool>(name: column, table: "SensorReadings", type: "INTEGER", nullable: false, defaultValue: false);

            migrationBuilder.AddColumn<int>(name: "LightIntensity", table: "SensorReadings", type: "INTEGER", nullable: false, defaultValue: 0);

            foreach (var column in new[] { "MinTemperature", "MaxTemperature", "MinHumidity", "MaxHumidity", "MinLightLevel", "MaxLightLevel" })
                migrationBuilder.AddColumn<double>(name: column, table: "Exhibits", type: "REAL", nullable: true);

            (string Name, double Default)[] thresholds =
            [
                ("HeatingOnTemperature", 18),
                ("HeatingOffTemperature", 19),
                ("HumidifierOnHumidity", 35),
                ("HumidifierOffHumidity", 40),
                ("MinLightLevel", 300),
                ("MaxLightLevel", 700),
            ];
            foreach (var (column, defaultValue) in thresholds)
                migrationBuilder.AddColumn<double>(name: column, table: "EnvironmentSettings", type: "REAL", nullable: false, defaultValue: defaultValue);

            // Fit new lower thresholds under any unusually low legacy upper thresholds.
            migrationBuilder.Sql("""
                UPDATE "EnvironmentSettings"
                SET "HeatingOffTemperature" = MAX("CoolingOnTemperature" - 1, ("CoolingOnTemperature" - 40.0) / 2.0)
                WHERE "CoolingOnTemperature" <= 19;

                UPDATE "EnvironmentSettings"
                SET "HeatingOnTemperature" = MIN("CoolingOffTemperature",
                    CASE WHEN "HeatingOffTemperature" <= 18
                         THEN MAX("HeatingOffTemperature" - 1, ("HeatingOffTemperature" - 40.0) / 2.0)
                         ELSE 18 END)
                WHERE "HeatingOnTemperature" >= "HeatingOffTemperature"
                   OR "HeatingOnTemperature" > "CoolingOffTemperature";

                UPDATE "EnvironmentSettings"
                SET "HumidifierOffHumidity" = MAX("DehumidifierOnHumidity" - 5, "DehumidifierOnHumidity" / 2.0)
                WHERE "DehumidifierOnHumidity" <= 40;

                UPDATE "EnvironmentSettings"
                SET "HumidifierOnHumidity" = MIN("DehumidifierOffHumidity",
                    CASE WHEN "HumidifierOffHumidity" <= 35
                         THEN MAX("HumidifierOffHumidity" - 5, "HumidifierOffHumidity" / 2.0)
                         ELSE 35 END)
                WHERE "HumidifierOnHumidity" >= "HumidifierOffHumidity"
                   OR "HumidifierOnHumidity" > "DehumidifierOffHumidity";
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite can drop these unindexed columns without rebuilding tables referenced by other records.
            migrationBuilder.Sql("""
                ALTER TABLE "SensorReadings" DROP COLUMN "HeatingEnabled";
                ALTER TABLE "SensorReadings" DROP COLUMN "HumidifierEnabled";
                ALTER TABLE "SensorReadings" DROP COLUMN "DehumidifierEnabled";
                ALTER TABLE "SensorReadings" DROP COLUMN "LightIntensity";
                ALTER TABLE "Exhibits" DROP COLUMN "MinTemperature";
                ALTER TABLE "Exhibits" DROP COLUMN "MaxTemperature";
                ALTER TABLE "Exhibits" DROP COLUMN "MinHumidity";
                ALTER TABLE "Exhibits" DROP COLUMN "MaxHumidity";
                ALTER TABLE "Exhibits" DROP COLUMN "MinLightLevel";
                ALTER TABLE "Exhibits" DROP COLUMN "MaxLightLevel";
                ALTER TABLE "EnvironmentSettings" DROP COLUMN "HeatingOnTemperature";
                ALTER TABLE "EnvironmentSettings" DROP COLUMN "HeatingOffTemperature";
                ALTER TABLE "EnvironmentSettings" DROP COLUMN "HumidifierOnHumidity";
                ALTER TABLE "EnvironmentSettings" DROP COLUMN "HumidifierOffHumidity";
                ALTER TABLE "EnvironmentSettings" DROP COLUMN "MinLightLevel";
                ALTER TABLE "EnvironmentSettings" DROP COLUMN "MaxLightLevel";
                """);

            migrationBuilder.RenameColumn(name: "CoolingEnabled", table: "SensorReadings", newName: "FanEnabled");
            migrationBuilder.RenameColumn(name: "CoolingOnTemperature", table: "EnvironmentSettings", newName: "FanOnTemperature");
            migrationBuilder.RenameColumn(name: "CoolingOffTemperature", table: "EnvironmentSettings", newName: "FanOffTemperature");
            migrationBuilder.RenameColumn(name: "DehumidifierOnHumidity", table: "EnvironmentSettings", newName: "FanOnHumidity");
            migrationBuilder.RenameColumn(name: "DehumidifierOffHumidity", table: "EnvironmentSettings", newName: "FanOffHumidity");
        }
    }
}
