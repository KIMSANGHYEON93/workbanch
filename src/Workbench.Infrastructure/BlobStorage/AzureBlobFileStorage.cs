using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Workbench.Application.Interfaces;

namespace Workbench.Infrastructure.BlobStorage;

public class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;

    public AzureBlobFileStorage(IOptions<FileStorageOptions> options)
    {
        var settings = options.Value;

        _container = new BlobServiceClient(settings.ConnectionString)
            .GetBlobContainerClient(settings.ContainerName);
    }

    public async Task<string> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobPath = Domain.BlobPath.Create(Guid.NewGuid(), fileName);

        // ★ 저장할 때 Content-Disposition 을 attachment 로 못박는다. 그렇게 하지 않으면
        // 업로드된 .html·.svg 가 Blob 도메인에서 그대로 렌더돼 저장형 XSS 가 된다.
        await _container.GetBlobClient(blobPath).UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    ContentDisposition = "attachment",
                },
            },
            cancellationToken);

        return blobPath;
    }

    public async Task<Stream?> OpenReadAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _container.GetBlobClient(blobPath).OpenReadAsync(
                cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) =>
        _container.GetBlobClient(blobPath).DeleteIfExistsAsync(cancellationToken: cancellationToken);
}
