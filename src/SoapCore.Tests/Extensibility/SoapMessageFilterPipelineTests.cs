using System;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	[TestClass]
	public class SoapMessageFilterPipelineTests
	{
		private const string _servicePath = "/Service.svc";
		private const string _echoBody = @"<Echo xmlns=""http://tempuri.org/""><text>abc</text></Echo>";

		private readonly SoapOptions _defaultTestOptions = new SoapOptions
		{
			Path = _servicePath,
			ServiceType = typeof(PingService),
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

		private IServiceCollection _serviceCollection;
		private PingService _pingService;
		private int _checkpoint;

		[TestInitialize]
		public void Reset()
		{
			_checkpoint = 0;
			_serviceCollection = new ServiceCollection();
			_pingService = new PingService(GetCheckpoint);
			_serviceCollection.AddSingleton(_pingService);
		}

		[TestMethod]
		public async Task FiltersAreCalledInPipelineSequence()
		{
			var filters = new[]
			{
				new TestFilter(GetCheckpoint),
				new TestFilter(GetCheckpoint),
				new TestFilter(GetCheckpoint),
			};
			RegisterFilters(filters.Select(x => (ISoapMessageFilter)x).ToArray());

			await ProcessPingMessage();

			var filtersInPipelineOrder = filters.OrderBy(x => x.EntryCheckpoint).ToArray();
			var filter1 = filtersInPipelineOrder[0];
			var filter2 = filtersInPipelineOrder[1];
			var filter3 = filtersInPipelineOrder[2];
			Assert.AreEqual(1, filter1.EntryCheckpoint);
			Assert.AreEqual(2, filter2.EntryCheckpoint);
			Assert.AreEqual(3, filter3.EntryCheckpoint);
			Assert.AreEqual(4, _pingService.PingCheckpoint);
			Assert.AreEqual(5, filter3.ExitCheckpoint);
			Assert.AreEqual(6, filter2.ExitCheckpoint);
			Assert.AreEqual(7, filter1.ExitCheckpoint);
		}

		[TestMethod]
		public async Task FilterCanChangeMessageForRestOfPipeline()
		{
			RegisterFilters(new ChangeActionFilter(_echoBody));

			await ProcessPingMessage();

			var echoCalled = _pingService.EchoCheckpoint != default;
			Assert.IsTrue(echoCalled);
			var pingCalled = _pingService.PingCheckpoint != default;
			Assert.IsFalse(pingCalled);
		}

		[TestMethod]
		[DataRow(true)]
		[DataRow(false)]
		public async Task FilterCanShortCircuitPipeline(bool callNext)
		{
			// Cross your fingers that order of registration is order of resolve
			var firstFilter = new TestFilter(GetCheckpoint);
			var shortCircuitFilter = new ShortCircuitFilter(callNext, GetCheckpoint);
			var cancelledFilter = new TestFilter(GetCheckpoint);
			RegisterFilters(firstFilter, shortCircuitFilter, cancelledFilter);

			await ProcessPingMessage();

			Assert.AreEqual(1, firstFilter.EntryCheckpoint);
			Assert.AreEqual(2, shortCircuitFilter.Checkpoint);
			Assert.AreEqual(default, cancelledFilter.EntryCheckpoint);
			Assert.AreEqual(default, _pingService.PingCheckpoint);
			Assert.AreEqual(default, cancelledFilter.ExitCheckpoint);
			Assert.AreEqual(3, firstFilter.ExitCheckpoint);
		}

		[TestMethod]
		public async Task ShortCircuitedFilterWithNoResultWontReturnMessageToClient()
		{
			var shortCircuitedFilter = new ShortCircuitNoResultFilter(GetCheckpoint);
			RegisterFilters(shortCircuitedFilter);

			var httpContext = await ProcessPingMessage();

			Assert.AreEqual(default, _pingService.PingCheckpoint);
			var response = httpContext.Response;
			Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
			Assert.AreEqual(0, response.Headers.Count);
			Assert.AreEqual(0, response.Body.Length);
		}

		[TestMethod]
		public async Task FilterCanThrowExceptionThatFlowsUpThroughPipeline()
		{
			var firstFilter = new TestFilter(GetCheckpoint);
			var throwingFilter = new ThrowingFilter();
			RegisterFilters(firstFilter, throwingFilter);

			var exceptionToFault = new Mock<IFaultExceptionTransformer>();
			exceptionToFault
				.Setup(x => x.ProvideFault(
					It.IsAny<InvalidOperationException>(),
					It.IsAny<MessageVersion>(),
					It.IsAny<XmlNamespaceManager>()))
				.Returns(
					Message.CreateMessage(MessageVersion.Soap11, "dummy", "body"))
				.Verifiable();
			_serviceCollection.AddSingleton(exceptionToFault.Object);

			await ProcessPingMessage();

			Assert.AreEqual(1, firstFilter.EntryCheckpoint);
			Assert.AreEqual(default, _pingService.PingCheckpoint);
			Assert.AreEqual(2, firstFilter.ExitCheckpoint);
			exceptionToFault.VerifyAll();
		}

		private void RegisterFilters(params ISoapMessageFilter[] filters)
		{
			foreach (var filter in filters)
			{
				_serviceCollection.AddSingleton(filter);
			}
		}

		private async Task<DefaultHttpContext> ProcessPingMessage()
        {
			const string pingMessage = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <Ping xmlns=""http://tempuri.org/"">
      <text>abc</text>
    </Ping>
  </s:Body>
</s:Envelope>";

			var features = new FeatureCollection();
			var httpContext = new DefaultHttpContext(features);

			features[typeof(IHttpRequestFeature)] = new HttpRequestFeature
			{
				Body = new MemoryStream(Encoding.UTF8.GetBytes(pingMessage)),
				Path = _servicePath,
				Headers = new HeaderDictionary
				{
					{ "Content-Type", "application/soap+xml" }
				}
			};
			features[typeof(IHttpResponseFeature)] = new HttpResponseFeature();
#if ASPNET_21 == false
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

		private int GetCheckpoint() => ++_checkpoint;

		[ServiceContract]
		private class PingService
		{
			private readonly Func<int> _getCheckpoint;

			public PingService(Func<int> getCheckpoint)
			{
				_getCheckpoint = getCheckpoint;
			}

			public int PingCheckpoint { get; private set; }
			public int EchoCheckpoint { get; private set; }

			[OperationContract]
			public string Ping(string text)
			{
				PingCheckpoint = _getCheckpoint();
				return text;
			}

			[OperationContract]
			public string Echo(string text)
			{
				EchoCheckpoint = _getCheckpoint();
				return text;
			}
		}

		private class TestFilter : ISoapMessageFilter
		{
			private readonly Func<int> _getCheckpoint;

			public TestFilter(Func<int> getCheckpoint)
			{
				_getCheckpoint = getCheckpoint;
			}

			public int EntryCheckpoint { get; private set; }

			public int ExitCheckpoint { get; private set; }

			public async Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
			{
				EntryCheckpoint = _getCheckpoint();
				try
				{
					await next();
				}
				finally
				{
					ExitCheckpoint = _getCheckpoint();
				}
			}
		}

		private class ChangeActionFilter : ISoapMessageFilter
		{
			private readonly string _messageBody;

			public ChangeActionFilter(string messageBody)
			{
				_messageBody = messageBody;
			}

			public async Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
			{
				var changedMessage = Message.CreateMessage(
					requestContext.Message.Version,
					"Not used, action is taken from HTTP header or body",
					new XmlTextReader(new StringReader(_messageBody)));
				requestContext.Message = changedMessage;
				await next();
			}
		}

		private class ShortCircuitFilter : ISoapMessageFilter
		{
			private readonly bool _callNext;
			private readonly Func<int> _getCheckpoint;

			public ShortCircuitFilter(bool callNext, Func<int> getCheckpoint)
			{
				_callNext = callNext;
				_getCheckpoint = getCheckpoint;
			}

			public int Checkpoint { get; private set; }

			public async Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
			{
				Checkpoint = _getCheckpoint();
				requestContext.Result = Message.CreateMessage(
					MessageVersion.Default,
					requestContext.Message.Headers.Action,
					"Response body");

				if (_callNext)
				{
					await next();
				}
			}
		}

		private class ShortCircuitNoResultFilter : ISoapMessageFilter
		{
			private readonly Func<int> _getCheckpoint;

			public ShortCircuitNoResultFilter(Func<int> getCheckpoint)
			{
				_getCheckpoint = getCheckpoint;
			}

			public int Checkpoint { get; private set; }

			public Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
			{
				Checkpoint = _getCheckpoint();
				return Task.CompletedTask;
			}
		}

		private class ThrowingFilter : ISoapMessageFilter
		{
			public Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
			{
				throw new InvalidOperationException();
			}
		}
	}
}
