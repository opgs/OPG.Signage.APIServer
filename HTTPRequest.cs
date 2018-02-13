using System;
using Windows.Web.Http;

namespace OPG.Signage.APIServer
{
	public sealed class HTTPRequest
	{
		public string Raw { get; }
		public HttpMethod Method { get; }
		public string URL { get; }
		public string URLPath { get; }
		public HttpVersion Version { get; } = HttpVersion.None;
		public bool Persistent { get; } = true;

		public HTTPRequest(string requestraw)
		{
			if(String.IsNullOrEmpty(requestraw))
			{
				return;
			}
			Raw = requestraw.ToLower();
			string[] firstline = requestraw.Split('\n')[0].Split(' ');
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
				if (Version == HttpVersion.Http11 && !Raw.Contains("host:"))
				{
					throw new HTTPRequestMalformedException("No host header was found on http/1.1 request : " + Raw);
				}
				if (Version == HttpVersion.Http20 || Version == HttpVersion.None)
				{
					throw new HTTPException(HttpStatusCode.HttpVersionNotSupported, "HTTP Version not supported");
				}
				if (Raw.Contains("connection: close") || Version == HttpVersion.Http10)
				{
					Persistent = false;
				}
			}
			else
			{
				throw new HTTPRequestMalformedException("HTTP Request was malformed : " + Raw);
			}
		}
	}
}
