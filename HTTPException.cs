using System;
using Windows.Web.Http;

namespace OPG.Signage.APIServer
{
	public class HTTPException : Exception
	{
		public override string Message { get; } = "A HTTP error occured";
		public HttpStatusCode Status { get; } = HttpStatusCode.Ok;

		public HTTPException() : base()
		{
		}

		public HTTPException(string messageIn) : base(messageIn)
		{
			Message = messageIn;
		}

		public HTTPException(string messageIn, Exception innerException) : base(messageIn, innerException)
		{
			Message = messageIn;
		}

		public HTTPException(HttpStatusCode statusIn, string messageIn) : base(messageIn)
		{
			Status = statusIn;
			Message = messageIn;
		}

		public HTTPException(HttpStatusCode statusIn, string messageIn, Exception innerExceptionIn) : base(messageIn, innerExceptionIn)
		{
			Status = statusIn;
			Message = messageIn;
		}
	}
}
