using System.Threading.Tasks;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// A continuation delegate for <see cref="IOperationFilter"/>. It either calls next filter or the operation invocation.
	/// </summary>
	/// <returns>A context describing a result of the pipeline.</returns>
	public delegate Task<OperationExecutedContext> OperationFilterExecutionDelegate();

	/// <summary>
	/// <para>
	/// A filter that surrounds execution of the operation execution. It is run after message filters and
	/// value binding, but before operation invocation.
	/// At the time of the calling, SoapCore already know what operation should be called and
	/// what are the arguments, unlike <see cref="ISoapMessageFilter" /> we no longer have access to the message.
	/// </para>
	/// <para>
	/// It can can
	/// <list type="bullet">
	/// <item>check values of the arguments, operation or service.</item>
	/// <item>short circuit the execution by not calling <code>next</code>,
	/// throwing an exception or by assigning result to the context.</item>
	/// </para>
	/// </list>
	/// </summary>
	public interface IOperationFilter
	{
		/// <summary>
		/// A filter that is called before the rest of the operation execution pipeline.
		/// </summary>
		/// <param name="context">A context with info about the operation and its arguments. Content of the context is <em>undetermined</em> after the <paramref name="next"/> call (other filters might modify the context), so if you need data, copy them..</param>
		/// <param name="next">A continuation of the pipeline, either next filter or operation invocation.</param>
		/// <returns>A task that represents the asynchronous operation.</returns>
		Task OnOperationExecution(OperationExecutingContext context, OperationFilterExecutionDelegate next);
	}
}
