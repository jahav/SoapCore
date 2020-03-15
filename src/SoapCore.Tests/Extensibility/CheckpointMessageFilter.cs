using System;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	internal class CheckpointMessageFilter : ISoapMessageFilter
	{
		private readonly Func<int> _getCheckpoint;

		public CheckpointMessageFilter(Func<int> getCheckpoint)
		{
			_getCheckpoint = getCheckpoint;
		}

		public int EntryCheckpoint { get; private set; }

		public int ExitCheckpoint { get; private set; }

		public async Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
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
