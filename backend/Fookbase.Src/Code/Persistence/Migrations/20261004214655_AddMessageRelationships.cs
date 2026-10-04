using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderUserId",
                table: "Messages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReactions_UserId",
                table: "MessageReactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageNotifications_ConversationId",
                table: "MessageNotifications",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageNotifications_MessageId",
                table: "MessageNotifications",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_PhotoMediaId",
                table: "Conversations",
                column: "PhotoMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UserId2",
                table: "Conversations",
                column: "UserId2");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationReadCursors_LastReadMessageId",
                table: "ConversationReadCursors",
                column: "LastReadMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_LastDeliveredMessageId",
                table: "ConversationParticipants",
                column: "LastDeliveredMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_LastReadMessageId",
                table: "ConversationParticipants",
                column: "LastReadMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationParticipants_AspNetUsers_UserId",
                table: "ConversationParticipants",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationParticipants_Conversations_ConversationId",
                table: "ConversationParticipants",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationParticipants_Messages_LastDeliveredMessageId",
                table: "ConversationParticipants",
                column: "LastDeliveredMessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationParticipants_Messages_LastReadMessageId",
                table: "ConversationParticipants",
                column: "LastReadMessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationReadCursors_AspNetUsers_UserId",
                table: "ConversationReadCursors",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationReadCursors_Conversations_ConversationId",
                table: "ConversationReadCursors",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationReadCursors_Messages_LastReadMessageId",
                table: "ConversationReadCursors",
                column: "LastReadMessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_AspNetUsers_UserId1",
                table: "Conversations",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_AspNetUsers_UserId2",
                table: "Conversations",
                column: "UserId2",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_MediaAssets_PhotoMediaId",
                table: "Conversations",
                column: "PhotoMediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageAttachments_MediaAssets_MediaId",
                table: "MessageAttachments",
                column: "MediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageNotifications_AspNetUsers_RecipientUserId",
                table: "MessageNotifications",
                column: "RecipientUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageNotifications_Conversations_ConversationId",
                table: "MessageNotifications",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageNotifications_Messages_MessageId",
                table: "MessageNotifications",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageReactions_AspNetUsers_UserId",
                table: "MessageReactions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_SenderUserId",
                table: "Messages",
                column: "SenderUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Conversations_ConversationId",
                table: "Messages",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationParticipants_AspNetUsers_UserId",
                table: "ConversationParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_ConversationParticipants_Conversations_ConversationId",
                table: "ConversationParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_ConversationParticipants_Messages_LastDeliveredMessageId",
                table: "ConversationParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_ConversationParticipants_Messages_LastReadMessageId",
                table: "ConversationParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_ConversationReadCursors_AspNetUsers_UserId",
                table: "ConversationReadCursors");

            migrationBuilder.DropForeignKey(
                name: "FK_ConversationReadCursors_Conversations_ConversationId",
                table: "ConversationReadCursors");

            migrationBuilder.DropForeignKey(
                name: "FK_ConversationReadCursors_Messages_LastReadMessageId",
                table: "ConversationReadCursors");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AspNetUsers_UserId1",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AspNetUsers_UserId2",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_MediaAssets_PhotoMediaId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageAttachments_MediaAssets_MediaId",
                table: "MessageAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageNotifications_AspNetUsers_RecipientUserId",
                table: "MessageNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageNotifications_Conversations_ConversationId",
                table: "MessageNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageNotifications_Messages_MessageId",
                table: "MessageNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageReactions_AspNetUsers_UserId",
                table: "MessageReactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AspNetUsers_SenderUserId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Conversations_ConversationId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SenderUserId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_MessageReactions_UserId",
                table: "MessageReactions");

            migrationBuilder.DropIndex(
                name: "IX_MessageNotifications_ConversationId",
                table: "MessageNotifications");

            migrationBuilder.DropIndex(
                name: "IX_MessageNotifications_MessageId",
                table: "MessageNotifications");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_PhotoMediaId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_UserId2",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_ConversationReadCursors_LastReadMessageId",
                table: "ConversationReadCursors");

            migrationBuilder.DropIndex(
                name: "IX_ConversationParticipants_LastDeliveredMessageId",
                table: "ConversationParticipants");

            migrationBuilder.DropIndex(
                name: "IX_ConversationParticipants_LastReadMessageId",
                table: "ConversationParticipants");
        }
    }
}
