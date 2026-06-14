using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edemly.Server.Data.Migrations.CompanyDb
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reminding_user_id",
                table: "reminding");

            migrationBuilder.DropIndex(
                name: "IX_payment_user_id",
                table: "payment");

            migrationBuilder.DropIndex(
                name: "IX_notes_creator_id",
                table: "notes");

            migrationBuilder.DropIndex(
                name: "IX_message_chat_id",
                table: "message");

            migrationBuilder.DropIndex(
                name: "IX_chat_member_chat_id",
                table: "chat_member");

            migrationBuilder.DropIndex(
                name: "IX_chat_member_user_id",
                table: "chat_member");

            migrationBuilder.CreateIndex(
                name: "IX_session_info_session_token",
                table: "session_info",
                column: "session_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reminding_user_id_last_time_is_completed",
                table: "reminding",
                columns: new[] { "user_id", "last_time", "is_completed" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transaction_id",
                table: "payment",
                column: "transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_user_id_date",
                table: "payment",
                columns: new[] { "user_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_notes_creator_id_user_id",
                table: "notes",
                columns: new[] { "creator_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_chat_id_sent_at_id",
                table: "message",
                columns: new[] { "chat_id", "sent_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_member_chat_id_user_id",
                table: "chat_member",
                columns: new[] { "chat_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_member_user_id_chat_id",
                table: "chat_member",
                columns: new[] { "user_id", "chat_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_session_info_session_token",
                table: "session_info");

            migrationBuilder.DropIndex(
                name: "IX_reminding_user_id_last_time_is_completed",
                table: "reminding");

            migrationBuilder.DropIndex(
                name: "IX_payment_transaction_id",
                table: "payment");

            migrationBuilder.DropIndex(
                name: "IX_payment_user_id_date",
                table: "payment");

            migrationBuilder.DropIndex(
                name: "IX_notes_creator_id_user_id",
                table: "notes");

            migrationBuilder.DropIndex(
                name: "IX_message_chat_id_sent_at_id",
                table: "message");

            migrationBuilder.DropIndex(
                name: "IX_chat_member_chat_id_user_id",
                table: "chat_member");

            migrationBuilder.DropIndex(
                name: "IX_chat_member_user_id_chat_id",
                table: "chat_member");

            migrationBuilder.CreateIndex(
                name: "IX_reminding_user_id",
                table: "reminding",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_user_id",
                table: "payment",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_notes_creator_id",
                table: "notes",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "IX_message_chat_id",
                table: "message",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_member_chat_id",
                table: "chat_member",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_member_user_id",
                table: "chat_member",
                column: "user_id");
        }
    }
}
