using System.Threading.Tasks;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// A binder that adjusts the value to be passed as an argument to an operation.
	/// </summary>
	public interface IValueBinder
	{
		/// <summary>
		/// Modify the <see cref="ValueBindingContext.Value"/> in the context that is going to be passed
		/// as an argument into an operation.
		/// </summary>
		/// <param name="context">A binding context.</param>
		/// <returns>A task that represents the asynchronous operation.</returns>
		Task BindValue(ValueBindingContext context);
	}
}
