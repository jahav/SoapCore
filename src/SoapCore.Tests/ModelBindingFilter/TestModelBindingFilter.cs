using System;
using System.Collections.Generic;
using SoapCore.Extensibility;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.ModelBindingFilter
{
#pragma warning disable CS0618 // Type or member is obsolete Testing obsolete interface
	public class TestModelBindingFilter : IModelBindingFilter
#pragma warning restore CS0618 // Type or member is obsolete
	{
		public TestModelBindingFilter(List<Type> modelTypes)
		{
			ModelTypes = modelTypes;
		}

		public List<Type> ModelTypes { get; set; }

		public void OnModelBound(object model, IServiceProvider serviceProvider, out object result)
		{
			var complexModel = (ComplexModelInputForModelBindingFilter)model;
			complexModel.StringProperty += "MODIFIED BY TestModelBindingFilter";
			complexModel.IntProperty = complexModel.IntProperty * 2;
			result = true;
		}
	}
}
