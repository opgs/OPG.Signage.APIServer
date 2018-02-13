using System;

namespace OPG.Signage.APIServer
{
    public class NothingReceivedException : Exception
    {
        public new string Message = "Nothing Received";

        public NothingReceivedException() : base()
        {
        }

        public NothingReceivedException(string messageIn) : base(messageIn)
        {
            Message = messageIn;
        }

        public NothingReceivedException(string messageIn, Exception innerExceptionIn) : base(messageIn, innerExceptionIn)
        {
            Message = messageIn;
        }
    }
}
