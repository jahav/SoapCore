using System;
using System.ServiceModel.Channels;
using SoapCore.ServiceModel;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// Context for the <see cref="ISoapMessageFilter"/>. It contains data about the received SOAP message and
	/// other items alteration of the message or necessary for changing the response.
	/// </summary>
	public sealed class MessageFilterExecutingContext
	{
		private Message _message;
		private Message _result;

		internal MessageFilterExecutingContext(Message message, ServiceDescription serviceDescription)
		{
			Message = message;
			ServiceDescription = serviceDescription;
		}

		/// <summary>
		/// Gets or sets the processed message. Once you change the message, filters downstream
		/// will receive the changed message.
		/// </summary>
		/// <exception cref="ArgumentNullException">You can't set property to null.</exception>
		public Message Message
		{
			get => _message;
			set => _message = value ?? throw new ArgumentNullException();
		}

		/// <summary>
		/// Gets description of the service that should process the message.
		/// </summary>
		public ServiceDescription ServiceDescription { get; }

		/// <summary>
		/// Gets or sets the result of the message filter pipeline.
		///
		/// If any filter sets the result, the pipeline will be short circuited
		/// and no filter further in the pipeline will be called (even if <code>next()</code>
		/// is called). The pipeline will ge through filters upwards to return the result to the client.
		///
		/// Once set, it is no longer possible to set it back to <code>null</code>.
		/// </summary>
		public Message Result
		{
			get => _result;
			set
			{
				if (_result != null)
				{
					throw new InvalidOperationException("It is not possible to unset result. That would reverse the direction of the pipeline.");
				}

				_result = value;
			}
		}
	}
}
