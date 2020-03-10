using System;
using System.ServiceModel.Channels;

namespace SoapCore.Extensibility
{
	[Obsolete(@"Interface has been replaced with " + nameof(ISoapMessageFilter) + @".")]
	public interface IMessageFilter
	{
		void OnRequestExecuting(Message message);
		void OnResponseExecuting(Message message);
	}
}
