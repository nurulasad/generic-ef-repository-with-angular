using System;
using System.Security.Principal;

namespace GenericRepository.Model
{

    public class EndUserFriendlyException : Exception
    {
        public MessageInfo Messages { get;set;}
        public EndUserFriendlyException(MessageInfo messageInfo) : base()
		{
            Messages = messageInfo;
		}


        public EndUserFriendlyException(string massage, MessageInfo messageInfo) : base(massage)
        {
            Messages = messageInfo;
        }

        public EndUserFriendlyException(string massage, Exception innerException, MessageInfo messageInfo) : base(massage, innerException)
        {
            Messages = messageInfo;
        }
        
    }
}
