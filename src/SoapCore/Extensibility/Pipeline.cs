using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SoapCore.Extensibility
{
	internal sealed class Pipeline<TFilter, TRequestContext, TResponseContext>
			where TResponseContext : class
			where TRequestContext : class
	{
		private readonly ILogger _logger;
		private readonly Func<TFilter, TRequestContext, Func<Task<TResponseContext>>, Task> _runFilter;
		private readonly Func<TRequestContext, bool> _wasResultSet;
		private readonly Func<TRequestContext, TResponseContext> _createResponseContext;
		private readonly Func<TRequestContext, Task<TResponseContext>> _callCore;
		private Func<TRequestContext, Task<TResponseContext>> _execute;

		private Pipeline(
			Func<TFilter, TRequestContext, Func<Task<TResponseContext>>, Task> runFilter,
			Func<TRequestContext, bool> wasResultSet,
			Func<TRequestContext, TResponseContext> createResponseContext,
			Func<TRequestContext, Task<TResponseContext>> callCore,
			ILogger logger)
		{
			_runFilter = runFilter;
			_wasResultSet = wasResultSet;
			_createResponseContext = createResponseContext;
			_callCore = callCore;
			_execute = async (requestContext) => await _callCore(requestContext);
			_logger = logger;
		}

		public static Pipeline<ISoapMessageFilter, MessageFilterExecutingContext, MessageFilterExecutedContext> CreateMessageFilterPipeline(
			IEnumerable<ISoapMessageFilter> filters,
			Func<MessageFilterExecutingContext, Task<MessageFilterExecutedContext>> core,
			ILogger logger)
		{
			var pipeline = new Pipeline<ISoapMessageFilter, MessageFilterExecutingContext, MessageFilterExecutedContext>(
				async (filter, ctx, next) => await filter.OnMessageReceived(ctx, () => next()),
				ctx => ctx.Result != null,
				ctx => MessageFilterExecutedContext.Create(ctx.Message, ctx.ServiceDescription),
				async (ctx) => await core(ctx),
				logger);
			pipeline.AddFilters(filters);
			return pipeline;
		}

		internal static Pipeline<IOperationFilter, OperationExecutingContext, OperationExecutedContext> CreateOperationFilterPipeline(
			IEnumerable<IOperationFilter> filters,
			IOperationInvoker core,
			ILogger logger)
		{
			var pipeline = new Pipeline<IOperationFilter, OperationExecutingContext, OperationExecutedContext>(
				async (filter, ctx, next) => await filter.OnOperationExecution(ctx, () => next()),
				ctx => ctx.Result != null,
				ctx => new OperationExecutedContext(ctx.HttpContext, ctx.OperationArguments, ctx.ServiceInstance, ctx.OperationDescription, null),
				async (ctx) =>
				{
					var responseObject = await core.InvokeAsync(ctx.OperationDescription.DispatchMethod, ctx.ServiceInstance, ctx.OperationArguments);
					return new OperationExecutedContext(ctx, responseObject);
				},
				logger);

			pipeline.AddFilters(filters);
			return pipeline;
		}

		internal async Task<TResponseContext> Execute(TRequestContext context) => await _execute(context);

		private void AddFilters(IEnumerable<TFilter> filters)
		{
			_execute = GetPipelineExecute(filters);
		}

		private Func<TRequestContext, Task<TResponseContext>> GetPipelineExecute(IEnumerable<TFilter> filters)
		{
			Func<TRequestContext, Task<TResponseContext>> executeOperation = _execute;

			var nextMessageFilter = executeOperation;
			foreach (var messageFilter in filters.Reverse())
			{
				// if next filter is not called, this is null
				TResponseContext capturedNextFilterResult = null;

				// Since the filter doesn't actually return anything, I need a wrapper to capture the result of next filter to pass it up the pipeline
				var nextMessageFilterInClosure = nextMessageFilter;

				var messageFilterInClosure = messageFilter;
				Func<TRequestContext, Task<TResponseContext>> messageFilterDelegate = async (requestContext) =>
				{
					Func<Task<TResponseContext>> executeNextFilter = async () =>
					{
						var isPipelineShortCircuited = _wasResultSet(requestContext);
						if (isPipelineShortCircuited)
						{
							capturedNextFilterResult = _createResponseContext(requestContext);
							return capturedNextFilterResult;
						}

						var result = await nextMessageFilterInClosure(requestContext);
						capturedNextFilterResult = result;
						return result;
					};

					await _runFilter(messageFilterInClosure, requestContext, executeNextFilter);
					var filterDidntCallNext = capturedNextFilterResult == null;
					if (filterDidntCallNext && !_wasResultSet(requestContext))
					{
						_logger.LogDebug($"Filter {messageFilterInClosure.GetType()} short-circuited the pipeline (didn't call next() nor dit it set the {nameof(MessageFilterExecutingContext)}.{nameof(MessageFilterExecutingContext.Result)}). This service won't return any message, same as one-way MEP.");
					}

					return capturedNextFilterResult;
				};
				nextMessageFilter = messageFilterDelegate;
			}

			return nextMessageFilter;
		}
	}
}
