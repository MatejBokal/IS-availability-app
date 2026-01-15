using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvailabilityCollector.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftMatrixModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftMatrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftMatrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PositionShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftMatrixId = table.Column<int>(type: "int", nullable: false),
                    PositionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionShifts_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionShifts_ShiftMatrices_ShiftMatrixId",
                        column: x => x.ShiftMatrixId,
                        principalTable: "ShiftMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShiftEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PositionShiftId = table.Column<int>(type: "int", nullable: false),
                    Days = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ShiftTime = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NumberOfPeople = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftEntries_PositionShifts_PositionShiftId",
                        column: x => x.PositionShiftId,
                        principalTable: "PositionShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PositionShifts_PositionId",
                table: "PositionShifts",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionShifts_ShiftMatrixId",
                table: "PositionShifts",
                column: "ShiftMatrixId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftEntries_PositionShiftId",
                table: "ShiftEntries",
                column: "PositionShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftMatrices_Id",
                table: "ShiftMatrices",
                column: "Id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftEntries");

            migrationBuilder.DropTable(
                name: "PositionShifts");

            migrationBuilder.DropTable(
                name: "ShiftMatrices");
        }
    }
}
