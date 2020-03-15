using System.ServiceModel;
using System.Threading.Tasks;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// A continuation delegate for <see cref="ISoapMessageFilter"/>. It either calls next filter or the rest of the message processing.
	/// </summary>
	/// <returns>A context describing a result of the pipeline.</returns>
	public delegate Task<MessageFilterExecutedContext> MessageFilterExecutionDelegate();

	/// <summary>
	/// <para>
	/// A filter extension point that is run at the beginning of the pipeline even before the context of the message is deserialized and/or
	/// after the operation has been executed and the response is already created.
	/// </para>
	/// <para>
	/// You can also filter out the operation execution completely, because request didn't satisfy the filter.
	/// </para>
	/// <para>
	/// Use this filter if you need to:
	/// <list type="bullet">
	///   <item>Have access to the XML structure of the message (e.g. implement some SOAP extension).</item>
	///   <item>Run code before any message processing (e.g. logging).</item>
	/// </list>
	/// </para>
	/// <para>
	/// <example>
	/// <code>
	/// internal class SoapFaultsLogFilter : ISoapMessageFilter
	/// {
	///   private readonly ILogger _logger;
	///   public SoapFaultsLogFilter(ILogger logger)
	///   {
	///     _logger = logger;
	///   }
	///   public async Task OnMessageReceived(SoapMessageFilterContext requestContext, MessageFilterExecutionDelegate next)
	///   {
	///     var requestAction = requestContext.Message.Headers.Action;
	///     var responseContext = await next();
	///     if (responseContext.Message.IsFault)
	///     {
	///       _logger.LogWarning("RPC call to {action} failed.", requestAction);
	///     }
	///   }
	/// }
	/// </code>
	/// </example>
	/// </para>
	/// </summary>
	public interface ISoapMessageFilter
	{
		/// <summary>
		/// A filter that checks the message before allowing it to pass to the rest of the pipeline.
		/// If the <paramref name="next"/>If continuation is not called, the result sent to the client is the same as if the operation for the message wasn't found on the service.
		/// If you need to use in synchronous code, just call next and return <see cref="Task.CompletedTask"/>.
		/// </summary>
		/// <param name="requestContext">A context with info about the message. Content of the context is <em>undetermined</em> after the <paramref name="next"/> call (other filters might modify the context), so if you need data, copy them..</param>
		/// <param name="next">A continuation of the pipeline, either next message filter or the pipeline.</param>
		/// <returns>A task that represents the asynchronous operation.</returns>
		/// <exception cref="FaultException">Throw this exception if you want custom faultcode.</exception>
		/// <exception cref="FaultException{TDetail}">Throw this exception if you want to include fault detail into the fault.</exception>
		Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next);
	}
}
