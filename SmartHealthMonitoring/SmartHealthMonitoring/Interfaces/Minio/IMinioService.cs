using System.IO;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Interfaces.Minio;

public interface IMinioService
{
    Task<string> UploadFileAsync(string bucketName, string objectName, Stream data, string contentType);
    Task<string> GetPresignedUrlAsync(string bucketName, string objectName, int expiryInMinutes);
}
