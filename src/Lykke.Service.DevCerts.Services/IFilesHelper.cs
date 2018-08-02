using Lykke.Service.DevCerts.Core.User;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.Services
{
    public interface IFilesHelper
    {
        Task UpdateDb(bool force = false, IUserEntity userEntity = null);
        Task UpoadCertToBlob(string creds, string userName, string ip);
        string GetCertPass(string creds);
        Task GenerateCertAsync(IUserEntity user, string userName, string ip);
        Task RevokeUser(IUserEntity user, string userName, string ip);
        Task ChangePass(IUserEntity user, string userName, string ip);
        Task GraintAccess(IUserEntity user, string isDev);
    }
}
