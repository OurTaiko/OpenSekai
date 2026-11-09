using System;
using Cysharp.Net.Http;
using UnityEngine;

namespace Sekai.CoopLive
{
	/// <summary>
	/// Gives <see cref="CoopTransport"/> its HTTP/2 handler, YetAnotherHttpHandler (Cysharp; native, works on
	/// desktop, Android and iOS). This assembly compiles only when the package
	/// <c>com.cysharp.yetanotherhttphandler</c> is installed (asmdef define constraint OPENSEKAI_YAHH).
	/// </summary>
	internal static class CoopHttp2Handler
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register()
		{
			CoopTransport.HandlerFactory = Create;
		}

		private static System.Net.Http.HttpMessageHandler Create(Uri address)
		{
			return new YetAnotherHttpHandler
			{
				// Plain http:// (a local server) is HTTP/2 without TLS; https:// negotiates h2 by ALPN.
				Http2Only = address.Scheme == Uri.UriSchemeHttp,
				Http2KeepAliveInterval = TimeSpan.FromSeconds(20),
				Http2KeepAliveTimeout = TimeSpan.FromSeconds(10),
				Http2KeepAliveWhileIdle = true,
			};
		}
	}
}
