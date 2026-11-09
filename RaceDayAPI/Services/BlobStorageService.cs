using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDayAPI.Services
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobContainerClient _containerClient;

        public BlobStorageService(IConfiguration configuration)
        {
            var connectionString = configuration["AzureBlobStorage:ConnectionString"]
                ?? throw new InvalidOperationException(
                    "AzureBlobStorage:ConnectionString is not configured. " +
                    "Set it with dotnet user-secrets.");

            var containerName =
                configuration["AzureBlobStorage:ContainerName"] ?? "event-banners";

            _containerClient = new BlobContainerClient(connectionString, containerName);
        }

        public async Task<string> UploadImageAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            await _containerClient.CreateIfNotExistsAsync(
                PublicAccessType.Blob,
                cancellationToken: cancellationToken);

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var blobName = $"{Guid.NewGuid():N}{extension}";
            var blobClient = _containerClient.GetBlobClient(blobName);

            await blobClient.UploadAsync(
                content,
                new BlobHttpHeaders { ContentType = contentType },
                cancellationToken: cancellationToken);

            return blobClient.Uri.ToString();
        }
    }
}