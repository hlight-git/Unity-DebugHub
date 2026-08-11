using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class NetworkReachabilityAuthenticationBypassTests
    {
        [Test]
        public void Check_WithNoUrls_ReportsFalse_WithoutNetworkCall()
        {
            var bypass = new NetworkReachabilityAuthenticationBypass();
            bool? result = null;

            var enumerator = bypass.Check(value => result = value);
            enumerator.MoveNext();

            Assert.AreEqual(false, result);
        }
    }
}
