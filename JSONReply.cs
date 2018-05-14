using Windows.Web.Http;

namespace OPG.Signage.APIServer
{
	public sealed class JSONReply
	{
        private string _Message = "";

        public HttpStatusCode Status { get; set; } = HttpStatusCode.Ok;

        public string MIMEType { get; set; } = "";

        public string Message 
		{
			get
			{
				if(_Message.StartsWith("{") || MIMEType == "text/html")
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
		}

		public JSONReply(string messageIn = "", string typeIn = "application/json", HttpStatusCode statusIn = HttpStatusCode.Ok)
		{
			Status = statusIn;
			MIMEType = typeIn;
			_Message = messageIn;
		}
	}
}
