using System;
using Windows.Web.Http;

namespace OPG.Signage.APIServer
{
    internal class HTTPRequestMalformedException : HTTPException
    {
        public HTTPRequestMalformedException() : base()
        {
        }

        public HTTPRequestMalformedException(string messageIn) : base(HttpStatusCode.BadRequest ,messageIn)
        {
            
        }

        public HTTPRequestMalformedException(string messageIn, Exception innerExceptionIn) : base(HttpStatusCode.BadRequest, messageIn, innerExceptionIn)
        {
            
        }
    }
}
