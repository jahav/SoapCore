using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SoapCore.Extensibility;

namespace SoapCore.Tests.Extensibility
{
	/// <summary>
	/// Test that when an element in the pipeline short circuits by exception, the control flow
	/// through the pipeline works as expected.
	/// </summary>
	[TestClass]
	public partial class PipelineShortCircuitTests
	{
		private Func<int> _getCheckpoint;
		private Mock<IFaultExceptionTransformer> _faultTransformer;

		[TestInitialize]
		public void InitializePipeline()
		{
			Func<Func<int>> createCheckpoint = () =>
			{
				int counter = 0;
				int GetCheckpoint() => ++counter;
				return GetCheckpoint;
			};
			_getCheckpoint = createCheckpoint();
			_faultTransformer = new Mock<IFaultExceptionTransformer>();
			_faultTransformer
				.Setup(x => x.ProvideFault(It.IsAny<ApplicationException>(), It.IsAny<MessageVersion>(), It.IsAny<XmlNamespaceManager>()))
				.Returns(Message.CreateMessage(MessageVersion.Soap11, "dummy", "body"))
				.Verifiable();
		}

		[TestMethod]
		public async Task MessageFilterExceptionGoesBackToFaultTransformer()
		{
			var messageFilterFirst = new CheckpointFilter(_getCheckpoint);
			var messageFilterShort = ShortCircuitFilter.CreateException(_getCheckpoint);
			var sut = new PipelineSut<PingService>(new PingService(_getCheckpoint))
				.RegisterMessageFilter(messageFilterFirst)
				.RegisterMessageFilter(messageFilterShort)
				.RegisterFaultTransformer(_faultTransformer.Object);

			await sut.ProcessRequest("<Ping/>");

			Assert.AreEqual(1, messageFilterFirst.EntryCheckpoint);
			Assert.AreEqual(2, messageFilterShort.Checkpoint);
			Assert.AreEqual(3, messageFilterFirst.ExitCheckpoint);
			Assert.AreEqual(4, _getCheckpoint());
			_faultTransformer.Verify();
		}

		[TestMethod]
		public async Task ValueBinderExceptionGoesBackToFaultTransformer()
		{
			var messageFilter = new CheckpointFilter(_getCheckpoint);
			var valueBinder = CheckpointValueBinder.CreateThrowingException(_getCheckpoint);
			var sut = new PipelineSut<PingService>(new PingService(_getCheckpoint))
				.RegisterMessageFilter(messageFilter)
				.RegisterValueBinder(valueBinder)
				.RegisterFaultTransformer(_faultTransformer.Object);

			await sut.ProcessRequest(@"<Ping xmlns=""http://tempuri.org/""><text>a</text></Ping>");

			Assert.AreEqual(1, messageFilter.EntryCheckpoint);
			Assert.AreEqual(2, valueBinder.Checkpoint);
			Assert.AreEqual(3, messageFilter.ExitCheckpoint);
			Assert.AreEqual(4, _getCheckpoint());
			_faultTransformer.Verify();
		}

		[TestMethod]
		public async Task OperationFilterExceptionGoesBackToFaultTransformer()
		{
			var messageFilter = new CheckpointFilter(_getCheckpoint);
			var valueBinder = CheckpointValueBinder.CreatePassThrough(_getCheckpoint);
			var operationFilterFirst = new CheckpointFilter(_getCheckpoint);
			var operationFilterShort = ShortCircuitFilter.CreateException(_getCheckpoint);
			var sut = new PipelineSut<PingService>(new PingService(_getCheckpoint))
				.RegisterMessageFilter(messageFilter)
				.RegisterValueBinder(valueBinder)
				.RegisterOperationFilter(operationFilterFirst)
				.RegisterOperationFilter(operationFilterShort)
				.RegisterFaultTransformer(_faultTransformer.Object);

			await sut.ProcessRequest(@"<Ping xmlns=""http://tempuri.org/""><text>a</text></Ping>");

			Assert.AreEqual(1, messageFilter.EntryCheckpoint);
			Assert.AreEqual(2, valueBinder.Checkpoint);
			Assert.AreEqual(3, operationFilterFirst.EntryCheckpoint);
			Assert.AreEqual(4, operationFilterShort.Checkpoint);
			Assert.AreEqual(5, operationFilterFirst.ExitCheckpoint);
			Assert.AreEqual(6, messageFilter.ExitCheckpoint);
			Assert.AreEqual(7, _getCheckpoint());
			_faultTransformer.Verify();
		}

		[TestMethod]
		public async Task OperationExecutionExceptionGoesBackToFaultTransformer()
		{
			var messageFilter = new CheckpointFilter(_getCheckpoint);
			var valueBinder = CheckpointValueBinder.CreatePassThrough(_getCheckpoint);
			var operationFilter = new CheckpointFilter(_getCheckpoint);
			var service = new PingService(_getCheckpoint);
			var sut = new PipelineSut<PingService>(service)
				.RegisterMessageFilter(messageFilter)
				.RegisterValueBinder(valueBinder)
				.RegisterOperationFilter(operationFilter)
				.RegisterFaultTransformer(_faultTransformer.Object);

			await sut.ProcessRequest(@"<Error xmlns=""http://tempuri.org/""/>");

			Assert.AreEqual(1, messageFilter.EntryCheckpoint);
			Assert.AreEqual(2, valueBinder.Checkpoint);
			Assert.AreEqual(3, operationFilter.EntryCheckpoint);
			Assert.AreEqual(4, service.Checkpoint);
			Assert.AreEqual(5, operationFilter.ExitCheckpoint);
			Assert.AreEqual(6, messageFilter.ExitCheckpoint);
			Assert.AreEqual(7, _getCheckpoint());
			_faultTransformer.Verify();
		}

		[ServiceContract]
		private class PingService
		{
			private readonly Func<int> _getCheckpoint;

			public PingService(Func<int> getCheckpoint) => _getCheckpoint = getCheckpoint;

			public int Checkpoint { get; private set; }

			[OperationContract]
			public void Ping(string text) => Checkpoint = _getCheckpoint();

			[OperationContract]
			public void Error(string text)
			{
				Checkpoint = _getCheckpoint();
				throw new ApplicationException();
			}
		}
	}
}
