//using Minio;
//using Minio.DataModel.Args;
//using System.IO;
//using System.Threading.Tasks;

//namespace SmartHealthMonitoring.Services
//{
//    public interface IMinioService
//    {
//        Task<string> UploadFileAsync(string bucketName, string objectName, Stream data, string contentType);
//        Task<string> GetPresignedUrlAsync(string bucketName, string objectName, int expiryInMinutes);
//    }

//    public class MinioService : IMinioService
//    {
//        private readonly IMinioClient _minioClient;

//        public MinioService(IMinioClient minioClient)
//        {
//            _minioClient = minioClient;
//        }

//        // 1. Hàm Upload File
//        public async Task<string> UploadFileAsync(string bucketName, string objectName, Stream data, string contentType)
//        {
//            // Tự động kiểm tra xem bucket lab-results đã có chưa, chưa có thì tạo
//            //bool found = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
//            //if (!found)
//            //{
//            //    await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
//            //}

//            // Đẩy file lên đĩa
//            var putObjectArgs = new PutObjectArgs()
//                .WithBucket(bucketName)
//                .WithObject(objectName)
//                .WithStreamData(data)
//                .WithObjectSize(data.Length)
//                .WithContentType(contentType);

//            await _minioClient.PutObjectAsync(putObjectArgs);
//            return objectName;
//        }

//        // 2. Hàm sinh Link bảo mật (Presigned URL) có thời hạn
//        public async Task<string> GetPresignedUrlAsync(string bucketName, string objectName, int expiryInMinutes)
//        {
//            var args = new PresignedGetObjectArgs()
//                .WithBucket(bucketName)
//                .WithObject(objectName)
//                .WithExpiry(expiryInMinutes * 60); // Đổi phút ra giây

//            return await _minioClient.PresignedGetObjectAsync(args);
//        }
//    }
//}