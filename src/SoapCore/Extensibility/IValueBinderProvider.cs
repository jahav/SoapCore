namespace SoapCore.Extensibility
{
	/// <summary>
	/// This interface determines, if a value for an operation should be adjusted after
	/// deserialization, but before operation invocation.
	/// In the binding phase of the pipeline, it might be necessary to adjust the values
	/// of the operation argument values.
	///
	/// That might need to be done only for certain globally, only for certain services,
	/// or operation.
	/// </summary>
	public interface IValueBinderProvider
	{
		/// <summary>
		/// Get a value binder to use for argument's value of an operation.
		/// Once a binder if found, the search is over, no further providers are checked if they have another binder.
		/// </summary>
		/// <param name="context">Context with information about the value that might need to be binded.</param>
		/// <returns>An instance of the value binder or null if no value binder found.</returns>
		IValueBinder GetBinder(ValueBinderProviderContext context);
	}
}
