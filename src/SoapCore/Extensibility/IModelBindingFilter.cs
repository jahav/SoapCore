using System;
using System.Collections.Generic;

namespace SoapCore.Extensibility
{
	[Obsolete("Interface has been replaced with " + nameof(IValueBinderProvider) + " and " + nameof(IValueBinder) + ".")]
	public interface IModelBindingFilter
	{
		List<Type> ModelTypes { get; set; }
		void OnModelBound(object model, IServiceProvider serviceProvider, out object output);
	}
}
