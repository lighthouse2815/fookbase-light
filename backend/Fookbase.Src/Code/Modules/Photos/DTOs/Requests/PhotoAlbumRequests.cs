namespace Fookbase.Api.Modules.Photos.DTOs.Requests;

public sealed record CreatePhotoAlbumRequest(string Name, string? Description, string Privacy);
public sealed record UpdatePhotoAlbumRequest(string Name, string? Description, string Privacy);
public sealed record AddAlbumMediaRequest(Guid MediaId);
public sealed record UpdateAlbumMediaRequest(string? Caption);
