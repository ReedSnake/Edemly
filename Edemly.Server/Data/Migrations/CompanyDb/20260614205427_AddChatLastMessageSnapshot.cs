using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edemly.Server.Data.Migrations.CompanyDb
{
    /// <inheritdoc />
    public partial class AddChatLastMessageSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "last_message_id",
                table: "chat",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "last_message_sender_id",
                table: "chat",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_message_text",
                table: "chat",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                """
                UPDATE `chat` AS c
                JOIN (
                    SELECT `id`, `chat_id`, `sender_id`, `text`, `sent_at`
                    FROM (
                        SELECT
                            m.`id`,
                            m.`chat_id`,
                            m.`sender_id`,
                            m.`text`,
                            m.`sent_at`,
                            ROW_NUMBER() OVER (
                                PARTITION BY m.`chat_id`
                                ORDER BY m.`sent_at` DESC, m.`id` DESC
                            ) AS rn
                        FROM `message` AS m
                    ) AS ranked
                    WHERE ranked.rn = 1
                ) AS latest ON latest.`chat_id` = c.`id`
                SET
                    c.`last_message_id` = latest.`id`,
                    c.`last_message_sender_id` = latest.`sender_id`,
                    c.`last_message_text` = latest.`text`,
                    c.`last_message_time` = latest.`sent_at`;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_message_id",
                table: "chat");

            migrationBuilder.DropColumn(
                name: "last_message_sender_id",
                table: "chat");

            migrationBuilder.DropColumn(
                name: "last_message_text",
                table: "chat");
        }
    }
}
