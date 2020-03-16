using System;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	internal class ShortCircuitOperationFilter : IOperationFilter
	{
		private Func<int> _getCheckpoint;

		public ShortCircuitOperationFilter(Func<int> getCheckpoint) => _getCheckpoint = getCheckpoint;

		public int Checkpoint { get; private set; }

		public Task OnOperationExecution(OperationExecutingContext context, OperationFilterExecutionDelegate next)
		{
			Checkpoint = _getCheckpoint();
			throw new ApplicationException();
		}
	}
}
