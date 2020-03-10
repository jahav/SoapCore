using System.Threading.Tasks;

// Use of obsolete symbol - This is an adapter to keep obsolete implementations working
#pragma warning disable 618

namespace SoapCore.Extensibility
{
	internal class AsyncMessageFilterAdapter : ISoapMessageFilter
	{
		private readonly IAsyncMessageFilter _messageFilter;

		public AsyncMessageFilterAdapter(IAsyncMessageFilter messageFilter)
		{
			_messageFilter = messageFilter;
		}

		public async Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
		{
			await _messageFilter.OnRequestExecuting(requestContext.Message);
			var responseContext = await next();
			await _messageFilter.OnRequestExecuting(responseContext.Message);
		}
	}
}
