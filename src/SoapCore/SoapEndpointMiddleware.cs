using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoapCore.Extensibility;
using SoapCore.MessageEncoder;
using SoapCore.Meta;
using SoapCore.ServiceModel;

namespace SoapCore
{
	public class SoapEndpointMiddleware<T_MESSAGE>
		where T_MESSAGE : CustomMessage, new()
	{
		private readonly ILogger<SoapEndpointMiddleware<T_MESSAGE>> _logger;
		private readonly RequestDelegate _next;
		private readonly SoapOptions _options;
		private readonly ServiceDescription _service;
		private readonly string _endpointPath;
		private readonly SoapSerializer _serializer;
		private readonly Binding _binding;
		private readonly StringComparison _pathComparisonStrategy;
		private readonly ISoapModelBounder _soapModelBounder;
		private readonly bool _httpGetEnabled;
		private readonly bool _httpsGetEnabled;
		private readonly SoapMessageEncoder[] _messageEncoders;
		private readonly SerializerHelper _serializerHelper;
		private readonly XmlNamespaceManager _xmlNamespaceManager;

		[Obsolete]
		public SoapEndpointMiddleware(ILogger<SoapEndpointMiddleware<T_MESSAGE>> logger, RequestDelegate next, Type serviceType, string path, SoapEncoderOptions[] encoderOptions, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, bool httpGetEnabled, bool httpsGetEnabled)
		{
			_logger = logger;
			_next = next;
			_endpointPath = path;
			_serializer = serializer;
			_serializerHelper = new SerializerHelper(_serializer);
			_pathComparisonStrategy = caseInsensitivePath ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			_service = new ServiceDescription(serviceType);
			_soapModelBounder = soapModelBounder;
			_binding = binding;
			_httpGetEnabled = httpGetEnabled;
			_httpsGetEnabled = httpsGetEnabled;

			_messageEncoders = new SoapMessageEncoder[encoderOptions.Length];

			for (var i = 0; i < encoderOptions.Length; i++)
			{
				_messageEncoders[i] = new SoapMessageEncoder(encoderOptions[i].MessageVersion, encoderOptions[i].WriteEncoding, encoderOptions[i].ReaderQuotas, true, true);
			}
		}

		public SoapEndpointMiddleware(ILogger<SoapEndpointMiddleware<T_MESSAGE>> logger, RequestDelegate next, SoapOptions options)
		{
			_logger = logger;
			_next = next;
			_options = options;
			_endpointPath = options.Path;
			_serializer = options.SoapSerializer;
			_serializerHelper = new SerializerHelper(_serializer);
			_pathComparisonStrategy = options.CaseInsensitivePath ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			_service = new ServiceDescription(options.ServiceType);
			_soapModelBounder = options.SoapModelBounder;
			_binding = options.Binding;
			_httpGetEnabled = options.HttpGetEnabled;
			_httpsGetEnabled = options.HttpsGetEnabled;
			_xmlNamespaceManager = options.XmlNamespacePrefixOverrides ?? Namespaces.CreateDefaultXmlNamespaceManager();
			Namespaces.AddDefaultNamespaces(_xmlNamespaceManager);

			_messageEncoders = new SoapMessageEncoder[options.EncoderOptions.Length];

			for (var i = 0; i < options.EncoderOptions.Length; i++)
			{
				_messageEncoders[i] = new SoapMessageEncoder(options.EncoderOptions[i].MessageVersion, options.EncoderOptions[i].WriteEncoding, options.EncoderOptions[i].ReaderQuotas, options.OmitXmlDeclaration, options.IndentXml);
			}
		}

		public async Task Invoke(HttpContext httpContext, IServiceProvider serviceProvider)
		{
			if (_options != null)
			{
				if (_options.BufferThreshold > 0 && _options.BufferLimit > 0)
				{
					httpContext.Request.EnableBuffering(_options.BufferThreshold, _options.BufferLimit);
				}
				else if (_options.BufferThreshold > 0)
				{
					httpContext.Request.EnableBuffering(_options.BufferThreshold);
				}
				else
				{
					httpContext.Request.EnableBuffering();
				}
			}
			else
			{
				httpContext.Request.EnableBuffering();
			}

			var trailPathTuner = serviceProvider.GetServices<TrailingServicePathTuner>().FirstOrDefault();

			trailPathTuner?.ConvertPath(httpContext);

			if (httpContext.Request.Path.Equals(_endpointPath, _pathComparisonStrategy))
			{
				if (httpContext.Request.Method?.ToLower() == "get")
				{
					// If GET is not enabled, either for HTTP or HTTPS, return a 403 instead of the WSDL
					if ((httpContext.Request.IsHttps && !_httpsGetEnabled) || (!httpContext.Request.IsHttps && !_httpGetEnabled))
					{
						httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
						return;
					}
				}

				try
				{
					_logger.LogDebug($"Received SOAP Request for {httpContext.Request.Path} ({httpContext.Request.ContentLength ?? 0} bytes)");

					if (httpContext.Request.Query.ContainsKey("wsdl") && httpContext.Request.Method?.ToLower() == "get")
					{
						await ProcessMeta(httpContext);
					}
					else
					{
						await ProcessOperation(httpContext, serviceProvider);
					}
				}
				catch (Exception ex)
				{
					_logger.LogCritical(ex, $"An error occurred when trying to service a request on SOAP endpoint: {httpContext.Request.Path}");

					// Let's pass this up the middleware chain after we have logged this issue
					// and signaled the critical of it
					throw;
				}
			}
			else
			{
				await _next(httpContext);
			}
		}

#if ASPNET_21
		private static Task WriteMessageAsync(SoapMessageEncoder messageEncoder, Message responseMessage, HttpContext httpContext)
		{
			return messageEncoder.WriteMessageAsync(responseMessage, httpContext.Response.Body);
		}

		private static Task<Message> ReadMessageAsync(HttpContext httpContext, SoapMessageEncoder messageEncoder)
		{
			return messageEncoder.ReadMessageAsync(httpContext.Request.Body, 0x10000, httpContext.Request.ContentType);
		}
#endif
#if ASPNET_30
		private static Task WriteMessageAsync(SoapMessageEncoder messageEncoder, Message responseMessage, HttpContext httpContext)
		{
			return messageEncoder.WriteMessageAsync(responseMessage, httpContext.Response.BodyWriter);
		}

		private static Task<Message> ReadMessageAsync(HttpContext httpContext, SoapMessageEncoder messageEncoder)
		{
			return messageEncoder.ReadMessageAsync(httpContext.Request.BodyReader, 0x10000, httpContext.Request.ContentType);
		}
#endif

		private async Task ProcessMeta(HttpContext httpContext)
		{
			var baseUrl = httpContext.Request.Scheme + "://" + httpContext.Request.Host + httpContext.Request.PathBase + httpContext.Request.Path;
			var bodyWriter = _serializer == SoapSerializer.XmlSerializer ? new MetaBodyWriter(_service, baseUrl, _binding, _xmlNamespaceManager) : (BodyWriter)new MetaWCFBodyWriter(_service, baseUrl, _binding);
			var responseMessage = Message.CreateMessage(_messageEncoders[0].MessageVersion, null, bodyWriter);
			responseMessage = new MetaMessage(responseMessage, _service, _binding, _xmlNamespaceManager);

			httpContext.Response.ContentType = _messageEncoders[0].ContentType;

			await WriteMessageAsync(_messageEncoders[0], responseMessage, httpContext);
		}

		private async Task ProcessOperation(HttpContext httpContext, IServiceProvider serviceProvider)
		{
			//Reload the body to ensure we have the full message
			var memoryStream = new MemoryStream((int)httpContext.Request.ContentLength.GetValueOrDefault(1024));
			await httpContext.Request.Body.CopyToAsync(memoryStream).ConfigureAwait(false);
			memoryStream.Seek(0, SeekOrigin.Begin);
			httpContext.Request.Body = memoryStream;

			//Return metadata if no request, provided this is a GET request
			if (httpContext.Request.Body.Length == 0 && httpContext.Request.Method?.ToLower() == "get")
			{
				await ProcessMeta(httpContext);
				return;
			}

			// Get the encoder based on Content Type
			var messageEncoder = _messageEncoders[0];

			foreach (var encoder in _messageEncoders)
			{
				if (encoder.IsContentTypeSupported(httpContext.Request.ContentType))
				{
					messageEncoder = encoder;
					break;
				}
			}

			//Get the message
			Message requestMessage = await ReadMessageAsync(httpContext, messageEncoder);

			var filters = serviceProvider.GetServices<ISoapMessageFilter>()
				.Concat(serviceProvider.GetLegacyApiFilters())
				.ToList();
			var messageFilterPipeline = Pipeline<ISoapMessageFilter, MessageFilterExecutingContext, MessageFilterExecutedContext>
				.CreateMessageFilterPipeline(
					filters,
					async ctx => await ProcessOneOperation(ctx.Message, messageEncoder, httpContext, serviceProvider),
					_logger);
			try
			{
				var context = new MessageFilterExecutingContext(requestMessage, _service);
				await messageFilterPipeline.Execute(context);
			}
			catch (Exception ex)
			{
				await WriteErrorResponseMessage(ex, StatusCodes.Status500InternalServerError, serviceProvider, requestMessage, httpContext);
			}
		}

		private async Task<MessageFilterExecutedContext> ProcessOneOperation(Message requestMessage, SoapMessageEncoder messageEncoder, HttpContext httpContext, IServiceProvider serviceProvider)
		{
			Message responseMessage;

			// for getting soapaction and parameters in body
			// GetReaderAtBodyContents must not be called twice in one request
			using (var reader = requestMessage.GetReaderAtBodyContents())
			{
				var soapAction = HeadersHelper.GetSoapAction(httpContext, requestMessage, reader);
				requestMessage.Headers.Action = soapAction;
				var operation = _service.Operations.FirstOrDefault(o => o.SoapAction.Equals(soapAction, StringComparison.Ordinal) || o.Name.Equals(soapAction, StringComparison.Ordinal));
				if (operation == null)
				{
					throw new InvalidOperationException($"No operation found for specified action: {requestMessage.Headers.Action}");
				}

				_logger.LogInformation($"Request for operation {operation.Contract.Name}.{operation.Name} received");

				try
				{
					//Create an instance of the service class
					var serviceInstance = serviceProvider.GetRequiredService(_service.ServiceType);

					SetMessageHeadersToProperty(requestMessage, serviceInstance);

					// Get operation arguments from message
					var arguments = GetRequestArguments(requestMessage, reader, operation, httpContext);

					await ExecuteValueBinders(operation, arguments, httpContext, serviceProvider);

					var invoker = serviceProvider.GetService<IOperationInvoker>() ?? new DefaultOperationInvoker();
					invoker = new UnwrapReflectionDecorator(invoker);

					var operationFilters = serviceProvider.GetServices<IOperationFilter>().ToList();
					operationFilters.Add(new LegacyOperationFiltersAdapter(_soapModelBounder, serviceProvider));
					var pipeline = Pipeline<IOperationFilter, OperationExecutingContext, OperationExecutedContext>
						.CreateOperationFilterPipeline(
							operationFilters,
							invoker,
							_logger);

					var context = new OperationExecutingContext(httpContext, arguments, serviceInstance, operation);
					var executedContext = await pipeline.Execute(context);
					var responseObject = executedContext.Result;

					if (operation.IsOneWay)
					{
						httpContext.Response.StatusCode = (int)HttpStatusCode.Accepted;
						return MessageFilterExecutedContext.CreateOneWay(_service);
					}

					var resultOutDictionary = new Dictionary<string, object>();
					foreach (var parameterInfo in operation.OutParameters)
					{
						resultOutDictionary[parameterInfo.Name] = arguments[parameterInfo.Index];
					}

					responseMessage = CreateResponseMessage(
						operation, responseObject, resultOutDictionary, soapAction, requestMessage, messageEncoder);

					httpContext.Response.ContentType = httpContext.Request.ContentType;
					httpContext.Response.Headers["SOAPAction"] = responseMessage.Headers.Action;

					SetHttpResponse(httpContext, responseMessage);
					await WriteMessageAsync(messageEncoder, responseMessage, httpContext);
				}
				catch (Exception exception)
				{
					//if (exception is TargetInvocationException targetInvocationException)
					//{
					//	exception = targetInvocationException.InnerException;
					//}

					_logger.LogWarning(0, exception, exception?.Message);
					responseMessage = await WriteErrorResponseMessage(exception, StatusCodes.Status500InternalServerError, serviceProvider, requestMessage, httpContext);
				}
			}

			return MessageFilterExecutedContext.Create(responseMessage, _service);
		}

		private Message CreateResponseMessage(
			OperationDescription operation,
			object responseObject,
			Dictionary<string, object> resultOutDictionary,
			string soapAction,
			Message requestMessage,
			SoapMessageEncoder soapMessageEncoder)
		{
			Message responseMessage;

			// Create response message
			var bodyWriter = new ServiceBodyWriter(_serializer, operation, responseObject, resultOutDictionary);

			if (soapMessageEncoder.MessageVersion.Addressing == AddressingVersion.WSAddressing10)
			{
				responseMessage = Message.CreateMessage(soapMessageEncoder.MessageVersion, soapAction, bodyWriter);
				T_MESSAGE customMessage = new T_MESSAGE
				{
					Message = responseMessage,
					NamespaceManager = _xmlNamespaceManager
				};
				responseMessage = customMessage;
				//responseMessage.Message = responseMessage;
				responseMessage.Headers.Action = operation.ReplyAction;
				responseMessage.Headers.RelatesTo = requestMessage.Headers.MessageId;
				responseMessage.Headers.To = requestMessage.Headers.ReplyTo?.Uri;
			}
			else
			{
				responseMessage = Message.CreateMessage(soapMessageEncoder.MessageVersion, null, bodyWriter);
				T_MESSAGE customMessage = new T_MESSAGE
				{
					Message = responseMessage,
					NamespaceManager = _xmlNamespaceManager
				};
				responseMessage = customMessage;

				if (responseObject != null)
				{
					var messageHeaderMembers = responseObject.GetType().GetMembersWithAttribute<MessageHeaderAttribute>();
					foreach (var messageHeaderMember in messageHeaderMembers)
					{
						var messageHeaderAttribute = messageHeaderMember.GetCustomAttribute<MessageHeaderAttribute>();
						responseMessage.Headers.Add(MessageHeader.CreateHeader(messageHeaderAttribute.Name ?? messageHeaderMember.Name, operation.Contract.Namespace, messageHeaderMember.GetPropertyOrFieldValue(responseObject)));
					}
				}
			}

			return responseMessage;
		}

		private async Task ExecuteValueBinders(
            OperationDescription operation,
            object[] arguments,
            HttpContext httpContext,
            IServiceProvider serviceProvider)
        {
			var binderProviders = serviceProvider.GetServices<IValueBinderProvider>().ToList();
			if (!binderProviders.Any())
			{
				return;
			}

			foreach (var parameterInfo in operation.InParameters)
			{
				var argument = arguments[parameterInfo.Index];
				var providerContext = new ValueBinderProviderContext(operation, parameterInfo, argument?.GetType());
				var valueBinders = binderProviders
					.Select(binderProvider => binderProvider.GetBinder(providerContext))
					.Where(binder => binder != null);

				var valueContext = new ValueBindingContext(argument, parameterInfo, operation, httpContext);
				foreach (var valueBinder in valueBinders)
				{
					await valueBinder.BindValue(valueContext);
				}
			}
		}

		private void SetMessageHeadersToProperty(Message requestMessage, object serviceInstance)
		{
			var headerProperty = _service.ServiceType.GetProperty("MessageHeaders");
			if (headerProperty != null && headerProperty.PropertyType == requestMessage.Headers.GetType())
			{
				headerProperty.SetValue(serviceInstance, requestMessage.Headers);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private object[] GetRequestArguments(Message requestMessage, System.Xml.XmlDictionaryReader xmlReader, OperationDescription operation, HttpContext httpContext)
		{
			var arguments = new object[operation.AllParameters.Length];

			// if any ordering issues, possible to rewrite like:
			/*while (!xmlReader.EOF)
			{
				var parameterInfo = operation.InParameters.FirstOrDefault(p => p.Name == xmlReader.LocalName && p.Namespace == xmlReader.NamespaceURI);
				if (parameterInfo == null)
				{
					xmlReader.Skip();
					continue;
				}
				var parameterName = parameterInfo.Name;
				var parameterNs = parameterInfo.Namespace;
				...
			}*/

			// Find the element for the operation's data
			if (!operation.IsMessageContractRequest)
			{
				xmlReader.ReadStartElement(operation.Name, operation.Contract.Namespace);

				foreach (var parameterInfo in operation.InParameters)
				{
					var parameterType = parameterInfo.Parameter.ParameterType;

					if (parameterType == typeof(HttpContext))
					{
						arguments[parameterInfo.Index] = httpContext;
					}
					else
					{
						var argumentValue = _serializerHelper.DeserializeInputParameter(xmlReader, parameterType, parameterInfo.Name, operation.Contract.Namespace, parameterInfo);

						//fix https://github.com/DigDes/SoapCore/issues/379 (hack, need research)
						if (argumentValue == null)
						{
							argumentValue = _serializerHelper.DeserializeInputParameter(xmlReader, parameterType, parameterInfo.Name, parameterInfo.Namespace, parameterInfo);
						}

						arguments[parameterInfo.Index] = argumentValue;
					}
				}
			}
			else
			{
				// MessageContracts are constrained to having one "InParameter". We can do special logic on
				// for this
				Debug.Assert(operation.InParameters.Length == 1, "MessageContracts are constrained to having one 'InParameter'");

				var parameterInfo = operation.InParameters[0];
				var parameterType = parameterInfo.Parameter.ParameterType;

				var messageContractAttribute = parameterType.GetCustomAttribute<MessageContractAttribute>();

				Debug.Assert(messageContractAttribute != null, "operation.IsMessageContractRequest should be false if this is null");

				var @namespace = parameterInfo.Namespace ?? operation.Contract.Namespace;

				if (messageContractAttribute.IsWrapped && !parameterType.GetMembersWithAttribute<MessageHeaderAttribute>().Any())
				{
					//https://github.com/DigDes/SoapCore/issues/385
					if (operation.DispatchMethod.GetCustomAttribute<XmlSerializerFormatAttribute>()?.Style == OperationFormatStyle.Rpc)
					{
						var importer = new SoapReflectionImporter(@namespace);
						var map = new XmlReflectionMember
						{
							IsReturnValue = false,
							MemberName = parameterInfo.Name,
							MemberType = parameterType
						};
						var mapping = importer.ImportMembersMapping(parameterInfo.Name, @namespace, new[] { map }, false, true);
						var serializer = XmlSerializer.FromMappings(new[] { mapping })[0];
						var value = serializer.Deserialize(xmlReader);
						if (value is object[] o && o.Length > 0)
						{
							arguments[parameterInfo.Index] = o[0];
						}
					}
					else
					{
						// It's wrapped so we treat it like normal!
						arguments[parameterInfo.Index] = _serializerHelper.DeserializeInputParameter(xmlReader, parameterInfo.Parameter.ParameterType, parameterInfo.Name, @namespace, parameterInfo);
					}
				}
				else
				{
					var messageHeadersMembers = parameterType.GetPropertyOrFieldMembers()
						.Where(x => x.GetCustomAttribute<MessageHeaderAttribute>() != null)
						.Select(mi => new
						{
							MemberInfo = mi,
							MessageHeaderMemberAttribute = mi.GetCustomAttribute<MessageHeaderAttribute>()
						}).ToArray();

					var wrapperObject = Activator.CreateInstance(parameterInfo.Parameter.ParameterType);

					for (var i = 0; i < requestMessage.Headers.Count; i++)
					{
						var header = requestMessage.Headers[i];
						var member = messageHeadersMembers.FirstOrDefault(x => x.MessageHeaderMemberAttribute.Name == header.Name || x.MemberInfo.Name == header.Name);

						if (member != null)
						{
							var reader = requestMessage.Headers.GetReaderAtHeader(i);

							var value = _serializerHelper.DeserializeInputParameter(reader, member.MemberInfo.GetPropertyOrFieldType(), member.MessageHeaderMemberAttribute.Name ?? member.MemberInfo.Name, member.MessageHeaderMemberAttribute.Namespace ?? @namespace);

							member.MemberInfo.SetValueToPropertyOrField(wrapperObject, value);
						}
					}

					// This object isn't a wrapper element, so we will hunt for the nested message body
					// member inside of it
					var messageBodyMembers = parameterType.GetPropertyOrFieldMembers().Where(x => x.GetCustomAttribute<MessageBodyMemberAttribute>() != null).Select(mi => new
					{
						Member = mi,
						MessageBodyMemberAttribute = mi.GetCustomAttribute<MessageBodyMemberAttribute>()
					}).OrderBy(x => x.MessageBodyMemberAttribute.Order);

					if (messageContractAttribute.IsWrapped)
					{
						xmlReader.Read();
					}

					foreach (var messageBodyMember in messageBodyMembers)
					{
						var messageBodyMemberAttribute = messageBodyMember.MessageBodyMemberAttribute;
						var messageBodyMemberInfo = messageBodyMember.Member;

						var innerParameterName = messageBodyMemberAttribute.Name ?? messageBodyMemberInfo.Name;
						var innerParameterNs = messageBodyMemberAttribute.Namespace ?? @namespace;
						var innerParameterType = messageBodyMemberInfo.GetPropertyOrFieldType();

						//xmlReader.MoveToStartElement(innerParameterName, innerParameterNs);
						var innerParameter = _serializerHelper.DeserializeInputParameter(xmlReader, innerParameterType, innerParameterName, innerParameterNs, parameterInfo);

						messageBodyMemberInfo.SetValueToPropertyOrField(wrapperObject, innerParameter);
					}

					arguments[parameterInfo.Index] = wrapperObject;
				}
			}

			foreach (var parameterInfo in operation.OutParameters)
			{
				if (arguments[parameterInfo.Index] != null)
				{
					// do not overwrite input ref parameters
					continue;
				}

				if (parameterInfo.Parameter.ParameterType.Name == "Guid&")
				{
					arguments[parameterInfo.Index] = Guid.Empty;
				}
				else if (parameterInfo.Parameter.ParameterType.Name == "String&" || parameterInfo.Parameter.ParameterType.GetElementType().IsArray)
				{
					arguments[parameterInfo.Index] = null;
				}
				else
				{
					var type = parameterInfo.Parameter.ParameterType.GetElementType();
					arguments[parameterInfo.Index] = Activator.CreateInstance(type);
				}
			}

			return arguments;
		}

		/// <summary>
		/// Helper message to write an error response message in case of an exception.
		/// </summary>
		/// <param name="exception">
		/// The exception that caused the failure.
		/// </param>
		/// <param name="statusCode">
		/// The HTTP status code that shall be returned to the caller.
		/// </param>
		/// <param name="serviceProvider">
		/// The DI container.
		/// </param>
		/// <param name="requestMessage">
		/// The Message for the incoming request
		/// </param>
		/// <param name="httpContext">
		/// The HTTP context that received the response message.
		/// </param>
		/// <returns>
		/// Returns the constructed message (which is implicitly written to the response
		/// and therefore must not be handled by the caller).
		/// </returns>
		private async Task<Message> WriteErrorResponseMessage(
			Exception exception,
			int statusCode,
			IServiceProvider serviceProvider,
			Message requestMessage,
			HttpContext httpContext)
		{
			var faultExceptionTransformer = serviceProvider.GetRequiredService<IFaultExceptionTransformer>();
			var faultMessage = faultExceptionTransformer.ProvideFault(exception, _messageEncoders[0].MessageVersion, _xmlNamespaceManager);

			httpContext.Response.ContentType = httpContext.Request.ContentType;
			httpContext.Response.Headers["SOAPAction"] = faultMessage.Headers.Action;
			httpContext.Response.StatusCode = statusCode;

			SetHttpResponse(httpContext, faultMessage);

			if (_messageEncoders[0].MessageVersion.Addressing == AddressingVersion.WSAddressing10)
			{
				// TODO: Some additional work needs to be done in order to support setting the action. Simply setting it to
				// "http://www.w3.org/2005/08/addressing/fault" will cause the WCF Client to not be able to figure out the type
				faultMessage.Headers.RelatesTo = requestMessage.Headers.MessageId;
				faultMessage.Headers.To = requestMessage.Headers.ReplyTo?.Uri;
			}

			await WriteMessageAsync(_messageEncoders[0], faultMessage, httpContext);

			return faultMessage;
		}

		private void SetHttpResponse(HttpContext httpContext, Message message)
		{
			if (!message.Properties.TryGetValue(HttpResponseMessageProperty.Name, out var value)
#pragma warning disable SA1119 // StatementMustNotUseUnnecessaryParenthesis
				|| !(value is HttpResponseMessageProperty httpProperty))
#pragma warning restore SA1119 // StatementMustNotUseUnnecessaryParenthesis
			{
				return;
			}

			httpContext.Response.StatusCode = (int)httpProperty.StatusCode;

			var feature = httpContext.Features.Get<IHttpResponseFeature>();
			if (feature != null && !string.IsNullOrEmpty(httpProperty.StatusDescription))
			{
				feature.ReasonPhrase = httpProperty.StatusDescription;
			}

			foreach (string key in httpProperty.Headers.Keys)
			{
				httpContext.Response.Headers.Add(key, httpProperty.Headers.GetValues(key));
			}
		}
	}
}
