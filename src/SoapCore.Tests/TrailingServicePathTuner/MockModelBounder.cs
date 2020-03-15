using System.Reflection;
using SoapCore.Extensibility;

#pragma warning disable CS0618 // Type or member is obsolete. Interface is obsolete, test ensures that adapter is working correctly.
namespace SoapCore.Tests
{
	internal class MockModelBounder : ISoapModelBounder
	{
		public void OnModelBound(MethodInfo methodInfo, object[] prms)
		{
			return;
		}
	}
}
