using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Windows.Web.Http;

namespace OPG.Signage.APIServer
{
	public sealed class HTTPRequest
	{
        public string Raw { get; } = "";
        public HttpMethod Method { get; } = HttpMethod.Get;
        public string URL { get; } = "";
        public string URLPath { get; } = "";
		public HttpVersion Version { get; } = HttpVersion.None;
        public string[] Headers { get; }
		public ConcurrentDictionary<string, string> HeaderPairs { get; } = new ConcurrentDictionary<string, string>();
        public ConcurrentDictionary<string, string> KVPairs { get; } = new ConcurrentDictionary<string, string>();
        public string Content { get; } = "";
		public bool Persistent { get; } = true;

		public HTTPRequest(string requestraw)
		{
			if(String.IsNullOrEmpty(requestraw))
			{
				return;
			}
			Raw = requestraw.ToLower();
			Headers = Raw.Split("\n");
			string[] firstline = requestraw.Split("\n")[0].Split(' ');
			int contentLength = 0;
			if (firstline.Length == 3)
			{
				Method = new HttpMethod(firstline[0]);
				URL = firstline[1];
				URLPath = URL.Split('?')[0];
				if (firstline[2].Contains("1.1"))
				{
					Version = HttpVersion.Http11;
				}
				else if (firstline[2].Contains("1.0"))
				{
					Version = HttpVersion.Http10;
				}
				for(int h = 1; h < Headers.Length; h++)
				{
					Headers[h] = Regex.Replace(Headers[h], @"\t|\n|\r|\0", "");
					if(!Headers[h].Contains(":"))
					{
						continue;
					}
					string pair0 = Headers[h].Substring(0, Headers[h].IndexOf(":")).Trim();
					string pair1 = Headers[h].Substring(Headers[h].IndexOf(":") + 1).Trim();
					HeaderPairs.TryAdd(pair0, pair1);
				}
				if (HeaderPairs.TryGetValue("host", out string hostValue) == false)
                {
                    if (Version == HttpVersion.Http11)
					{
						throw new HTTPException(HttpStatusCode.BadRequest, "No host header was found on http/1.1 request : " + Raw);
					}
				}
				if (HeaderPairs.TryGetValue("connection", out string connectionValue))
				{
					if (connectionValue == "close")
					{
						Persistent = false;
					}
				}
				if (HeaderPairs.TryGetValue("content-length", out string contentLengthValue))
				{
					contentLength = int.Parse(contentLengthValue);
				}
				if (HeaderPairs.TryGetValue("content-type", out string contentTypeValue))
				{
					Content = requestraw.Substring(requestraw.Length - contentLength);

					if (contentTypeValue.Contains("application/x-www-form-urlencoded"))
					{
						string[] keys = Content.Split("&");
						for (int k = 0; k < keys.Length; k++)
						{
							string[] pair = keys[k].Split("=");
							if (pair.Length > 1)
							{
								KVPairs.TryAdd(pair[0], System.Web.HttpUtility.UrlDecode(pair[1]));
							}
						}
					}
				}
				if (Version == HttpVersion.Http20 || Version == HttpVersion.None)
				{
					throw new HTTPException(HttpStatusCode.HttpVersionNotSupported, "HTTP Version not supported");
				}
				if (Version == HttpVersion.Http10)
				{
					Persistent = false;
				}
			}
			else
			{
				throw new HTTPException(HttpStatusCode.BadRequest, "HTTP Request was malformed : " + Raw);
			}
		}
	}
}
