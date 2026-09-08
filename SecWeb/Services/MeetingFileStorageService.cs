using Microsoft.AspNetCore.Components.Forms;

namespace SecWeb.Services
{
    public sealed class MeetingFileStorageService
    {
        public const long MaxFileSize =
            25L * 1024L * 1024L;


        private readonly string _storageRoot;


        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public MeetingFileStorageService(
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            // -----------------------------------------------------
            // PRODUCTION STORAGE
            // -----------------------------------------------------
            //
            // On the Linux server, set:
            //
            // MeetingFiles__StorageRoot=
            //     /var/lib/secweb/MeetingTaskFiles
            //
            // This keeps uploaded files outside the published
            // application so deploying a new version of SecWeb
            // cannot accidentally delete member submissions.
            //
            // -----------------------------------------------------
            // DEVELOPMENT STORAGE
            // -----------------------------------------------------
            //
            // If no production path is configured, SecWeb keeps
            // using:
            //
            // App_Data/MeetingTaskFiles
            //
            // under the normal application content directory.
            //

            string? configuredStorageRoot =
                configuration[
                    "MeetingFiles:StorageRoot"];


            _storageRoot =
                string.IsNullOrWhiteSpace(
                    configuredStorageRoot)

                    ? Path.Combine(
                        environment.ContentRootPath,
                        "App_Data",
                        "MeetingTaskFiles")

                    : Path.GetFullPath(
                        configuredStorageRoot,
                        environment.ContentRootPath);


            Directory.CreateDirectory(
                _storageRoot);
        }


        // =========================================================
        // SAVE FILE
        // =========================================================

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


        // =========================================================
        // GET PHYSICAL FILE PATH
        // =========================================================

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


        // =========================================================
        // DELETE FILE
        // =========================================================

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


        // =========================================================
        // CONTENT TYPE
        // =========================================================

        private static string NormalizeContentType(
            string? contentType)
        {
            string value =
                string.IsNullOrWhiteSpace(
                    contentType)

                    ? "application/octet-stream"

                    : contentType.Trim();


            return value.Length <= 200
                ? value
                : value[..200];
        }


        // =========================================================
        // FILE SIZE DISPLAY
        // =========================================================

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