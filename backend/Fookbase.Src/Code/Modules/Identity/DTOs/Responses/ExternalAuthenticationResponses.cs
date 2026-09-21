namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record ExternalAuthenticationProvidersResponse(bool Google, bool GoogleMobile = false);
