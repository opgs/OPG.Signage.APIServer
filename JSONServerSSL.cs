using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace OPG.Signage.APIServer
{
	public class JSONServerSSL : JSONServer
	{
		private int _SSLPort = 444;
		private TcpListener ListenerSSL;
		private X509Certificate2 cert;
		private bool dualServer;

		public int SSLPort {
			get
			{
				return _SSLPort;
			}
		}

		public JSONServerSSL(string ipIn, bool dualServerIn, int portIn = 8081) : base((dualServerIn ? portIn : 8081), ipIn)
		{
			byte[] rawCert = File.ReadAllBytes(@"opg.internal.pfx");
			cert = new X509Certificate2(rawCert, "****", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.DefaultKeySet);
			dualServer = dualServerIn;
		}

		private bool JSONStarted = false;

		public new async Task StartAsync()
		{
			if (!JSONStarted && dualServer) { await base.StartAsync(); JSONStarted = true; }
			try
			{
				ListenerSSL = new TcpListener(IPAddress.Any, _SSLPort);
				ListenerSSL.Start();
				_SSLPort = ((IPEndPoint)ListenerSSL.LocalEndpoint).Port;
				Debug.WriteLine("Listening for ssl on port " + _SSLPort);
			}
			catch (Exception e)
			{
				Debug.WriteLine("SSL Start error");
				Debug.WriteLine(e.Message);
				Debug.WriteLine(e.StackTrace);
				_SSLPort++;
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
			int conId = base.id++;
			string hostname = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
			Debug.WriteLine("{" + conId + "} ssl New connection - " + hostname);

			SslStream ssl;
			try
			{
				ssl = new SslStream(client.GetStream(), false);
			}
			catch (Exception e)
			{
				Debug.WriteLine("SSL stream error");
				Debug.WriteLine(e.Message);
				Debug.WriteLine(e.StackTrace);
				return;
			}

			ssl.AuthenticateAsServer(cert, false, SslProtocols.Tls12, false);

			bool persistent = true;
			byte persistentCount = 99;

			while (persistent)
			{
				HTTPReply reply = new HTTPReply(Windows.Web.Http.HttpVersion.Http11);
				try
				{
					HTTPRequest request = await ReadHTTP(conId, ssl.AsInputStream(), hostname);

					Debug.WriteLine("{" + conId + "} ssl Read " + request.Method + " " + request.URLPath + " " + request.Version + " - " + hostname);

					if (!request.Persistent || persistentCount-- < 1)
					{
						persistent = false;
					}

					reply = new HTTPReply(request.Version)
					{
						IsHead = (request.Method == Windows.Web.Http.HttpMethod.Head),
						IsPersistent = persistent
					};

					string finalPath = (request.URLPath.EndsWith("/")) ? request.URLPath.Substring(0, request.URLPath.Length - 1) : request.URLPath;

					bool GetExists = GetPaths.TryGetValue(finalPath, out Func<string, JSONReply> GetEntry);
					bool PostExists = PostPaths.TryGetValue(finalPath, out Func<string, JSONReply> PostEntry);
					JSONReply GetEntryReply = GetExists ? GetEntry(request.URL) : null;
					JSONReply PostEntryReply = PostExists ? GetEntry(request.URL) : null;

					HandleMethod(GetExists, PostExists, ref request, ref reply, ref GetEntryReply, ref PostEntryReply);
				}
				catch (NothingReceivedException e)
				{
					Debug.WriteLine("{" + conId + "} ssl " + e.Message + " - " + hostname);
					break;
				}
				catch (HTTPRequestMalformedException e)
				{
					reply.Status = Windows.Web.Http.HttpStatusCode.BadRequest;
					reply.SetReply(e.Message);
					reply.MIMEType = "text/html";
				}
				catch (HTTPException e)
				{
					reply.Status = e.Status;
					reply.SetReply(e.Message);
					reply.MIMEType = "text/html";
				}
				catch (Exception e)
				{
					persistent = false;
					Debug.WriteLine("{" + conId + "} ssl " + e.Message + " - " + hostname);
					break;
				}
				await SendHTTP(conId, reply, ssl, hostname);
			}
			Debug.WriteLine("{" + conId + "} ssl Connection done - " + hostname);
		});
	}
}
