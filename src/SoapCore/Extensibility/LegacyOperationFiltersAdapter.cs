using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

// Type or member is obsolete - Adapter for legacy interfaces. Remove once legacy interfaces removed.
#pragma warning disable CS0618
namespace SoapCore.Extensibility
{
	internal class LegacyOperationFiltersAdapter : IOperationFilter
	{
		private readonly ISoapModelBounder _modelBounder;
		private readonly IServiceProvider _serviceProvider;

		internal LegacyOperationFiltersAdapter(ISoapModelBounder modelBounder, IServiceProvider serviceProvider)
		{
			_modelBounder = modelBounder;
			_serviceProvider = serviceProvider;
		}

		public async Task OnOperationExecution(OperationExecutingContext context, OperationFilterExecutionDelegate next)
		{
			var operation = context.OperationDescription;
			var arguments = context.OperationArguments;
			var httpContext = context.HttpContext;

			// Execute model binding filters
			object modelBindingOutput = null;
			foreach (var modelBindingFilter in _serviceProvider.GetServices<IModelBindingFilter>())
			{
				foreach (var modelType in modelBindingFilter.ModelTypes)
				{
					foreach (var parameterInfo in operation.InParameters)
					{
						var arg = arguments[parameterInfo.Index];
						if (arg != null && arg.GetType() == modelType)
						{
							modelBindingFilter.OnModelBound(arg, _serviceProvider, out modelBindingOutput);
						}
					}
				}
			}

			// Execute Mvc ActionFilters
			foreach (var actionFilterAttr in operation.DispatchMethod.CustomAttributes.Where(a => a.AttributeType.Name == "ServiceFilterAttribute"))
			{
				var actionFilter = _serviceProvider.GetService(actionFilterAttr.ConstructorArguments[0].Value as Type);
				actionFilter.GetType().GetMethod("OnSoapActionExecuting")?.Invoke(actionFilter, new[] { operation.Name, arguments, httpContext, modelBindingOutput });
			}

			// Invoke OnModelBound
			_modelBounder?.OnModelBound(context.OperationDescription.DispatchMethod, context.OperationArguments);

			// Tune service instance for operation call
			var serviceOperationTuners = _serviceProvider.GetServices<IServiceOperationTuner>();
			foreach (var operationTuner in serviceOperationTuners)
			{
				operationTuner.Tune(context.HttpContext, context.ServiceInstance, context.OperationDescription);
			}

			await next();
		}
	}
}
