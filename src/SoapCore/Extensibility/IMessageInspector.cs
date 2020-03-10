using System;
using System.ServiceModel.Channels;

namespace SoapCore.Extensibility
{
	[Obsolete("Interface has been replaced with " + nameof(ISoapMessageFilter) + @".")]
	public interface IMessageInspector
	{
		object AfterReceiveRequest(ref Message message);
		void BeforeSendReply(ref Message reply, object correlationState);
	}
}
