using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoapCore.MessageEncoder;
using SoapCore.ServiceModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace SoapCore.Extensibility
{
	internal class FilterPipeline
	{
		public static Func<Task<MessageFilterExecutedContext>> CreateMessageFilterPipeline(
			IEnumerable<ISoapMessageFilter> filters,
			ILogger logger,
			MessageFilterExecutingContext requestContext,
			Func<MessageFilterExecutingContext, Task<MessageFilterExecutedContext>> core)
		{
			var pipeline = new Pipeline<ISoapMessageFilter, MessageFilterExecutingContext, MessageFilterExecutedContext>(logger)
			{
				RunFilter = async (filter, ctx, next) => await filter.OnMessageReceived(ctx, () => next()),
				WasResultSet = ctx => ctx.Result != null,
				CreateResponseContext = ctx => MessageFilterExecutedContext.Create(ctx.Message, ctx.ServiceDescription),
				CallCore = async (ctx) => await core(ctx)
			};

			return pipeline.CreatePipeline(filters, requestContext);
		}

		internal static Func<Task<OperationExecutedContext>> CreateOperationFilterPipeline(
			IEnumerable<IOperationFilter> filters,
			OperationExecutingContext requestContext,
			IOperationInvoker core,
			ILogger logger)
		{
			var pipeline = new Pipeline<IOperationFilter, OperationExecutingContext, OperationExecutedContext>(logger)
			{
				RunFilter = async (filter, ctx, next) => await filter.OnOperationExecution(ctx, () => next()),
				WasResultSet = ctx => ctx.Result != null,
				CreateResponseContext = ctx => new OperationExecutedContext(ctx.HttpContext, ctx.OperationArguments, ctx.ServiceInstance, ctx.OperationDescription, null),
				CallCore = async (ctx) =>
				{
					var responseObject = await core.InvokeAsync(ctx.OperationDescription.DispatchMethod, ctx.ServiceInstance, ctx.OperationArguments);
					return new OperationExecutedContext(ctx, responseObject);
				}
			};

			return pipeline.CreatePipeline(filters, requestContext);
		}

		private class Pipeline<TFilter, TRequestContext, TResponseContext>
			where TResponseContext : class
			where TRequestContext : class
		{
			private readonly ILogger _logger;

			public Pipeline(ILogger logger)
			{
				_logger = logger;
			}

			internal Func<TFilter, TRequestContext, Func<Task<TResponseContext>>, Task> RunFilter { get; set; }
			internal Func<TRequestContext, bool> WasResultSet { get; set; }
			internal Func<TRequestContext, TResponseContext> CreateResponseContext { get; set; }
			internal Func<TRequestContext, Task<TResponseContext>> CallCore { get; set; }

			internal Func<Task<TResponseContext>> CreatePipeline(
				IEnumerable<TFilter> filters,
				TRequestContext requestContext)
			{
				Func<Task<TResponseContext>> executeOperation = async () => await CallCore(requestContext);

				var nextMessageFilter = executeOperation;
				foreach (var messageFilter in filters.Reverse())
				{
					// if next filter is not called, this is null
					TResponseContext capturedNextFilterResult = null;

					// Since the filter doesn't actually return anything, I need a wrapper to capture the result of next filter to pass it up the pipeline
					var nextMessageFilterInClosure = nextMessageFilter;
					Func<Task<TResponseContext>> executeNextFilter = async () =>
					{
						var isPipelineShortCircuited = WasResultSet(requestContext);
						if (isPipelineShortCircuited)
						{
							capturedNextFilterResult = CreateResponseContext(requestContext);
							return capturedNextFilterResult;
						}

						var result = await nextMessageFilterInClosure();
						capturedNextFilterResult = result;
						return result;
					};

					var messageFilterInClosure = messageFilter;
					Func<Task<TResponseContext>> messageFilterDelegate = async () =>
					{
						await RunFilter(messageFilterInClosure, requestContext, executeNextFilter);
						var filterDidntCallNext = capturedNextFilterResult == null;
						if (filterDidntCallNext && !WasResultSet(requestContext))
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
}
