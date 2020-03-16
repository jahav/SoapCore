using System;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	internal class CheckpointOperationFilter : IOperationFilter
	{
		private Func<int> _getCheckpoint;

		public CheckpointOperationFilter(Func<int> getCheckpoint) => _getCheckpoint = getCheckpoint;

		public int EntryCheckpoint { get; private set; }
		public int ExitCheckpoint { get; private set; }

		public async Task OnOperationExecution(OperationExecutingContext context, OperationFilterExecutionDelegate next)
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
