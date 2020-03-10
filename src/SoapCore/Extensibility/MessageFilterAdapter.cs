using System.Threading.Tasks;

// Use of obsolete symbol - This is an adapter to keep obsolete implementations working
#pragma warning disable 618

namespace SoapCore.Extensibility
{
	public class MessageFilterAdapter : ISoapMessageFilter
	{
		private readonly IMessageFilter _messageFilter;

		public MessageFilterAdapter(IMessageFilter messageFilter)
		{
			_messageFilter = messageFilter;
		}

		public async Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
		{
			_messageFilter.OnRequestExecuting(requestContext.Message);
			var responseContext = await next();
			_messageFilter.OnResponseExecuting(responseContext.Message);
		}
	}
}
