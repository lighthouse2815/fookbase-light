using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Fookbase.Api.Modules.Events.Services;
public sealed class EventAccessService(FookbaseDbContext dbContext)
{
    public async Task<bool> CanManageAsync(Event item, Guid userId, CancellationToken ct = default) => item.DeletedAtUtc is null && item.HostType switch
    { EventHostType.User => item.HostId == userId,
      EventHostType.Group => await dbContext.GroupMembers.AsNoTracking().AnyAsync(x => x.GroupId == item.HostId && x.UserId == userId && (x.Role == GroupMemberRole.Owner || x.Role == GroupMemberRole.Admin || x.Role == GroupMemberRole.Moderator), ct),
      EventHostType.Page => await dbContext.PageMembers.AsNoTracking().AnyAsync(x => x.PageId == item.HostId && x.UserId == userId && (x.Role == PageRole.Owner || x.Role == PageRole.Admin || x.Role == PageRole.Editor), ct), _ => false };
    public async Task<bool> CanViewAsync(Event item, Guid? userId, CancellationToken ct = default)
    { if (item.DeletedAtUtc is not null || userId is null) return false;
      if (await CanManageAsync(item,userId.Value,ct)) return true;
      if (item.Status != EventStatus.Published) return false;
      if (item.Privacy == EventPrivacy.Public) return item.HostType != EventHostType.User || !await IsBlockedAsync(userId.Value,item.HostId,ct);
      return await dbContext.EventParticipants.AsNoTracking().AnyAsync(x=>x.EventId==item.Id && x.UserId==userId.Value,ct) ||
        await dbContext.EventInvitations.AsNoTracking().AnyAsync(x=>x.EventId==item.Id && x.InviteeUserId==userId.Value && x.Status==EventInvitationStatus.Pending,ct); }
    public async Task<bool> CanPostAsync(Event item, Guid userId, CancellationToken ct = default) =>
        item.Status == EventStatus.Published && await CanViewAsync(item,userId,ct) &&
        (await CanManageAsync(item,userId,ct) || await dbContext.EventParticipants.AsNoTracking().AnyAsync(x=>x.EventId==item.Id && x.UserId==userId,ct));
    public IQueryable<Post> ApplyDirectAccess(IQueryable<Post> posts, PostViewerContext? viewer)
    { var active = posts.Where(x=>x.DeletedAtUtc==null && x.ContainerType==PostContainerType.Event);
      if (viewer is null) return active.Where(_=>false);
      var userId=viewer.UserId;
      return active.Where(post => dbContext.Events.Any(item=>item.Id==post.ContainerId && item.DeletedAtUtc==null && item.Status==EventStatus.Published &&
        (item.Privacy==EventPrivacy.Public || dbContext.EventParticipants.Any(p=>p.EventId==item.Id && p.UserId==userId) || dbContext.EventInvitations.Any(i=>i.EventId==item.Id && i.InviteeUserId==userId && i.Status==EventInvitationStatus.Pending) ||
         (item.HostType==EventHostType.User && item.HostId==userId) || (item.HostType==EventHostType.Group && dbContext.GroupMembers.Any(m=>m.GroupId==item.HostId && m.UserId==userId && (m.Role==GroupMemberRole.Owner || m.Role==GroupMemberRole.Admin || m.Role==GroupMemberRole.Moderator))) ||
         (item.HostType==EventHostType.Page && dbContext.PageMembers.Any(m=>m.PageId==item.HostId && m.UserId==userId && (m.Role==PageRole.Owner || m.Role==PageRole.Admin || m.Role==PageRole.Editor)))))); }
    private Task<bool> IsBlockedAsync(Guid a, Guid b, CancellationToken ct) => dbContext.BlockedUsers.AsNoTracking().AnyAsync(x => (x.BlockerUserId==a && x.BlockedUserId==b) || (x.BlockerUserId==b && x.BlockedUserId==a),ct);
}
