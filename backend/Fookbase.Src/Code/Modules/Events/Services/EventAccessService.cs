using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Microsoft.EntityFrameworkCore;
namespace Fookbase.Api.Modules.Events.Services;
public sealed class EventAccessService(FookbaseDbContext dbContext)
{
    public async Task<bool> CanManageAsync(Event item, Guid userId, CancellationToken ct = default) => item.DeletedAtUtc is null && item.HostType switch
    { EventHostType.USER => item.HostId == userId,
      EventHostType.GROUP => await dbContext.GroupMembers.AsNoTracking().AnyAsync(x => x.GroupId == item.HostId && x.UserId == userId && (x.Role == GroupMemberRole.OWNER || x.Role == GroupMemberRole.ADMIN || x.Role == GroupMemberRole.MODERATOR), ct),
      EventHostType.PAGE => await dbContext.PageMembers.AsNoTracking().AnyAsync(x => x.PageId == item.HostId && x.UserId == userId && (x.Role == PageRole.OWNER || x.Role == PageRole.ADMIN || x.Role == PageRole.EDITOR), ct), _ => false };
    public async Task<bool> CanViewAsync(Event item, Guid? userId, CancellationToken ct = default)
    { if (item.DeletedAtUtc is not null || userId is null) return false;
      if (await CanManageAsync(item,userId.Value,ct)) return true;
      if (item.Status == EventStatus.CANCELLED)
      {
          if (item.HostType == EventHostType.USER && await IsBlockedAsync(userId.Value, item.HostId, ct)) return false;
          if (item.HostType == EventHostType.GROUP && !await dbContext.Groups.AsNoTracking().AnyAsync(group =>
              group.Id == item.HostId && group.DeletedAtUtc == null &&
              (group.Privacy == GroupPrivacy.PUBLIC || dbContext.GroupMembers.Any(member =>
                  member.GroupId == group.Id && member.UserId == userId.Value)), ct)) return false;
          if (item.HostType == EventHostType.PAGE && !await dbContext.Pages.AsNoTracking().AnyAsync(page =>
              page.Id == item.HostId && page.DeletedAtUtc == null &&
              (page.Status == PageStatus.PUBLISHED || dbContext.PageMembers.Any(member =>
                  member.PageId == page.Id && member.UserId == userId.Value)), ct)) return false;
          return await dbContext.EventParticipants.AsNoTracking().AnyAsync(participant =>
              participant.EventId == item.Id && participant.UserId == userId.Value, ct) ||
              await dbContext.EventInvitations.AsNoTracking().AnyAsync(invitation =>
                  invitation.EventId == item.Id && invitation.InviteeUserId == userId.Value &&
                  invitation.Status == EventInvitationStatus.PENDING, ct);
      }
      if (item.Status != EventStatus.PUBLISHED) return false;
      if (item.Privacy == EventPrivacy.PUBLIC) return item.HostType != EventHostType.USER || !await IsBlockedAsync(userId.Value,item.HostId,ct);
      return await dbContext.EventParticipants.AsNoTracking().AnyAsync(x=>x.EventId==item.Id && x.UserId==userId.Value,ct) ||
        await dbContext.EventInvitations.AsNoTracking().AnyAsync(x=>x.EventId==item.Id && x.InviteeUserId==userId.Value && x.Status==EventInvitationStatus.PENDING,ct); }
    public async Task<bool> CanPostAsync(Event item, Guid userId, CancellationToken ct = default) =>
        item.Status == EventStatus.PUBLISHED && await CanViewAsync(item,userId,ct) &&
        (await CanManageAsync(item,userId,ct) || await dbContext.EventParticipants.AsNoTracking().AnyAsync(x=>x.EventId==item.Id && x.UserId==userId,ct));
    public IQueryable<Post> ApplyDirectAccess(IQueryable<Post> posts, PostViewerContext? viewer)
    { var active = posts.Where(x=>x.DeletedAtUtc==null && x.ContainerType==PostContainerType.EVENT);
      if (viewer is null) return active.Where(_=>false);
      var userId=viewer.UserId;
      return active.Where(post => dbContext.Events.Any(item=>item.Id==post.ContainerId && item.DeletedAtUtc==null && item.Status==EventStatus.PUBLISHED &&
        (item.Privacy==EventPrivacy.PUBLIC || dbContext.EventParticipants.Any(p=>p.EventId==item.Id && p.UserId==userId) || dbContext.EventInvitations.Any(i=>i.EventId==item.Id && i.InviteeUserId==userId && i.Status==EventInvitationStatus.PENDING) ||
         (item.HostType==EventHostType.USER && item.HostId==userId) || (item.HostType==EventHostType.GROUP && dbContext.GroupMembers.Any(m=>m.GroupId==item.HostId && m.UserId==userId && (m.Role==GroupMemberRole.OWNER || m.Role==GroupMemberRole.ADMIN || m.Role==GroupMemberRole.MODERATOR))) ||
         (item.HostType==EventHostType.PAGE && dbContext.PageMembers.Any(m=>m.PageId==item.HostId && m.UserId==userId && (m.Role==PageRole.OWNER || m.Role==PageRole.ADMIN || m.Role==PageRole.EDITOR)))))); }
    private Task<bool> IsBlockedAsync(Guid a, Guid b, CancellationToken ct) => dbContext.BlockedUsers.AsNoTracking().AnyAsync(x => (x.BlockerUserId==a && x.BlockedUserId==b) || (x.BlockerUserId==b && x.BlockedUserId==a),ct);
}
