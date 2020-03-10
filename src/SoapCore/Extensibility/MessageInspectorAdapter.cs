using System.Threading.Tasks;

// Use of obsolete symbol - This is an adapter to keep obsolete implementations working
#pragma warning disable 618

namespace SoapCore.Extensibility
{
	internal class MessageInspectorAdapter : ISoapMessageFilter
	{
		private readonly IMessageInspector _messageInspector;

		public MessageInspectorAdapter(IMessageInspector messageInspector)
		{
			_messageInspector = messageInspector;
		}

		public async Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
		{
			var requestMessage = requestContext.Message;
			var correlationState = _messageInspector.AfterReceiveRequest(ref requestMessage);
			requestContext.Message = requestMessage;

			var responseContext = await next();

			var responseMessage = responseContext.Message;
			_messageInspector.BeforeSendReply(ref responseMessage, correlationState);
			responseContext.Message = responseMessage;
		}
	}
}
