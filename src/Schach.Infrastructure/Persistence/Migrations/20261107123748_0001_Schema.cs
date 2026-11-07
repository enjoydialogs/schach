using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Schach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0001_Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Anmeldung",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CodeHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    Fehlversuche = table.Column<short>(type: "smallint", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LaeuftAbAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anmeldung", x => x.Id);
                    table.CheckConstraint("CK_Anmeldung_CodeHash_32", "octet_length(\"CodeHash\") = 32");
                    table.CheckConstraint("CK_Anmeldung_TokenHash_32", "octet_length(\"TokenHash\") = 32");
                });

            migrationBuilder.CreateTable(
                name: "AnmeldungVersuch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnmeldungVersuch", x => x.Id);
                    table.CheckConstraint("CK_AnmeldungVersuch_EmailHash_32", "octet_length(\"EmailHash\") = 32");
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LlmVerbrauch",
                columns: table => new
                {
                    Monat = table.Column<DateOnly>(type: "date", nullable: false),
                    PromptTokens = table.Column<long>(type: "bigint", nullable: false),
                    CompletionTokens = table.Column<long>(type: "bigint", nullable: false),
                    Aufrufe = table.Column<int>(type: "integer", nullable: false),
                    Zeitueberschreitungen = table.Column<int>(type: "integer", nullable: false),
                    Fehler = table.Column<int>(type: "integer", nullable: false),
                    WarnGemeldetAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErschoepftGemeldetAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmVerbrauch", x => x.Monat);
                });

            migrationBuilder.CreateTable(
                name: "Spieler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ZuletztAngemeldetAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Sicherheitsstempel = table.Column<Guid>(type: "uuid", nullable: false),
                    Elo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spieler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bilanz",
                columns: table => new
                {
                    SpielerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GegnerId = table.Column<string>(type: "text", nullable: false),
                    Siege = table.Column<int>(type: "integer", nullable: false),
                    Remis = table.Column<int>(type: "integer", nullable: false),
                    Niederlagen = table.Column<int>(type: "integer", nullable: false),
                    BesiegtAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bilanz", x => new { x.SpielerId, x.GegnerId });
                    table.ForeignKey(
                        name: "FK_Bilanz_Spieler_SpielerId",
                        column: x => x.SpielerId,
                        principalTable: "Spieler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Partie",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpielerId = table.Column<Guid>(type: "uuid", nullable: true),
                    GegnerId = table.Column<string>(type: "text", nullable: false),
                    SpielerFarbe = table.Column<string>(type: "text", nullable: false),
                    Bedenkzeit = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Ergebnis = table.Column<string>(type: "text", nullable: true),
                    Grund = table.Column<string>(type: "text", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LetzterZugAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BeendetAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Halbzuege = table.Column<int>(type: "integer", nullable: false),
                    Fen = table.Column<string>(type: "text", nullable: false),
                    SpielerRestMs = table.Column<long>(type: "bigint", nullable: true),
                    GegnerRestMs = table.Column<long>(type: "bigint", nullable: true),
                    AmZugSeit = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ZaehltFuerElo = table.Column<bool>(type: "boolean", nullable: false),
                    LetztesRemisAngebotBeiZug = table.Column<int>(type: "integer", nullable: true),
                    LetzterVersuchAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EloVorher = table.Column<int>(type: "integer", nullable: true),
                    EloNachher = table.Column<int>(type: "integer", nullable: true),
                    Seed = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    MistralAufrufe = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partie", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Partie_Spieler_SpielerId",
                        column: x => x.SpielerId,
                        principalTable: "Spieler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Spruch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartieId = table.Column<Guid>(type: "uuid", nullable: false),
                    NachHalbzug = table.Column<int>(type: "integer", nullable: false),
                    Ereignis = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Quelle = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spruch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Spruch_Partie_PartieId",
                        column: x => x.PartieId,
                        principalTable: "Partie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Zug",
                columns: table => new
                {
                    PartieId = table.Column<Guid>(type: "uuid", nullable: false),
                    Halbzug = table.Column<int>(type: "integer", nullable: false),
                    Seite = table.Column<string>(type: "text", nullable: false),
                    Uci = table.Column<string>(type: "text", nullable: false),
                    San = table.Column<string>(type: "text", nullable: false),
                    FenDanach = table.Column<string>(type: "text", nullable: false),
                    GespieltAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Engine = table.Column<string>(type: "text", nullable: true),
                    MaiaRang = table.Column<int>(type: "integer", nullable: true),
                    PAnteil = table.Column<double>(type: "double precision", nullable: true),
                    W = table.Column<double>(type: "double precision", nullable: true),
                    Wdl = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zug", x => new { x.PartieId, x.Halbzug });
                    table.ForeignKey(
                        name: "FK_Zug_Partie_PartieId",
                        column: x => x.PartieId,
                        principalTable: "Partie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Anmeldung_Email",
                table: "Anmeldung",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Anmeldung_ErstelltAm",
                table: "Anmeldung",
                column: "ErstelltAm");

            migrationBuilder.CreateIndex(
                name: "IX_Anmeldung_TokenHash",
                table: "Anmeldung",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnmeldungVersuch_EmailHash_ErstelltAm",
                table: "AnmeldungVersuch",
                columns: new[] { "EmailHash", "ErstelltAm" });

            migrationBuilder.CreateIndex(
                name: "IX_AnmeldungVersuch_ErstelltAm",
                table: "AnmeldungVersuch",
                column: "ErstelltAm");

            migrationBuilder.CreateIndex(
                name: "IX_Partie_SpielerId_Offen",
                table: "Partie",
                column: "SpielerId",
                unique: true,
                filter: "\"Status\" = 'Offen'");

            migrationBuilder.CreateIndex(
                name: "IX_Partie_Status_LetzterZugAm",
                table: "Partie",
                columns: new[] { "Status", "LetzterZugAm" });

            migrationBuilder.CreateIndex(
                name: "IX_Spieler_Email",
                table: "Spieler",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Spruch_PartieId_NachHalbzug",
                table: "Spruch",
                columns: new[] { "PartieId", "NachHalbzug" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Anmeldung");

            migrationBuilder.DropTable(
                name: "AnmeldungVersuch");

            migrationBuilder.DropTable(
                name: "Bilanz");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "LlmVerbrauch");

            migrationBuilder.DropTable(
                name: "Spruch");

            migrationBuilder.DropTable(
                name: "Zug");

            migrationBuilder.DropTable(
                name: "Partie");

            migrationBuilder.DropTable(
                name: "Spieler");
        }
    }
}
