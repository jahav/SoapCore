using System;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	internal class ShortCircuitValueBinder : IValueBinder, IValueBinderProvider
	{
		private Func<int> _getCheckpoint;

		public ShortCircuitValueBinder(Func<int> getCheckpoint) => _getCheckpoint = getCheckpoint;

		public int Checkpoint { get; private set; }

		public Task BindValue(ValueBindingContext context)
		{
			Checkpoint = _getCheckpoint();
			throw new ApplicationException();
		}

		public IValueBinder GetBinder(ValueBinderProviderContext context) => this;
	}
}
