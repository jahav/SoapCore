using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SoapCore.Extensibility;

namespace SoapCore
{
	internal sealed class UnwrapReflectionDecorator : IOperationInvoker
	{
		private readonly IOperationInvoker _invoker;
		private readonly ILogger _logger;

		public UnwrapReflectionDecorator(IOperationInvoker invoker, ILogger logger)
		{
			_invoker = invoker;
			_logger = logger;
		}

		public async Task<object> InvokeAsync(MethodInfo methodInfo, object instance, object[] inputs)
		{
			try
			{
				return await _invoker.InvokeAsync(methodInfo, instance, inputs);
			}
			catch (Exception exception)
			{
				if (exception is TargetInvocationException targetInvocationException)
				{
					exception = targetInvocationException.InnerException;
				}

				_logger.LogWarning(0, exception, exception?.Message);

				throw exception;
			}
		}
	}
}
