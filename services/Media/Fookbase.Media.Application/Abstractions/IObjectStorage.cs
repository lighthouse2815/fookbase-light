namespace Fookbase.Media.Application.Abstractions;

public interface IObjectStorage
{
    Task PutAsync(
        string objectName,
        Stream content,
        long length,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string objectName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectName,
        CancellationToken cancellationToken = default);
}
