using Lykke.Service.DevCerts.AzureRepositories.Extentions;
using Lykke.Service.DevCerts.Core.Blob;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.AzureRepositories.Blob
{
    public class BlobDataRepository : IBlobDataRepository
    {
        private readonly IBlobStorage _blobStorage;

        private readonly string _container;

        private readonly string _historyContainer;

        private readonly string _file;

        public BlobDataRepository(IBlobStorage blobStorage, string container, string historyContainer, string file)
        {
            _blobStorage = blobStorage;
            _container = container;
            _historyContainer = historyContainer;
            _file = file;
        }

        public BlobDataRepository(IBlobStorage blobStorage, string container, string historyContainer)
        {
            _blobStorage = blobStorage;
            _container = container;
            _historyContainer = historyContainer;
        }

        public async Task<AzureBlobResult> GetDataAsync(string file = null)
        {
            return (await GetDataWithMetaAsync(file)).Item1;
        }

        public async Task<Tuple<AzureBlobResult, string>> GetDataWithMetaAsync(string file = null)
        {
            var fileName = GetFileName(file);
            try
            {
                var result = await _blobStorage.GetAsync(_container, fileName);
                return new Tuple<AzureBlobResult, string>(result, result.ETag);
            }
            catch (Exception ex)
            {
                return new Tuple<AzureBlobResult, string>(new AzureBlobResult(), String.Empty);
            }
        }

        public string GetETag(string file = null)
        {
            var fileName = GetFileName(file);
            return _blobStorage.GetETag(_container, fileName);
        }

        public async Task<string> GetLastModified(string file = null)
        {
            var fileName = GetFileName(file);
            return await _blobStorage.GetLastModified(_container, fileName);
        }

        public async Task UpdateBlobAsync(byte[] data, string userName, string ipAddress, string file = null)
        {
            var fileName = GetFileName(file);
            await _blobStorage.SaveBlobAsync(_container, fileName, data);
            if (!string.IsNullOrEmpty(_historyContainer))
            {
                await _blobStorage.SaveBlobAsync(_historyContainer, $"{fileName}_{DateTime.UtcNow.StorageString()}_{userName}_{ipAddress}",
                    data);
            }
        }

        public async Task<List<string>> GetExistingFileNames()
        {
            return await _blobStorage.GetExistingFileNames(_container);
        }

        public async Task DelBlobAsync(string file = null)
        {
            //var fileName = GetFileName(file);
            try
            {
                await _blobStorage.DelBlobAsync(_container, file);
            }
            catch (Exception ex)
            {
                return;
            }
        }

        public async Task<IEnumerable<AzureBlobResult>> GetBlobFilesDataAsync()
        {
            return await _blobStorage.GetBlobFilesDataAsync(_container);
        }

        private string GetFileName(string file)
        {
            return String.IsNullOrWhiteSpace(file) ? _file : file;
        }
    }
}
