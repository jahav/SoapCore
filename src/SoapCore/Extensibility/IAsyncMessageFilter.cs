using System;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace SoapCore.Extensibility
{
	[Obsolete(@"Interface has been replaced with " + nameof(ISoapMessageFilter) + @".")]
	public interface IAsyncMessageFilter
	{
		Task OnRequestExecuting(Message message);
		Task OnResponseExecuting(Message message);
	}
}
