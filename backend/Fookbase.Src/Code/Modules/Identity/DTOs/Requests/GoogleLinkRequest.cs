namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record GoogleLinkRequest(string? Code, string? Password, string? Client);
