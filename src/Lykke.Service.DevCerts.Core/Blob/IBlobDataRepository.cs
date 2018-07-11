using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.Core.Blob
{
    public interface IBlobDataRepository
    {
        Task<AzureBlobResult> GetDataAsync(string file = null);
        Task<Tuple<AzureBlobResult, string>> GetDataWithMetaAsync(string file = null);
        Task UpdateBlobAsync(byte[] data, string userName, string ipAddress, string file = null);
        string GetETag(string file = null);
        Task<string> GetLastModified(string file = null);
        Task DelBlobAsync(string file = null);
        Task<IEnumerable<AzureBlobResult>> GetBlobFilesDataAsync();
        Task<List<string>> GetExistingFileNames();
    }
}
