using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StarterApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rental_request",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    RequestorUserId = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rental_request", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rental_request_item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rental_request_users_RequestorUserId",
                        column: x => x.RequestorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rental_request_ItemId",
                table: "rental_request",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_rental_request_RequestorUserId",
                table: "rental_request",
                column: "RequestorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rental_request");
        }
    }
}
