using System;
using System.Reflection;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore
{
	internal sealed class UnwrapReflectionDecorator : IOperationInvoker
	{
		private readonly IOperationInvoker _invoker;

		public UnwrapReflectionDecorator(IOperationInvoker invoker)
		{
			_invoker = invoker;
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

				throw exception;
			}
		}
	}
}
