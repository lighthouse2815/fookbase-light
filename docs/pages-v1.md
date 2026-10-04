# Pages V1

Pages are public identities managed by people without exposing the publishing manager in normal Page-post responses.

## Scope

- A Page starts `unpublished`; only its managers can see it until its Owner publishes it.
- Usernames are normalized to lowercase, restricted to `[a-z0-9._]`, reject reserved system names, and use PostgreSQL `citext` plus a filtered unique index for case-insensitive uniqueness among active Pages.
- A `PageMember` is a management role, not a follower. Owner, Admin, Editor and Moderator permissions are enforced server-side.
- Page role invitations are pending/accepted/declined, unique per Page/invitee while pending, and send a `PageRoleInvite` notification.
- Following is idempotent and only allowed for published Pages.
- Avatar and cover are ready images owned by the acting manager. `PageMediaReferences` retain active media and are updated atomically with replacement or Page deletion.

## Posts and visibility

- `PostContainerType.PAGE` preserves Profile and Group enum values and uses the Page Id for `ContainerId` while retaining the real publishing user in the audit field.
- Page Posts are standard public posts only. Owner/Admin/Editor can publish, edit and delete; Owner/Admin/Moderator can remove comments.
- Page-post responses set `authorUserId` to `null` and `displayAuthor` to the Page identity. The publishing manager is not exposed by normal post APIs.
- Published, non-deleted Page and post determine Page-post visibility. Manager profile privacy and blocks do not suppress it.
- Page Posts are excluded from Home Feed and do not participate in Reels or Stories.
- Engagement notifications target the real publishing manager in V1. This is documented behavior until Page-level notification routing exists.

## Key endpoints

- Public: `GET /api/pages/discover`, `GET /api/pages/{idOrUsername}`, `GET /api/pages/{id}/posts` and signed-image redirects at `/avatar` and `/cover`.
- Management: create/update/delete, publish/unpublish, avatar/cover update, roles/invitations, ownership transfer and Page post creation under `/api/pages`.
- Social: idempotent `POST`/`DELETE /api/pages/{id}/follow`, plus `/mine` and `/following`.

## Deferred

Page Blocks, Page Reels, Page Stories, Page Inbox, Ads/Business Manager, badges, Events, Marketplace, global search, mixed Page Home Feed ranking, ML ranking, HLS and CDN delivery are intentionally outside Pages V1.

## M8.1: ngữ nghĩa tác giả của Page post

`Posts.AuthorUserId` luôn lưu `User.Id` của tài khoản đã xác thực thực hiện thao tác publish. Với Page post, `ContainerType = Page` và `ContainerId = Page.Id`; Page là display identity riêng và không phải Identity user.

Publisher thật vẫn là dữ liệu audit nội bộ và là người nhận notification reaction/comment trong V1; self-action vẫn bị loại bỏ. DTO công khai không trả publisher này, chỉ trả `displayAuthor` của Page.

Không có migration dữ liệu cho M8.1: luồng M8 đã truyền actor từ JWT vào `CreatePostInPageAsync`, vì vậy các row Page post đã tồn tại đã lưu publisher là `User.Id`. Sai lệch chỉ ở projection DTO từng thay giá trị hiển thị bằng `Page.Id`. Việc cố chuyển `Page.Id` trong dữ liệu hiện tại sẽ không an toàn và không cần thiết; Post ID, container và timestamp không thay đổi.
