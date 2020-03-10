using System;
using System.ServiceModel.Channels;
using SoapCore.ServiceModel;

namespace SoapCore.Extensibility
{
	[Obsolete("Interface has been replaced with " + nameof(ISoapMessageFilter) + @".")]
	public interface IMessageInspector2
	{
		object AfterReceiveRequest(ref Message message, ServiceDescription serviceDescription);
		void BeforeSendReply(ref Message reply, ServiceDescription serviceDescription, object correlationState);
	}
}
