using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvailabilityCollector.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matrica",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MesecLeto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatricaJSON = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matrica", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "UrnikKoncni",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UrnikJSON = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MesecLeto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ZaporedniTeden = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrnikKoncni", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "UrnikRazpolozljivost",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UrnikJSON = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MesecLeto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ZaporedniTeden = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrnikRazpolozljivost", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Worker",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ime = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priimek = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DelovnoMesto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VrstaZaposlitve = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Worker", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Razpolozljivost",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RazpolozljivostJSON = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MesecLeto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ZaporedniTeden = table.Column<int>(type: "int", nullable: true),
                    WorkerID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Razpolozljivost", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Razpolozljivost_Worker_WorkerID",
                        column: x => x.WorkerID,
                        principalTable: "Worker",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Status",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkerID = table.Column<int>(type: "int", nullable: false),
                    StUr = table.Column<int>(type: "int", nullable: false),
                    StDelovnihDni = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Status", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Status_Worker_WorkerID",
                        column: x => x.WorkerID,
                        principalTable: "Worker",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Razpolozljivost_WorkerID",
                table: "Razpolozljivost",
                column: "WorkerID");

            migrationBuilder.CreateIndex(
                name: "IX_Status_WorkerID",
                table: "Status",
                column: "WorkerID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Matrica");

            migrationBuilder.DropTable(
                name: "Razpolozljivost");

            migrationBuilder.DropTable(
                name: "Status");

            migrationBuilder.DropTable(
                name: "UrnikKoncni");

            migrationBuilder.DropTable(
                name: "UrnikRazpolozljivost");

            migrationBuilder.DropTable(
                name: "Worker");
        }
    }
}
