using Amazon.S3;
using Amazon.S3.Transfer;
using DataWarehouse.SDK.Attributes;
using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.Plugins.Storage.S3.Engine
{
    /// <summary>
    /// GOLD TIER: AWS S3 Storage Provider.
    /// Dedicated plugin for offloading data to the cloud.
    /// </summary>
    [PluginPriority(50, OperatingMode.Server)]
    public class S3StoragePlugin : IFeaturePlugin, IStorageProvider
    {
        public string Id => "aws-s3-storage";
        public string Name => "AWS S3 Provider";
        public string Version => "5.2.0";
        public string Scheme => "s3";

        private IKernelContext? _context;
        private AmazonS3Client? _s3Client;
        private TransferUtility? _transferUtility;

        public void Initialize(IKernelContext context)
        {
            _context = context;

            // Environment Variables are preferred for Container/Server deployments
            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";

            if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            {
                context.LogWarning("[S3] Credentials missing. Plugin dormant.");
                return;
            }

            var config = new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region) };
            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
            _transferUtility = new TransferUtility(_s3Client);

            context.LogInfo($"[S3] Connected to {region}");
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;

        public async Task SaveAsync(Uri uri, Stream data)
        {
            var (bucket, key) = ParseUri(uri);
            try
            {
                var request = new TransferUtilityUploadRequest
                {
                    InputStream = data,
                    Key = key,
                    BucketName = bucket,
                    AutoCloseStream = false
                };
                await _transferUtility!.UploadAsync(request);
            }
            catch (Exception ex)
            {
                _context?.LogError($"[S3] Upload Failed: {uri}", ex);
                throw;
            }
        }

        public async Task<Stream> LoadAsync(Uri uri)
        {
            var (bucket, key) = ParseUri(uri);
            try
            {
                var response = await _s3Client!.GetObjectAsync(bucket, key);
                return response.ResponseStream;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException($"S3 Blob not found: {uri}", ex);
            }
        }

        public async Task DeleteAsync(Uri uri)
        {
            var (bucket, key) = ParseUri(uri);
            await _s3Client!.DeleteObjectAsync(bucket, key);
        }

        public async Task<bool> ExistsAsync(Uri uri)
        {
            var (bucket, key) = ParseUri(uri);
            try
            {
                await _s3Client!.GetObjectMetadataAsync(bucket, key);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        private static (string bucket, string key) ParseUri(Uri uri)
        {
            // s3://my-bucket/folder/file.txt -> Host=my-bucket, Path=folder/file.txt
            return (uri.Host, uri.AbsolutePath.TrimStart('/'));
        }
    }
}