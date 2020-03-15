using System;
using System.Reflection;

namespace SoapCore.Extensibility
{
	[Obsolete("Use " + nameof(IValueBinder) + " or " + nameof(IOperationFilter) + ".")]
	public interface ISoapModelBounder
	{
		void OnModelBound(MethodInfo methodInfo, object[] prms);
	}
}
