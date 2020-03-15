using Microsoft.AspNetCore.Http;
using SoapCore.ServiceModel;
using System;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// Context for the result of an operation exeuction for the return path of the <see cref="IOperationFilter"/>.
	/// </summary>
	public sealed class OperationExecutedContext
	{
		internal OperationExecutedContext(HttpContext httpContext, object[] arguments, object serviceInstance, OperationDescription operationDescription, object result)
		{
			HttpContext = httpContext;
			OperationArguments = arguments;
			ServiceInstance = serviceInstance;
			OperationDescription = operationDescription;
			Result = result;
		}

		internal OperationExecutedContext(OperationExecutingContext ctx, object responseObject)
			: this(ctx.HttpContext, ctx.OperationArguments, ctx.ServiceInstance, ctx.OperationDescription, responseObject)
		{
		}

		/// <summary>
		/// Gets the http context for the current request.
		/// </summary>
		public HttpContext HttpContext { get; }

		/// <summary>
		/// Gets the arguments that were passed into the operation.
		/// </summary>
		public object[] OperationArguments { get; }

		/// <summary>
		/// Gets the instance of the service that was called.
		/// </summary>
		public object ServiceInstance { get; }

		/// <summary>
		/// Gets the description of the operation that was called.
		/// </summary>
		public OperationDescription OperationDescription { get; }

		/// <summary>
		/// Gets or sets the result of the operation.
		/// </summary>
		public object Result { get; set; }

		internal static OperationExecutedContext CreateOneWay(OperationExecutingContext context)
		{
			throw new NotImplementedException();
		}
	}
}
