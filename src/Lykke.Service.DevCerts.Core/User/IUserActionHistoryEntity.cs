using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Service.DevCerts.Core.User
{
    public interface IUserActionHistoryEntity
    {
        string UserEmail { get; set; }
        DateTime ActionDate { get; set; }
        string IpAddress { get; set; }
        string ControllerName { get; set; }
        string ActionName { get; set; }
        string Params { get; set; }
    }
}
