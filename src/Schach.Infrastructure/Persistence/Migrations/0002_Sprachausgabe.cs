using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprachausgabe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SprachausgabeVerbrauch",
                columns: table => new
                {
                    Monat = table.Column<DateOnly>(type: "date", nullable: false),
                    Zeichen = table.Column<long>(type: "bigint", nullable: false),
                    Aufrufe = table.Column<int>(type: "integer", nullable: false),
                    Zeitueberschreitungen = table.Column<int>(type: "integer", nullable: false),
                    Fehler = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprachausgabeVerbrauch", x => x.Monat);
                });

            migrationBuilder.CreateTable(
                name: "SpruchStimme",
                columns: table => new
                {
                    Hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    Audio = table.Column<byte[]>(type: "bytea", nullable: false),
                    Zeichen = table.Column<int>(type: "integer", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ZuletztGenutztAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpruchStimme", x => x.Hash);
                    table.CheckConstraint("CK_SpruchStimme_Hash_32", "octet_length(\"Hash\") = 32");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpruchStimme_ZuletztGenutztAm",
                table: "SpruchStimme",
                column: "ZuletztGenutztAm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SprachausgabeVerbrauch");

            migrationBuilder.DropTable(
                name: "SpruchStimme");
        }
    }
}
