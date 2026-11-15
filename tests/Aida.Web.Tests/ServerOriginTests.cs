using System.Net;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Aida.Web.Tests;

public sealed class ServerOriginTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("::ffff:127.0.0.1", true)]
    [InlineData("192.168.1.20", false)]
    [InlineData("::ffff:10.0.0.5", false)]
    [InlineData(null, false)]
    public void Solo_loopback_es_el_propio_servidor(string? ipString, bool expected)
    {
        var context = new DefaultHttpContext();
        if (ipString is not null)
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(ipString);
        }

        var result = ServerOrigin.IsServerItself(context);
        Assert.Equal(expected, result);
    }
}
