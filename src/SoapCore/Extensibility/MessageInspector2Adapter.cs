using System.Threading.Tasks;

// Use of obsolete symbol - This is an adapter to keep obsolete implementations working
#pragma warning disable 618

namespace SoapCore.Extensibility
{
	internal class MessageInspector2Adapter : ISoapMessageFilter
	{
		private readonly IMessageInspector2 _messageInspector;

		public MessageInspector2Adapter(IMessageInspector2 messageInspector)
		{
			_messageInspector = messageInspector;
		}

		public async Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
		{
			var requestMessage = requestContext.Message;
			var correlationState = _messageInspector.AfterReceiveRequest(ref requestMessage, requestContext.ServiceDescription);
			requestContext.Message = requestMessage;

			var responseContext = await next();

			var responseMessage = responseContext.Message;
			_messageInspector.BeforeSendReply(ref responseMessage, responseContext.ServiceDescription, correlationState);
			responseContext.Message = responseMessage;
		}
	}
}
