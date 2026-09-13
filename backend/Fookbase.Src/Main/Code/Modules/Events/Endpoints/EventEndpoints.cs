using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Events.DTOs.Requests;
using Fookbase.Api.Modules.Events.Services;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Media.Services;
namespace Fookbase.Api.Modules.Events.Endpoints;
public static class EventEndpoints
{ public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder endpoints)
  { var g=endpoints.MapGroup("/api/events").RequireAuthorization();
    g.MapGet("/mine",Mine);g.MapGet("/upcoming",Upcoming);g.MapGet("/discover",Discover);g.MapGet("/invitations/mine",Invitations);g.MapPost("/invitations/{inviteId:guid}/accept",Accept);g.MapPost("/invitations/{inviteId:guid}/decline",Decline);g.MapPost("",Create);g.MapGet("/{id:guid}",Get);g.MapGet("/{id:guid}/cover",Cover);g.MapPatch("/{id:guid}",Update);g.MapDelete("/{id:guid}",Delete);g.MapPost("/{id:guid}/publish",Publish);g.MapPost("/{id:guid}/cancel",Cancel);g.MapPost("/{id:guid}/rsvp",Rsvp);g.MapDelete("/{id:guid}/rsvp",RemoveRsvp);g.MapGet("/{id:guid}/participants",Participants);g.MapPost("/{id:guid}/invites",Invite);g.MapGet("/{id:guid}/posts",Posts);g.MapPost("/{id:guid}/posts",CreatePost);return endpoints; }
  private static Task<IResult> Create(CreateEventRequest r,ClaimsPrincipal p,EventsService s,CancellationToken ct)=>Value(p,x=>s.CreateAsync(x,r,ct),v=>Results.Created($"/api/events/{v.Id}",v));
  private static async Task<IResult> Get(Guid id,ClaimsPrincipal p,EventsService s,CancellationToken ct){var r=await s.GetAsync(id,Actor(p),ct);return r.Succeeded?Results.Ok(r.Value):r.Error!.ToHttpResult();}
  private static async Task<IResult> Cover(Guid id,ClaimsPrincipal p,EventsService s,MediaService media,CancellationToken ct){var r=await s.GetCoverMediaIdAsync(id,Actor(p),ct);if(!r.Succeeded)return r.Error!.ToHttpResult();var url=await media.CreateReadUrlAsync(r.Value!,ct);return url.Succeeded?Results.Redirect(url.Value!.Url):Results.NotFound();}
  private static Task<IResult> Update(Guid id,UpdateEventRequest r,ClaimsPrincipal p,EventsService s,CancellationToken ct)=>Value(p,x=>s.UpdateAsync(x,id,r,ct),Results.Ok);
  private static Task<IResult> Delete(Guid id,ClaimsPrincipal p,EventsService s,CancellationToken ct)=>Plain(p,x=>s.DeleteAsync(x,id,ct));
  private static Task<IResult> Publish(Guid id,ClaimsPrincipal p,EventsService s,CancellationToken ct)=>Value(p,x=>s.PublishAsync(x,id,ct),Results.Ok);
  private static Task<IResult> Cancel(Guid id,ClaimsPrincipal p,EventsService s,CancellationToken ct)=>Value(p,x=>s.CancelAsync(x,id,ct),Results.Ok);
  private static Task<IResult> Rsvp(Guid id,SetEventRsvpRequest r,ClaimsPrincipal p,EventsService s,CancellationToken ct)=>Value(p,x=>s.RsvpAsync(x,id,r.Status,ct),Results.Ok);
  private static Task<IResult> RemoveRsvp(Guid id,ClaimsPrincipal p,EventsService s,CancellationToken ct)=>Plain(p,x=>s.RemoveRsvpAsync(x,id,ct));
  private static Task<IResult> Invite(Guid id,CreateEventInvitationRequest r,ClaimsPrincipal p,EventsService s,CancellationToken ct)=>Value(p,x=>s.InviteAsync(x,id,r.UserId,ct),v=>Results.Created($"/api/events/invitations/{v.Id}",v));
  private static Task<IResult> Accept(Guid inviteId,ClaimsPrincipal p,EventsService s,CancellationToken ct)=>Value(p,x=>s.RespondInviteAsync(x,inviteId,true,ct),Results.Ok);
  private static Task<IResult> Decline(Guid inviteId,ClaimsPrincipal p,EventsService s,CancellationToken ct)=>Value(p,x=>s.RespondInviteAsync(x,inviteId,false,ct),Results.Ok);
  private static Task<IResult> CreatePost(Guid id,CreateEventPostRequest r,ClaimsPrincipal p,EventsService s,CancellationToken ct)=>Value(p,x=>s.CreatePostAsync(x,id,r,ct),v=>Results.Created($"/api/posts/{v.Id}",v));
  private static Task<IResult> Mine(ClaimsPrincipal p,EventsService s,CancellationToken ct,string? cursor=null,int limit=EventsService.DefaultPageSize)=>Value(p,x=>s.GetMineAsync(x,cursor,limit,ct),Results.Ok);
  private static Task<IResult> Upcoming(ClaimsPrincipal p,EventsService s,CancellationToken ct,string? cursor=null,int limit=EventsService.DefaultPageSize)=>Value(p,x=>s.UpcomingAsync(x,cursor,limit,ct),Results.Ok);
  private static Task<IResult> Discover(ClaimsPrincipal p,EventsService s,CancellationToken ct,string? query=null,string? cursor=null,int limit=EventsService.DefaultPageSize)=>Value(p,x=>s.DiscoverAsync(x,query,cursor,limit,ct),Results.Ok);
  private static Task<IResult> Invitations(ClaimsPrincipal p,EventsService s,CancellationToken ct,string? cursor=null,int limit=EventsService.DefaultPageSize)=>Value(p,x=>s.InvitationsAsync(x,cursor,limit,ct),Results.Ok);
  private static Task<IResult> Participants(Guid id,ClaimsPrincipal p,EventsService s,CancellationToken ct,string? cursor=null,int limit=EventsService.DefaultPageSize)=>Value(p,x=>s.ParticipantsAsync(x,id,cursor,limit,ct),Results.Ok);
  private static Task<IResult> Posts(Guid id,ClaimsPrincipal p,EventsService s,CancellationToken ct,string? cursor=null,int limit=EventsService.DefaultPageSize)=>Value(p,x=>s.PostsAsync(x,id,cursor,limit,ct),Results.Ok);
  private static Guid? Actor(ClaimsPrincipal p)=>Guid.TryParse(p.FindFirstValue(JwtRegisteredClaimNames.Sub),out var id)?id:null;
  private static async Task<IResult> Value<T>(ClaimsPrincipal p,Func<Guid,Task<ApplicationResult<T>>> action,Func<T,IResult> ok){var id=Actor(p);if(id is null)return Results.Unauthorized();var r=await action(id.Value);return r.Succeeded?ok(r.Value!):r.Error!.ToHttpResult();}
  private static async Task<IResult> Plain(ClaimsPrincipal p,Func<Guid,Task<ApplicationResult>> action){var id=Actor(p);if(id is null)return Results.Unauthorized();var r=await action(id.Value);return r.Succeeded?Results.NoContent():r.Error!.ToHttpResult();}
}
