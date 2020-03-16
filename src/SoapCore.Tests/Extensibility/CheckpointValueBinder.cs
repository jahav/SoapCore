using System;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	internal class CheckpointValueBinder : IValueBinder, IValueBinderProvider
	{
		private readonly Func<int> _getCheckpoint;
		private readonly bool _throwException;

		private CheckpointValueBinder(Func<int> getCheckpoint, bool throwException)
		{
			_getCheckpoint = getCheckpoint;
			_throwException = throwException;
		}

		public int Checkpoint { get; private set; }

		public static CheckpointValueBinder CreatePassThrough(Func<int> getCheckpoint)
		{
			return new CheckpointValueBinder(getCheckpoint, false);
		}

		public static CheckpointValueBinder CreateThrowingException(Func<int> getCheckpoint)
		{
			return new CheckpointValueBinder(getCheckpoint, true);
		}

		public IValueBinder GetBinder(ValueBinderProviderContext context)
		{
			return this;
		}

		public Task BindValue(ValueBindingContext context)
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
