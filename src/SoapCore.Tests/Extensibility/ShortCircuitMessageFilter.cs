using System;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	internal class ShortCircuitMessageFilter<TException> : ISoapMessageFilter
		where TException : Exception, new()
	{
		private readonly Func<int> _getCheckpoint;
		private readonly bool _throughException;

		public ShortCircuitMessageFilter(Func<int> getCheckpoint, bool throughException = false)
		{
			_getCheckpoint = getCheckpoint;
			_throughException = throughException;
		}

		public int Checkpoint { get; private set; }

		public Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
		{
			Checkpoint = _getCheckpoint();
			if (_throughException)
			{
				throw new TException();
			}

			return Task.CompletedTask;
		}
	}
}
