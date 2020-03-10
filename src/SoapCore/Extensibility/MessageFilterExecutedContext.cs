using System;
using System.ServiceModel.Channels;
using SoapCore.ServiceModel;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// A context after the message filter downstream in pipeline was executed. It contains result of the filter pipeline.
	/// </summary>
	public sealed class MessageFilterExecutedContext
	{
		private MessageFilterExecutedContext(Message message, ServiceDescription serviceDescription)
		{
			Message = message;
			ServiceDescription = serviceDescription ?? throw new ArgumentNullException(nameof(serviceDescription));
		}

		/// <summary>
		/// Gets or sets the response message. The value can be null, if the operation is One-Way MEP (SOAP 1.2 Part 3).
		/// One way MEP don't return any message to the client.
		/// </summary>
		public Message Message { get; set; }

		/// <summary>
		/// Gets description of the service that should process the message.
		/// </summary>
		public ServiceDescription ServiceDescription { get; }

		internal static MessageFilterExecutedContext CreateOneWay(ServiceDescription serviceDescription)
		{
			return new MessageFilterExecutedContext(null, serviceDescription);
		}

		internal static MessageFilterExecutedContext Create(Message responseMessage, ServiceDescription serviceDescription)
		{
			return new MessageFilterExecutedContext(responseMessage, serviceDescription);
		}
	}
}
