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
        protected string IP;
        protected string Hostname;
        protected ConcurrentDictionary<string, Func<HTTPRequest, JSONReply>> GetPaths = new ConcurrentDictionary<string, Func<HTTPRequest, JSONReply>>();
        protected ConcurrentDictionary<string, Func<HTTPRequest, JSONReply>> PostPaths = new ConcurrentDictionary<string, Func<HTTPRequest, JSONReply>>();
        private StreamSocketListener Listener = new StreamSocketListener();

        public int Port { get; private set; } = 0;
        public Uri RegUrl { get; set; } = new Uri("http://localhost");
        public string ReplyFormat { get; set; } = "{0}";

        public JSONServer(int portIn, string ipIn, string hostname)
        {
            Port = portIn;
            IP = ipIn;
            Hostname = hostname;
            Listener.ConnectionReceived += (_, e) => ProcessRequest(e.Socket);
        }

        public async void RegisterDeviceAsync(string ipIn, int portIn, string hostname)
        {
            if (!RegUrl.Equals(new Uri("http://localhost")))
            {
                Debug.WriteLine("Registering with monitor");
                using (HttpClient curl = new HttpClient())
                {
                    try
                    {
                        Debug.WriteLine(RegUrl + "?ip=" + ipIn + "&port=" + portIn.ToString() + "&host=" + hostname);
                        HttpResponseMessage reply = await curl.GetAsync(new Uri(RegUrl + "?ip=" + IP + "&port=" + portIn.ToString() + "&host=" + hostname));
                        Debug.WriteLine(reply.Content);
                    }
                    catch (Exception e)
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
                await Listener.BindServiceNameAsync(Port.ToString());
            }
            catch (Exception)
            {
                Port++;
                await StartAsync().ConfigureAwait(false);
            }
            RegisterDeviceAsync(IP, Port, Hostname);
        }

        public void Get(string pathIn, Func<HTTPRequest, JSONReply> toDo)
        {
            GetPaths.TryAdd(pathIn, toDo);
        }

        public void Post(string pathIn, Func<HTTPRequest, JSONReply> toDo)
        {
            PostPaths.TryAdd(pathIn, toDo);
        }

        protected const ushort BufferSize = 4096;

        protected async Task<HTTPRequest> ReadHTTP(int thisId, IInputStream input, string hostname)
        {
            StringBuilder requestraw = new StringBuilder();
            byte[] data = new byte[BufferSize];
            IBuffer buffer = data.AsBuffer();
            int dataRead = BufferSize;

            while (dataRead == BufferSize)
            {
                await input.ReadAsync(buffer, BufferSize, InputStreamOptions.Partial);
                if (buffer.Length == 0)
                {
                    throw new NothingReceivedException();
                }
                requestraw.Append(Encoding.UTF8.GetString(buffer.ToArray(), 0, buffer.ToArray().Length));
                dataRead = buffer.ToArray().Length;
            }

            return new HTTPRequest(requestraw.ToString().Trim());
        }

        protected async Task SendHTTP(int conId, HTTPReply reply, Stream response, string hostname)
        {
            byte[] content = (byte[])reply.Response;
            await response.WriteAsync(content, 0, content.Length).ConfigureAwait(false);
            await response.FlushAsync().ConfigureAwait(false);

            Debug.WriteLine("{" + conId + "} Response " + Encoding.UTF8.GetString((byte[])reply.Response).Split("\n")[0] + " - " + hostname);
        }

        protected int id = 0;

        protected void HandleMethod(ref HTTPRequest request, ref HTTPReply reply, ref JSONReply GetEntryReply, ref JSONReply PostEntryReply)
        {
            switch (request.Method.Method.ToUpper())
            {
                case "HEAD":
                    if (PostEntryReply != null)
                    {
                        goto case "POST";
                    }
                    if (GetEntryReply != null)
                    {
                        goto case "GET";
                    }
                    break;
                case "POST":
                    if (PostEntryReply == null)
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
                    if (GetEntryReply == null)
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

        protected string getFinalPath(string urlIn)
        {
            return (urlIn.EndsWith("/")) ? urlIn.Substring(0, urlIn.Length - 1) : urlIn;
        }

        private void ProcessRequest(StreamSocket socket) => Task.Run(async () =>
        {
            int conId = id++;
            string rhostname = socket.Information.RemoteHostName.ToString();
            Debug.WriteLine("{" + conId + "} New connection - " + rhostname);

            using (Stream response = socket.OutputStream.AsStreamForWrite())
            using (IInputStream input = socket.InputStream)
            {
                await _doRequest(conId, rhostname, input, response);
            }
            Debug.WriteLine("{" + conId + "} Connection done - " + rhostname);
        });

        protected async Task _doRequest(int conId, string hostname, IInputStream input, Stream response)
        {
            bool persistent = true;
            byte persistentCount = 99;

            while (persistent)
            {
                HTTPReply reply = new HTTPReply(HttpVersion.Http11);
                try
                {
                    HTTPRequest request = await ReadHTTP(conId, input, hostname);

                    Debug.WriteLine("{" + conId + "} Read : " + request.Method + " " + request.URLPath + " " + request.Version + " - " + hostname);

                    if (!request.Persistent || persistentCount-- < 1)
                    {
                        persistent = false;
                    }

                    reply = new HTTPReply(request.Version)
                    {
                        IsHead = (request.Method == HttpMethod.Head),
                        IsPersistent = persistent
                    };

                    string finalPath = getFinalPath(request.URLPath);

                    bool GetExists = GetPaths.TryGetValue(finalPath, out Func<HTTPRequest, JSONReply> GetEntry);
                    bool PostExists = PostPaths.TryGetValue(finalPath, out Func<HTTPRequest, JSONReply> PostEntry);
                    JSONReply GetEntryReply = GetExists ? GetEntry(request) : null;
                    JSONReply PostEntryReply = PostExists ? PostEntry(request) : null;

                    HandleMethod(ref request, ref reply, ref GetEntryReply, ref PostEntryReply);
                }
                catch (NothingReceivedException e)
                {
                    Debug.WriteLine("{" + conId + "} " + e.Message + " - " + hostname);
                    break;
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
                    Debug.WriteLine("{" + conId + "} " + e.Message + " - " + hostname);
                    break;
                }
                finally
                {
                    try
                    {
                        await SendHTTP(conId, reply, response, hostname);
                    }
                    catch (Exception)
                    {
                        persistent = false;
                    }
                }
            }
        }

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
