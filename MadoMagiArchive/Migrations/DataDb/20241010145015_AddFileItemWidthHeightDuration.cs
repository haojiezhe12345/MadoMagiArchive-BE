using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MadoMagiArchive.Migrations.DataDb
{
    /// <inheritdoc />
    public partial class AddFileItemWidthHeightDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                table: "Files",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "Files",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "Files",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "Files");
        }
    }
}
