using System;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	internal class CheckpointValueBinder : IValueBinder, IValueBinderProvider
	{
		private Func<int> _getCheckpoint;

		public CheckpointValueBinder(Func<int> getCheckpoint)
		{
			_getCheckpoint = getCheckpoint;
		}

		public int Checkpoint { get; private set; }

		public IValueBinder GetBinder(ValueBinderProviderContext context)
		{
			return this;
		}

		public Task BindValue(ValueBindingContext context)
		{
			Checkpoint = _getCheckpoint();
			return Task.CompletedTask;
		}
	}
}
