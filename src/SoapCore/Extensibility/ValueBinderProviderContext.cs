using SoapCore.ServiceModel;
using System;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// Context passed to the <see cref="IValueBinderProvider"/>. It contains information
	/// necessary for determining if/which <see cref="IValueBinder"/> should be used for
	/// an argument value to be used as an argument of operation.
	/// </summary>
	public sealed class ValueBinderProviderContext
	{
		internal ValueBinderProviderContext(OperationDescription operation, SoapMethodParameterInfo parameterInfo, Type valueType)
		{
			Operation = operation;
			ParameterInfo = parameterInfo;
			ValueType = valueType;
		}

		/// <summary>
		/// Gets type of deserialized argument value. If nothing, then null value.
		/// </summary>
		public Type ValueType { get; }

		/// <summary>
		/// Gets info about the paramter that might need to be binded.
		/// </summary>
		public SoapMethodParameterInfo ParameterInfo { get; }

		/// <summary>
		/// Gets the operation that is going to be executed.
		/// </summary>
		public OperationDescription Operation { get; }
	}
}
