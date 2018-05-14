using System.Security.Cryptography.X509Certificates;

namespace OPG.Signage.APIServer
{
	public interface IJSONServerSSLCertificate
	{
		X509Certificate2 Load();
	}
}
