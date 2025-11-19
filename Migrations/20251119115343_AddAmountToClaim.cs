using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMCS.web.Migrations
{
    /// <inheritdoc />
    public partial class AddAmountToClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "Claims",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Claims");
        }
    }
}
