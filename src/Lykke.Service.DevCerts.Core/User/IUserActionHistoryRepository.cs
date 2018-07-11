using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.Core.User
{
    public interface IUserActionHistoryRepository
    {
        Task SaveUserActionHistoryAsync(IUserActionHistoryEntity userActionHistory);
    }
}
