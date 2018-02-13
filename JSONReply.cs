using Windows.Web.Http;

namespace OPG.Signage.APIServer
{
	public sealed class JSONReply
	{
		private HttpStatusCode _Status;
		private string _MIMEType;
		private string _Message;

		public HttpStatusCode Status 
		{
			get
			{
				return _Status;
			}
			set
			{
				_Status = value;
			}
		}

		public string MIMEType 
		{
			get
			{
				return _MIMEType;
			}
			set
			{
				_MIMEType = value;
			}
		}

		public string Message 
		{
			get
			{
				if(_Message.StartsWith("{") || _MIMEType == "text/html")
				{ 
					return _Message;
				}
				return "\"" + _Message + "\"";
			}
			set
			{
				_Message = value;
			}
		}

		public JSONReply()
		{
			_MIMEType = "";
			_Message = "";
		}

		public JSONReply(string messageIn = "", string typeIn = "application/json", HttpStatusCode statusIn = HttpStatusCode.Ok)
		{
			_Status = statusIn;
			_MIMEType = typeIn;
			_Message = messageIn;
		}
	}
}
