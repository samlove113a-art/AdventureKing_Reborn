using AdventureKing.Gameplay;
using NUnit.Framework;

namespace AdventureKing.Tests
{
    /// <summary>Which server URLs a client accepts: HTTPS, loopback, and (development builds only) plain HTTP to the LAN or Tailscale.</summary>
    public sealed class ServerUrlPolicyTests
    {
        [TestCase("http://100.91.134.127:5080/api/v1")]   // the project's Tailscale server
        [TestCase("http://100.64.0.0:5080/api/v1")]       // first address of 100.64.0.0/10
        [TestCase("http://100.127.255.255:5080/api/v1")]  // last address
        [TestCase("http://100.100.100.100/api/v1")]
        public void Tailscale_range_is_accepted_in_development_builds(string url) => Assert.IsTrue(AdventureServerConnection.IsAllowedUrl(url, true));

        [TestCase("http://100.63.255.255:5080/api/v1")]   // just below the range
        [TestCase("http://100.128.0.0:5080/api/v1")]      // just above it
        [TestCase("http://100.0.0.1:5080/api/v1")]
        [TestCase("http://101.91.134.127:5080/api/v1")]
        [TestCase("http://8.8.8.8:5080/api/v1")]
        [TestCase("http://100.91.134.256:5080/api/v1")]   // octet out of range
        [TestCase("http://100.91.134:5080/api/v1")]       // three parts
        [TestCase("http://100.91.134.127.5:5080/api/v1")] // five parts
        [TestCase("http://100.91.+34.127:5080/api/v1")]   // sign
        [TestCase("http://100.91.134.127@evil.example/api/v1")] // user-info trick
        [TestCase("http://100.91.134.127.evil.example/api/v1")]
        [TestCase("ftp://100.91.134.127/api/v1")]
        public void Anything_outside_the_tailscale_range_is_refused(string url) => Assert.IsFalse(AdventureServerConnection.IsAllowedUrl(url, true));

        [Test]
        public void Release_builds_still_refuse_plain_http_to_tailscale_and_lan()
        {
            Assert.IsFalse(AdventureServerConnection.IsAllowedUrl("http://100.91.134.127:5080/api/v1", false));
            Assert.IsFalse(AdventureServerConnection.IsAllowedUrl("http://192.168.1.46:5080/api/v1", false));
            Assert.IsTrue(AdventureServerConnection.IsAllowedUrl("https://game.example.com/api/v1", false));
        }

        [Test]
        public void Localhost_and_lan_keep_working()
        {
            Assert.IsTrue(AdventureServerConnection.IsAllowedUrl("http://127.0.0.1:5080/api/v1", false));
            Assert.IsTrue(AdventureServerConnection.IsAllowedUrl("http://localhost:5080/api/v1", false));
            foreach (string u in new[] { "http://192.168.1.46:5080/x", "http://10.0.0.5:5080/x", "http://172.16.3.4:5080/x", "http://172.31.255.1:5080/x" })
                Assert.IsTrue(AdventureServerConnection.IsAllowedUrl(u, true), u);
            Assert.IsFalse(AdventureServerConnection.IsAllowedUrl("http://172.32.0.1:5080/x", true));
            Assert.IsFalse(AdventureServerConnection.IsAllowedUrl("", true));
        }
    }
}
