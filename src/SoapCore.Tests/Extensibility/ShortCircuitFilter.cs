using System;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	internal class ShortCircuitFilter : ISoapMessageFilter, IOperationFilter
	{
		private readonly Func<int> _getCheckpoint;
		private readonly bool _throwException;
		private readonly bool _setResult;
		private readonly bool _callNext;

		private ShortCircuitFilter(Func<int> getCheckpoint, bool throwException, bool setResult, bool callNext)
		{
			_getCheckpoint = getCheckpoint;
			_throwException = throwException;
			_setResult = setResult;
			_callNext = callNext;
		}

		public int Checkpoint { get; private set; }

		public static ShortCircuitFilter CreateWithResult(Func<int> getCheckpoint, bool callNext) => new ShortCircuitFilter(getCheckpoint, false, true, callNext);

		public static ShortCircuitFilter CreateWithNoResultNoNext(Func<int> getCheckpoint) => new ShortCircuitFilter(getCheckpoint, false, false, false);

		public static ShortCircuitFilter CreateException(Func<int> getCheckpoint) => new ShortCircuitFilter(getCheckpoint, true, false, false);

		public Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
		{
			if (_setResult)
			{
				requestContext.Result = Message.CreateMessage(
						MessageVersion.Default,
						requestContext.Message.Headers.Action,
						"Response body");
			}

			return RunFilter();
		}

		public Task OnOperationExecution(OperationExecutingContext context, OperationFilterExecutionDelegate next)
		{
			if (_setResult)
			{
				context.Result = "Done";
			}

			return RunFilter();
		}

		private Task RunFilter()
		{
			Checkpoint = _getCheckpoint();
			if (_throwException)
			{
				throw new ApplicationException();
			}

			return Task.CompletedTask;
		}
	}
}
