using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

// Use of obsolete symbol - This is an orchestration to keep obsolete implementations working
#pragma warning disable 618

namespace SoapCore.Extensibility
{
	internal static class LegacyApiAdapterExtensions
	{
		internal static IEnumerable<ISoapMessageFilter> GetLegacyApiFilters(this IServiceProvider serviceProvider)
		{
			// Order of the adapters is important.
			// Since interfaces can be registered anytime, we must create adapters for every request
			var messageFilters = serviceProvider.GetServices<IMessageFilter>();
			foreach (var messageFilter in messageFilters)
			{
				yield return new MessageFilterAdapter(messageFilter);
			}

			var asyncMessageFilters = serviceProvider.GetServices<IAsyncMessageFilter>();
			foreach (var asyncMessageFilter in asyncMessageFilters)
			{
				yield return new AsyncMessageFilterAdapter(asyncMessageFilter);
			}

			var messageInspector = serviceProvider.GetService<IMessageInspector>();
			if (messageInspector != null)
			{
				yield return new MessageInspectorAdapter(messageInspector);
			}

			var messageInspector2Collection = serviceProvider.GetServices<IMessageInspector2>();
			foreach (var messageInspector2 in messageInspector2Collection)
			{
				yield return new MessageInspector2Adapter(messageInspector2);
			}
		}
	}
}
