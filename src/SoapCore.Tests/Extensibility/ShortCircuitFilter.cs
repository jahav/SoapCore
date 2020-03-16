using System;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	internal class ShortCircuitFilter : ISoapMessageFilter, IOperationFilter
	{
		private readonly Func<int> _getCheckpoint;
		private readonly bool _throughException;

		public ShortCircuitFilter(Func<int> getCheckpoint, bool throughException = false)
		{
			_getCheckpoint = getCheckpoint;
			_throughException = throughException;
		}

		public int Checkpoint { get; private set; }

		public Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
		{
			return RunFilter();
		}

		public Task OnOperationExecution(OperationExecutingContext context, OperationFilterExecutionDelegate next)
		{
			return RunFilter();
		}

		private Task RunFilter()
		{
			Checkpoint = _getCheckpoint();
			if (_throughException)
			{
				throw new ApplicationException();
			}

			return Task.CompletedTask;
		}
	}
}
