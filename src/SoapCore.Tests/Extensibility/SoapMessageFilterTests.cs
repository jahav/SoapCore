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
	public class SoapMessageFilterTests
	{
		private const string _servicePath = "/Service.svc";
		private const string _pingBody = @"<Ping xmlns=""http://tempuri.org/""><text>abc</text></Ping>";
		private const string _echoBody = @"<Echo xmlns=""http://tempuri.org/""><text>abc</text></Echo>";

		private IServiceCollection _serviceCollection;
		private PingService _pingService;
		private PipelineSut<PingService> _sut;
		private Func<int> _getCheckpoint;

		[TestInitialize]
		public void Reset()
		{
			Func<Func<int>> createCheckpoint = () =>
			{
				int counter = 0;
				int GetCheckpoint() => ++counter;
				return GetCheckpoint;
			};
			_getCheckpoint = createCheckpoint();

			_pingService = new PingService(_getCheckpoint);
			_sut = new PipelineSut<PingService>(_pingService);

			_serviceCollection = new ServiceCollection();

			_serviceCollection.AddSingleton(_pingService);
		}

		[TestMethod]
		public async Task FiltersAreCalledInPipelineSequence()
		{
			var filters = new[]
			{
				new CheckpointFilter(_getCheckpoint),
				new CheckpointFilter(_getCheckpoint),
				new CheckpointFilter(_getCheckpoint)
			};

			foreach (var filter in filters)
			{
				_sut.RegisterMessageFilter(filter);
			}

			await _sut.ProcessRequest(_pingBody);

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
			_sut.RegisterMessageFilter(new ChangeActionFilter(_echoBody));

			await _sut.ProcessRequest(_pingBody);

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
			var firstFilter = new CheckpointFilter(_getCheckpoint);
			var shortCircuitFilter = ShortCircuitFilter.CreateWithResult(_getCheckpoint, callNext);
			var cancelledFilter = new CheckpointFilter(_getCheckpoint);

			_sut.RegisterMessageFilter(firstFilter)
				.RegisterMessageFilter(shortCircuitFilter)
				.RegisterMessageFilter(cancelledFilter);

			await _sut.ProcessRequest(_pingBody);

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
			var shortCircuitedFilter = ShortCircuitFilter.CreateWithNoResultNoNext(_getCheckpoint);
			_sut.RegisterMessageFilter(shortCircuitedFilter);

			var httpContext = await _sut.ProcessRequest(_pingBody);

			Assert.AreEqual(default, _pingService.PingCheckpoint);
			var response = httpContext.Response;
			Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
			Assert.AreEqual(0, response.Headers.Count);
			Assert.AreEqual(0, response.Body.Length);
		}

		[TestMethod]
		public async Task FilterCanThrowExceptionThatFlowsUpThroughPipeline()
		{
			var firstFilter = new CheckpointFilter(_getCheckpoint);
			var throwingFilter = ShortCircuitFilter.CreateException(_getCheckpoint);
			_sut.RegisterMessageFilter(firstFilter)
				.RegisterMessageFilter(throwingFilter);

			var exceptionToFault = new Mock<IFaultExceptionTransformer>();
			exceptionToFault
				.Setup(x => x.ProvideFault(
					It.IsAny<ApplicationException>(),
					It.IsAny<MessageVersion>(),
					It.IsAny<XmlNamespaceManager>()))
				.Returns(
					Message.CreateMessage(MessageVersion.Soap11, "dummy", "body"))
				.Verifiable();
			_sut.RegisterFaultTransformer(exceptionToFault.Object);

			await _sut.ProcessRequest(_pingBody);

			Assert.AreEqual(1, firstFilter.EntryCheckpoint);
			Assert.AreEqual(2, throwingFilter.Checkpoint);
			Assert.AreEqual(default, _pingService.PingCheckpoint);
			Assert.AreEqual(3, firstFilter.ExitCheckpoint);
			exceptionToFault.VerifyAll();
		}

		[ServiceContract]
		private class PingService
		{
			private readonly Func<int> _getCheckpoint;
			public PingService(Func<int> getCheckpoint) => _getCheckpoint = getCheckpoint;

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
	}
}
