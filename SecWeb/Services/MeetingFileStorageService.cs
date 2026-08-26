using Microsoft.AspNetCore.Components.Forms;

namespace SecWeb.Services
{
    public sealed class MeetingFileStorageService
    {
        public const long MaxFileSize =
            25L * 1024L * 1024L;

        private readonly string _storageRoot;


        public MeetingFileStorageService(
            IWebHostEnvironment environment)
        {
            _storageRoot =
                Path.Combine(
                    environment.ContentRootPath,
                    "App_Data",
                    "MeetingTaskFiles");


            Directory.CreateDirectory(
                _storageRoot);
        }


        public async Task<StoredMeetingFile>
            SaveAsync(
                IBrowserFile file,
                CancellationToken cancellationToken = default)
        {
            if (file.Size <= 0)
            {
                throw new InvalidOperationException(
                    "The selected file is empty.");
            }


            if (file.Size > MaxFileSize)
            {
                throw new InvalidOperationException(
                    $"The selected file is larger than the {FormatMaximumSize()} upload limit.");
            }


            string originalFileName =
                Path.GetFileName(
                    file.Name);


            if (string.IsNullOrWhiteSpace(
                originalFileName))
            {
                originalFileName =
                    "upload";
            }


            if (originalFileName.Length > 260)
            {
                originalFileName =
                    originalFileName[..260];
            }


            string extension =
                Path.GetExtension(
                    originalFileName);


            if (extension.Length > 20)
            {
                extension =
                    string.Empty;
            }


            string storedFileName =
                $"{Guid.NewGuid():N}{extension}";


            string physicalPath =
                GetPhysicalPath(
                    storedFileName);


            await using Stream input =
                file.OpenReadStream(
                    MaxFileSize,
                    cancellationToken);


            await using FileStream output =
                new(
                    physicalPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);


            await input.CopyToAsync(
                output,
                cancellationToken);


            return new StoredMeetingFile(
                originalFileName,
                storedFileName,
                NormalizeContentType(
                    file.ContentType),
                file.Size);
        }


        public string GetPhysicalPath(
            string storedFileName)
        {
            string safeFileName =
                Path.GetFileName(
                    storedFileName);


            return Path.Combine(
                _storageRoot,
                safeFileName);
        }


        public Task DeleteAsync(
            string? storedFileName)
        {
            if (string.IsNullOrWhiteSpace(
                storedFileName))
            {
                return Task.CompletedTask;
            }


            string physicalPath =
                GetPhysicalPath(
                    storedFileName);


            if (File.Exists(
                physicalPath))
            {
                File.Delete(
                    physicalPath);
            }


            return Task.CompletedTask;
        }


        private static string NormalizeContentType(
            string? contentType)
        {
            string value =
                string.IsNullOrWhiteSpace(contentType)
                    ? "application/octet-stream"
                    : contentType.Trim();


            return value.Length <= 200
                ? value
                : value[..200];
        }


        public static string FormatMaximumSize()
        {
            return
                $"{MaxFileSize / 1024L / 1024L} MB";
        }
    }


    public sealed record StoredMeetingFile(
        string OriginalFileName,
        string StoredFileName,
        string ContentType,
        long SizeBytes);
}
