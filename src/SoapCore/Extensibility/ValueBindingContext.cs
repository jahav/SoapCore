using Microsoft.AspNetCore.Http;
using SoapCore.ServiceModel;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// A context for <see cref="IValueBinder"/>.
	/// </summary>
	public sealed class ValueBindingContext
	{
		internal ValueBindingContext(object value, SoapMethodParameterInfo parameter, OperationDescription operation, HttpContext httpContext)
		{
			Value = value;
			Parameter = parameter;
			Operation = operation;
			HttpContext = httpContext;
		}

		/// <summary>
		/// Gets or sets a value that is going to be passed as an argument to the operation.
		/// </summary>
		public object Value { get; set; }

		/// <summary>
		/// Gets the information about the parameter the <see cref="Value"/>
		/// used as an argument.
		/// </summary>
		public SoapMethodParameterInfo Parameter { get; }

		/// <summary>
		/// Gets a description of the operation the <see cref="Value"/> is to be.
		/// </summary>
		public OperationDescription Operation { get; }

		/// <summary>
		/// Gets a http context that contains the request with a message that caused this SOAP RPC call.
		/// </summary>
		public HttpContext HttpContext { get; }
	}
}
