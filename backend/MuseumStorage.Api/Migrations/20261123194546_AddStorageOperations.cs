using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseumStorage.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorageOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExhibitId = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    FromStorageLocationId = table.Column<long>(type: "INTEGER", nullable: true),
                    ToStorageLocationId = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageOperations_Exhibits_ExhibitId",
                        column: x => x.ExhibitId,
                        principalTable: "Exhibits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageOperations_StorageLocations_FromStorageLocationId",
                        column: x => x.FromStorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageOperations_StorageLocations_ToStorageLocationId",
                        column: x => x.ToStorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_ExhibitId",
                table: "StorageOperations",
                column: "ExhibitId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_FromStorageLocationId",
                table: "StorageOperations",
                column: "FromStorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_ToStorageLocationId",
                table: "StorageOperations",
                column: "ToStorageLocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StorageOperations");
        }
    }
}
