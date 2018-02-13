using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace OPG.Signage.APIServer
{
	public class JSONServer : IDisposable
	{
		private string IP;
		private int _Port = 0;
		protected ConcurrentDictionary<string, Func<string, JSONReply>> GetPaths = new ConcurrentDictionary<string, Func<string,JSONReply>>();
		protected ConcurrentDictionary<string, Func<string, JSONReply>> PostPaths = new ConcurrentDictionary<string, Func<string, JSONReply>>();
		private StreamSocketListener Listener = new StreamSocketListener();
		private Uri _RegUrl = new Uri("http://localhost");
		private string _ReplyFormat = "{0}";

		public int Port 
		{
			get
			{
				return _Port;
			}
		}

		public Uri RegUrl 
		{
			get
			{
				return _RegUrl;
			}
			set
			{
				_RegUrl = value;
			}
		}

		public string ReplyFormat 
		{
			get
			{
				return _ReplyFormat;
			}
			set
			{
				_ReplyFormat = value;
			}
		}

		public JSONServer(int portIn, string ipIn)
		{
			_Port = portIn;
			IP = ipIn;
			Listener.ConnectionReceived += (_, e) => ProcessRequest(e.Socket);
		}

		public async void RegisterDeviceAsync(string ipIn, int portIn)
		{
			if (!RegUrl.Equals(new Uri("http://localhost")))
			{
				Debug.WriteLine("Registering with monitor");
				using (HttpClient curl = new HttpClient())
				{
					try
					{
						Debug.WriteLine(RegUrl + "?ip=" + ipIn + "&port=" + portIn.ToString() + "&host=" + Info.Network.HostName);
						HttpResponseMessage reply = await curl.GetAsync(new Uri(RegUrl + "?ip=" + IP + "&port=" + portIn.ToString() + "&host=" + Info.Network.HostName));
						Debug.WriteLine(reply.Content);
					}
					catch(Exception e)
					{
						Debug.WriteLine(e.Message);
					}
				}
			}
		}

		public async Task StartAsync()
		{
			try
			{
				await Listener.BindServiceNameAsync(_Port.ToString());
			}
			catch (Exception)
			{
				_Port++;
				await StartAsync().ConfigureAwait(false);
			}
			RegisterDeviceAsync(IP, Port);
		}

		public void Get(string pathIn, Func<string, JSONReply> toDo)
		{
			GetPaths.TryAdd(pathIn, toDo);
		}

		public void Post(string pathIn, Func<string, JSONReply> toDo)
		{
			PostPaths.TryAdd(pathIn, toDo);
		}

		protected const ushort BufferSize = 4096;

		protected async Task<HTTPRequest> ReadHTTP(int thisId, IInputStream input, string hostname)
		{
			StringBuilder requestraw = new StringBuilder();
			byte[] data = new byte[BufferSize];
			IBuffer buffer = data.AsBuffer();
			uint dataRead = BufferSize;

			while (dataRead == BufferSize)
			{
				IBuffer bytesRead = await input.ReadAsync(buffer, BufferSize, InputStreamOptions.Partial);
				if (bytesRead.Length == 0)
				{
					throw new NothingReceivedException();
				}
				requestraw.Append(Encoding.UTF8.GetString(data, 0, data.Length));
				dataRead = bytesRead.Length;
				Debug.WriteLine("{" + thisId + "} Reading " + dataRead.ToString() + " from - " + hostname);
			}

			return new HTTPRequest(requestraw.ToString());
		}

		protected async Task SendHTTP(int conId, HTTPReply reply, Stream response, string hostname)
		{
			byte[] content = (byte[])reply.Response;
			await response.WriteAsync(content, 0, content.Length).ConfigureAwait(false);
			await response.FlushAsync().ConfigureAwait(false);

			Debug.WriteLine("{" + conId + "} Response " + Encoding.UTF8.GetString((byte[])reply.Response) + " - " + hostname);
		}

		protected int id = 0;

		protected void HandleMethod(bool GetExists, bool PostExists, ref HTTPRequest request, ref HTTPReply reply, ref JSONReply GetEntryReply, ref JSONReply PostEntryReply)
		{
			switch (request.Method.Method)
			{
				case "HEAD":
					if (PostExists)
					{
						goto case "POST";
					}
					if (GetExists)
					{
						goto case "GET";
					}
					break;
				case "POST":
					if (!PostExists)
					{
						throw new HTTPException(HttpStatusCode.NotFound, request.URLPath + "<br />Not Found<br />Incorrect URL");
					}
					else if (PostEntryReply.MIMEType == "application/json")
					{
						reply.Status = PostEntryReply.Status;
						reply.SetReply(string.Format(ReplyFormat, PostEntryReply.Message));
					}
					else
					{
						reply.Status = PostEntryReply.Status;
						reply.SetReply(PostEntryReply.Message);
						reply.MIMEType = PostEntryReply.MIMEType;
					}
					break;
				case "GET":
					if (!GetExists)
					{
						throw new HTTPException(HttpStatusCode.NotFound, request.URLPath + "<br />Not Found<br />Incorrect URL");
					}
					else if (GetEntryReply.MIMEType == "application/json")
					{
						reply.Status = GetEntryReply.Status;
						reply.SetReply(string.Format(ReplyFormat, GetEntryReply.Message));
					}
					else
					{
						reply.Status = GetEntryReply.Status;
						reply.SetReply(GetEntryReply.Message);
						reply.MIMEType = GetEntryReply.MIMEType;
					}
					break;
				case "OPTIONS":
					reply.Status = HttpStatusCode.Ok;
					reply.SetReply("");
					reply.Header += "Allow: HEAD,GET,OPTIONS";
					reply.Header += "Access-Control-Allow-Methods: HEAD,GET,OPTIONS";
					break;
				default:
					throw new HTTPException(HttpStatusCode.NotImplemented, "Method not implemented");
			}
		}

		private void ProcessRequest(StreamSocket socket) => Task.Run(async () =>
		{
			int conId = id++;
			Debug.WriteLine("{" + conId + "} New connection - " + socket.Information.RemoteHostName);

			using (Stream response = socket.OutputStream.AsStreamForWrite())
			using (IInputStream input = socket.InputStream)
			{
				bool persistent = true;
				byte persistentCount = 99;

				while (persistent)
				{
					HTTPReply reply = new HTTPReply(HttpVersion.Http11);
					try
					{
						HTTPRequest request = await ReadHTTP(conId, input, socket.Information.RemoteHostName.ToString());

						Debug.WriteLine("{" + conId + "} Read " + request.Method + " " + request.URLPath + " " + request.Version + " - " + socket.Information.RemoteHostName);

						if (!request.Persistent || persistentCount-- < 1)
						{
							persistent = false;
						}

						reply = new HTTPReply(request.Version)
						{
							IsHead = (request.Method == HttpMethod.Head),
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
						Debug.WriteLine("{" + conId + "} " + e.Message + " - " + socket.Information.RemoteHostName);
						break;
					}
					catch (HTTPRequestMalformedException e)
					{
						reply.Status = HttpStatusCode.BadRequest;
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
						Debug.WriteLine("{" + conId + "} " + e.Message + " - " + socket.Information.RemoteHostName);
						break;
					}

					await SendHTTP(conId, reply, response, socket.Information.RemoteHostName.ToString());
				}
			}
			Debug.WriteLine("{" + conId + "} Connection done - " + socket.Information.RemoteHostName);
		});

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool doDispose)
		{
			if (!disposedValue)
			{
				if (doDispose)
				{
					Listener.Dispose();
				}

				GetPaths = null;
				disposedValue = true;
			}
		}

		void IDisposable.Dispose()
		{
			Dispose(true);
		}
		#endregion
	}
}
