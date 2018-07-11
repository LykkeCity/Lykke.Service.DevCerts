using Lykke.SettingsReader.Attributes;

namespace Lykke.Service.DevCerts.Client 
{
    /// <summary>
    /// DevCerts client settings.
    /// </summary>
    public class DevCertsServiceClientSettings 
    {
        /// <summary>Service url.</summary>
        [HttpCheck("api/isalive")]
        public string ServiceUrl {get; set;}
    }
}
