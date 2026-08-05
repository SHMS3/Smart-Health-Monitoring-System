using Minio;
using Minio.DataModel.Args;
using System.IO;
using System.Threading.Tasks;
using SmartHealthMonitoring.Interfaces.Minio;

namespace SmartHealthMonitoring.Services.Minio
{
    public class MinioService : IMinioService
    {
        private readonly IMinioClient _minioClient;

        public MinioService(IMinioClient minioClient)
        {
            _minioClient = minioClient;
        }

        public async Task<string> UploadFileAsync(string bucketName, string objectName, Stream data, string contentType)
        {
            bool found = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
            if (!found)
            {
                await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
            }

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)

                .WithObject(objectName)
                .WithStreamData(data)
                .WithObjectSize(data.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs);
            return objectName;
        }

        public async Task<string> GetPresignedUrlAsync(string bucketName, string objectName, int expiryInMinutes)
        {
            var args = new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithExpiry(expiryInMinutes * 60); // Đổi phút ra giây

            return await _minioClient.PresignedGetObjectAsync(args);
        }
    }
}
