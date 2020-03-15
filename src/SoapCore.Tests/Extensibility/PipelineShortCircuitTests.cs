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
	/// Test that when an element in the pipeline short circuits (by any means), the control flow
	/// through the pipeline works as expected.
	/// </summary>
	[TestClass]
	public partial class PipelineShortCircuitTests
	{
		private Func<int> _getCheckpoint;

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
		}

		[TestMethod]
		public async Task MessageFilterShortGoesBackThroughMessageFilter()
		{
			var sut = new ServiceSut<PingService>(new PingService(_getCheckpoint));
			var messageFilterFirst = new CheckpointMessageFilter(_getCheckpoint);
			var messageFilterShort = new ShortCircuitMessageFilter<ApplicationException>(_getCheckpoint, true);
			var messageFilterLast = new CheckpointMessageFilter(_getCheckpoint);
			var faultTransformer = new Mock<IFaultExceptionTransformer>();
			faultTransformer
				.Setup(x => x.ProvideFault(It.IsAny<ApplicationException>(), It.IsAny<MessageVersion>(), It.IsAny<XmlNamespaceManager>()))
				.Returns(Message.CreateMessage(MessageVersion.Soap11, "dummy", "body"))
				.Verifiable();
			sut
				.RegisterFilter(messageFilterFirst)
				.RegisterFilter(messageFilterShort)
				.RegisterFilter(messageFilterLast)
				.RegisterFaultTransformer(faultTransformer.Object);

			await sut.ProcessRequest("<Ping/>");

			Assert.AreEqual(1, messageFilterFirst.EntryCheckpoint);
			Assert.AreEqual(2, messageFilterShort.Checkpoint);
			Assert.AreEqual(3, messageFilterFirst.ExitCheckpoint);
			faultTransformer.Verify();

			Assert.AreEqual(default, messageFilterLast.EntryCheckpoint);
			Assert.AreEqual(default, messageFilterLast.ExitCheckpoint);
		}

		[ServiceContract]
		private class PingService
		{
			private readonly Func<int> _getCheckpoint;

			public PingService(Func<int> getCheckpoint) => _getCheckpoint = getCheckpoint;

			public int Checkpoint { get; private set; }

			[OperationContract]
			public void Ping() => Checkpoint = _getCheckpoint();
		}
	}
}
