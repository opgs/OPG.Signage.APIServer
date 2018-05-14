using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Web.Http;

namespace OPG.Signage.APIServer
{
	public sealed class HTTPReply
	{
		public HttpVersion Version { get; set; } = HttpVersion.Http10;
		public HttpStatusCode Status { get; set; } = HttpStatusCode.Ok;
		public bool IsHead { get; set; } = false;
		public bool IsPersistent { get; set; } = false;
		private List<string> _headers = new List<string>() { Dates.OPGDate.GetHTTPHeader(), "Access-Control-Allow-Origin: *\r\n", "Cache-Control: no-cache\r\n" };
		private string _MIMEType = "application/json";
		private byte[] Content;

		public string MIMEType
		{
			get
			{
				return _MIMEType;
			}
			set
			{
				if (!String.IsNullOrEmpty(value) && value.Contains("/"))
				{
					_MIMEType = value;
				}
			}
		}

		public string Header {
			get
			{
				string outVersion = "HTTP/1.0";
				if(Version == HttpVersion.Http11)
				{
					outVersion = "HTTP/1.1";
				}
				StringBuilder buildb = new StringBuilder(outVersion + " " + (int)Status + " " + Status.ToString().ToUpper() + "\r\n");
				buildb.Append("Content-Length: " + Content.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\nContent-Type: " + MIMEType + "\r\n");
				for(int counter = 0; counter < _headers.Count; counter++)
				{
					buildb.Append(_headers[counter]);
				}
                buildb.Append(IsPersistent ? "\r\n" : "Connection: close\r\n\r\n");
                return buildb.ToString();
			}
			set
			{
				_headers.Add(value);
			}
			
		}

		public IList<byte> Response {
			get
			{
				if (IsHead)
				{
					return Encoding.UTF8.GetBytes(Header);
				}
				else
				{
					return Encoding.UTF8.GetBytes(Header).Concat(Content).ToArray();
				}
			}
		}

		public HTTPReply()
		{
		}

		public HTTPReply(HttpVersion versionIn)
		{
			Version = versionIn;
		}

		public void SetReply(string replyIn)
		{
			Content = Encoding.UTF8.GetBytes(replyIn);
		}

		public void SetReply(byte[] replyIn)
		{
			Content = replyIn;
		}
	}
}
