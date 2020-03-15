using Microsoft.AspNetCore.Http;
using SoapCore.ServiceModel;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// A contect for the <see cref="IOperationFilter"/>. It represents a context of the input into an operation execution.
	/// </summary>
	public sealed class OperationExecutingContext
	{
		// Do not ever put Message into the context, that is what ISoapMessageFilter is for.
		internal OperationExecutingContext(HttpContext httpContext, object[] arguments, object serviceInstance, OperationDescription operationDescription)
		{
			HttpContext = httpContext;
			OperationArguments = arguments;
			ServiceInstance = serviceInstance;
			OperationDescription = operationDescription;
		}

		/// <summary>
		/// Gets the http context for the current request.
		/// </summary>
		public HttpContext HttpContext { get; }

		/// <summary>
		/// Gets the arguments (both in and out) that are to be passed into the operation.
		/// </summary>
		public object[] OperationArguments { get; }

		/// <summary>
		/// Gets the instance of the service that is will be called.
		/// </summary>
		public object ServiceInstance { get; }

		/// <summary>
		/// Gets the description of the operation that will be called.
		/// </summary>
		public OperationDescription OperationDescription { get; }

		/// <summary>
		/// Gets or sets the result of the operation execution. When a filter short circuts, the value is <code>null</code>.
		/// </summary>
		public object Result { get; set; }
	}
}
