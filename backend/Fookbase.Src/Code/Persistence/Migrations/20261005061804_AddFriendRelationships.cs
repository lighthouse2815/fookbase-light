using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_UserId2",
                table: "FriendRequests",
                column: "UserId2");

            migrationBuilder.CreateIndex(
                name: "IX_FriendNotifications_ActorUserId",
                table: "FriendNotifications",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendNotifications_FriendRequestId",
                table: "FriendNotifications",
                column: "FriendRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockedUserId",
                table: "BlockedUsers",
                column: "BlockedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockerUserId",
                table: "BlockedUsers",
                column: "BlockerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendNotifications_AspNetUsers_ActorUserId",
                table: "FriendNotifications",
                column: "ActorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendNotifications_AspNetUsers_RecipientUserId",
                table: "FriendNotifications",
                column: "RecipientUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendNotifications_FriendRequests_FriendRequestId",
                table: "FriendNotifications",
                column: "FriendRequestId",
                principalTable: "FriendRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_ReceiverUserId",
                table: "FriendRequests",
                column: "ReceiverUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_SenderUserId",
                table: "FriendRequests",
                column: "SenderUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_UserId1",
                table: "FriendRequests",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_UserId2",
                table: "FriendRequests",
                column: "UserId2",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Friendships_AspNetUsers_UserId1",
                table: "Friendships",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Friendships_AspNetUsers_UserId2",
                table: "Friendships",
                column: "UserId2",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFollows_AspNetUsers_FollowerUserId",
                table: "UserFollows",
                column: "FollowerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFollows_AspNetUsers_FollowingUserId",
                table: "UserFollows",
                column: "FollowingUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockedUserId",
                table: "BlockedUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockerUserId",
                table: "BlockedUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendNotifications_AspNetUsers_ActorUserId",
                table: "FriendNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendNotifications_AspNetUsers_RecipientUserId",
                table: "FriendNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendNotifications_FriendRequests_FriendRequestId",
                table: "FriendNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_ReceiverUserId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_SenderUserId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_UserId1",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_UserId2",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Friendships_AspNetUsers_UserId1",
                table: "Friendships");

            migrationBuilder.DropForeignKey(
                name: "FK_Friendships_AspNetUsers_UserId2",
                table: "Friendships");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFollows_AspNetUsers_FollowerUserId",
                table: "UserFollows");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFollows_AspNetUsers_FollowingUserId",
                table: "UserFollows");

            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_UserId2",
                table: "FriendRequests");

            migrationBuilder.DropIndex(
                name: "IX_FriendNotifications_ActorUserId",
                table: "FriendNotifications");

            migrationBuilder.DropIndex(
                name: "IX_FriendNotifications_FriendRequestId",
                table: "FriendNotifications");
        }
    }
}
