using System.IO;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	internal class ServiceSut<TService>
		where TService : class
	{
		private const string _servicePath = "/Service.svc";

		private readonly ServiceCollection _serviceCollection;
		private readonly SoapOptions _defaultTestOptions;

		public ServiceSut(TService serviceInstance)
		{
			Service = serviceInstance;
			_serviceCollection = new ServiceCollection();
			_serviceCollection.AddSingleton(serviceInstance);
			_defaultTestOptions = new SoapOptions
			{
				Path = _servicePath,
				ServiceType = typeof(TService),
				EncoderOptions = new[]
				{
					new SoapEncoderOptions
					{
						MessageVersion = MessageVersion.Soap11,
						WriteEncoding = Encoding.UTF8,
						ReaderQuotas = XmlDictionaryReaderQuotas.Max
					}
				}
			};
		}

		public TService Service { get; }

		internal async Task<HttpContext> ProcessRequest(string body)
		{
			var message = $@"
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>{body}</s:Body>
</s:Envelope>";

			var features = new FeatureCollection();
			var httpContext = new DefaultHttpContext(features);

			features[typeof(IHttpRequestFeature)] = new HttpRequestFeature
			{
				Body = new MemoryStream(Encoding.UTF8.GetBytes(message)),
				Path = _servicePath,
				Headers = new HeaderDictionary
				{
					{ "Content-Type", "application/soap+xml" }
				}
			};
			features[typeof(IHttpResponseFeature)] = new HttpResponseFeature();
#if !ASPNET_21
				features[typeof(IRequestBodyPipeFeature)] = new RequestBodyPipeFeature(httpContext);
				features[typeof(IHttpResponseBodyFeature)] = new StreamResponseBodyFeature(new MemoryStream());
#endif

			//	features[typeof(IResponse)] = new RequestBodyPipeFeature(httpContext);
			var soapCore = new SoapEndpointMiddleware<CustomMessage>(
				Mock.Of<ILogger<SoapEndpointMiddleware<CustomMessage>>>(),
				httpCtx => Task.CompletedTask,
				_defaultTestOptions);
			var serviceProvider = _serviceCollection.BuildServiceProvider();
			await soapCore.Invoke(httpContext, serviceProvider);

			return httpContext;
		}

		internal ServiceSut<TService> RegisterFilter<TFilter>(TFilter filter)
			where TFilter : ISoapMessageFilter
		{
			_serviceCollection.AddSingleton<ISoapMessageFilter>(filter);
			return this;
		}

		internal ServiceSut<TService> RegisterFaultTransformer(IFaultExceptionTransformer transformer)
		{
			_serviceCollection.AddSingleton(transformer);
			return this;
		}
	}
}
