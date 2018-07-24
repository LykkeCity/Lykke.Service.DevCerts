using JetBrains.Annotations;
using Lykke.Sdk.Settings;

namespace Lykke.Service.DevCerts.Settings
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class AppSettings : BaseAppSettings
    {
        public DevCertsSettings DevCertsService { get; set; }        
    }
}
