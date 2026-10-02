using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShoppingQtyNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItem_ShoppingLists_ShoppingListId",
                table: "ShoppingListItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ShoppingListItem",
                table: "ShoppingListItem");

            migrationBuilder.RenameTable(
                name: "ShoppingListItem",
                newName: "ShoppingListItems");

            migrationBuilder.RenameIndex(
                name: "IX_ShoppingListItem_ShoppingListId_Name_Unit",
                table: "ShoppingListItems",
                newName: "IX_ShoppingListItems_ShoppingListId_Name_Unit");

            migrationBuilder.AlterColumn<double>(
                name: "Quantity",
                table: "ShoppingListItems",
                type: "REAL",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShoppingListItems",
                table: "ShoppingListItems",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_ShoppingLists_ShoppingListId",
                table: "ShoppingListItems",
                column: "ShoppingListId",
                principalTable: "ShoppingLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_ShoppingLists_ShoppingListId",
                table: "ShoppingListItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ShoppingListItems",
                table: "ShoppingListItems");

            migrationBuilder.RenameTable(
                name: "ShoppingListItems",
                newName: "ShoppingListItem");

            migrationBuilder.RenameIndex(
                name: "IX_ShoppingListItems_ShoppingListId_Name_Unit",
                table: "ShoppingListItem",
                newName: "IX_ShoppingListItem_ShoppingListId_Name_Unit");

            migrationBuilder.AlterColumn<double>(
                name: "Quantity",
                table: "ShoppingListItem",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShoppingListItem",
                table: "ShoppingListItem",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItem_ShoppingLists_ShoppingListId",
                table: "ShoppingListItem",
                column: "ShoppingListId",
                principalTable: "ShoppingLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
