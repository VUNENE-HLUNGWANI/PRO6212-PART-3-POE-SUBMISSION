namespace RaceDayAPI.Services
{
    public interface IBlobStorageService
    {
        /// <summary>
        /// Uploads an image to Azure Blob Storage and returns its public URL.
        /// </summary>
        Task<string> UploadImageAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default);
    }
}