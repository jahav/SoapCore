using System;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	internal class CheckpointFilter : ISoapMessageFilter, IOperationFilter
	{
		private readonly Func<int> _getCheckpoint;

		public CheckpointFilter(Func<int> getCheckpoint)
		{
			_getCheckpoint = getCheckpoint;
		}

		public int EntryCheckpoint { get; private set; }

		public int ExitCheckpoint { get; private set; }

		public async Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
		{
			await RunFilter(async () => await next());
		}

		public async Task OnOperationExecution(OperationExecutingContext context, OperationFilterExecutionDelegate next)
		{
			await RunFilter(async () => await next());
		}

		private async Task RunFilter<T>(Func<Task<T>> next)
		{
			EntryCheckpoint = _getCheckpoint();
			try
			{
				await next();
			}
			finally
			{
				ExitCheckpoint = _getCheckpoint();
			}
		}
	}
}
