using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace OPG.Signage.APIServer
{
	public class JSONServerSSL : JSONServer
	{
        private TcpListener ListenerSSL;
		private static IJSONServerSSLCertificate cert;
        private bool dualServer = false;

        public int SSLPort { get; private set; } = 444;

        public JSONServerSSL(IJSONServerSSLCertificate certIn, string ipIn, string hostname, bool dualServerIn, int portIn = 8081) : base((dualServerIn ? portIn : 8081), ipIn, hostname)
		{
			cert = certIn;
			dualServer = dualServerIn;
		}

		private bool JSONStarted = false;

		public new async Task StartAsync()
		{
			if (!JSONStarted && dualServer)
			{
				await base.StartAsync();
                JSONStarted = true;
			}
			else
			{
				RegisterDeviceAsync(IP, SSLPort, Hostname);
			}

			try
			{
				ListenerSSL = new TcpListener(IPAddress.Any, SSLPort);
				ListenerSSL.Start();
				SSLPort = ((IPEndPoint)ListenerSSL.LocalEndpoint).Port;
				Debug.WriteLine("Listening for ssl on port " + SSLPort);
			}
			catch (Exception e)
			{
				Debug.WriteLine("SSL Start error");
				Debug.WriteLine(e.Message);
				Debug.WriteLine(e.StackTrace);
				SSLPort++;
				await StartAsync().ConfigureAwait(false);
			}

			while(true)
			{
				try
				{
					TcpClient client = ListenerSSL.AcceptTcpClient();
					ProcessRequestSSL(client);
				}
				catch (Exception e)
				{
					Debug.WriteLine("SSL Accept error");
					Debug.WriteLine(e.Message);
					Debug.WriteLine(e.StackTrace);
				}
			}
		}

		private void ProcessRequestSSL(TcpClient client) => Task.Run(async () =>
		{
			int conId = id++;
			string rhostname = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
			Debug.WriteLine("{" + conId + "} ssl New connection - " + rhostname);

            SslStream ssl = null;
			try
			{
				ssl = new SslStream(client.GetStream(), false);
                ssl.AuthenticateAsServer(cert.Load(), false, SslProtocols.Tls12, false);

                await _doRequest(conId, rhostname, ssl.AsInputStream(), ssl);
            }
			catch (Exception e)
			{
				Debug.WriteLine("SSL stream error");
				Debug.WriteLine(e.Message);
				Debug.WriteLine(e.StackTrace);
				return;
			}
            finally
            {
                ssl?.Dispose();
            }

			Debug.WriteLine("{" + conId + "} ssl Connection done - " + rhostname);
		});
	}
}
