using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.Core.Blob
{
    //TODO: remove and use IBlobStorage from Lykke.AzureStorage
    public interface IBlobStorage
    {
        /// <summary>
        ///     Сохранить двоичный поток в контейнер
        /// </summary>
        /// <param name="container">Имя контейнера</param>
        /// <param name="key">Ключ</param>
        /// <param name="bloblStream">Поток</param>
        Task SaveBlobAsync(string container, string key, Stream bloblStream);

        Task SaveBlobAsync(string container, string key, byte[] blob);

        Task<bool> HasBlobAsync(string container, string key);

        /// <summary>
        ///     Returns datetime of latest modification among all blobs
        /// </summary>
        Task<DateTime> GetBlobsLastModifiedAsync(string container);

        Task<AzureBlobResult> GetAsync(string blobContainer, string key);

        string GetETag(string blobContainer, string key);

        Task<string> GetLastModified(string blobContainer, string key);

        string GetBlobUrl(string container, string key);

        Task<IEnumerable<string>> FindNamesByPrefixAsync(string container, string prefix);


        Task<IEnumerable<string>> GetListOfBlobsAsync(string container);

        Task<IEnumerable<AzureBlobResult>> GetBlobFilesDataAsync(string container);

        Task<List<string>> GetExistingFileNames(string container);

        Task DelBlobAsync(string blobContainer, string key);
    }
}
