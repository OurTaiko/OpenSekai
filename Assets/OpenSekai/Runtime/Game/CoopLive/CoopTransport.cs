using System;
using System.Net.Http;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Sekai.CoopLive.Proto;

namespace Sekai.CoopLive
{
	/// <summary>
	/// The gRPC side of 協力ライブ: the channel to SekaiFanmade's co-op server and the one bidirectional call,
	/// <c>sekai.multiplay.v1.MultiLive/Connect</c> (proto in SekaiFanmade, <c>proto/sekai/multiplay/v1</c>).
	/// gRPC needs HTTP/2, which Unity's own HttpClient does not speak; <see cref="HandlerFactory"/> is set by the
	/// <c>OpenSekai.CoopLive.Http2</c> assembly, which compiles only when YetAnotherHttpHandler is installed.
	/// </summary>
	public static class CoopTransport
	{
		/// <summary>Makes an HTTP/2 handler for a server address (http:// is h2c, https:// negotiates h2).</summary>
		public static Func<Uri, HttpMessageHandler> HandlerFactory { get; set; }

		public static bool HasHttp2 => HandlerFactory != null;

		private static readonly Marshaller<ClientMessage> ClientMarshaller =
			Marshallers.Create(message => message.ToByteArray(), ClientMessage.Parser.ParseFrom);

		private static readonly Marshaller<ServerMessage> ServerMarshaller =
			Marshallers.Create(message => message.ToByteArray(), ServerMessage.Parser.ParseFrom);

		private static readonly Method<ClientMessage, ServerMessage> ConnectMethod = new Method<ClientMessage, ServerMessage>(
			MethodType.DuplexStreaming, "sekai.multiplay.v1.MultiLive", "Connect", ClientMarshaller, ServerMarshaller);

		public static GrpcChannel CreateChannel(string address)
		{
			if (HandlerFactory == null)
			{
				throw new CoopException("NO_HTTP2", L10n.Get("CoopLive", "error.noHttp2"));
			}
			Uri uri = new Uri(address);
			return GrpcChannel.ForAddress(uri, new GrpcChannelOptions
			{
				HttpHandler = HandlerFactory(uri),
				DisposeHttpClient = true,
				MaxReceiveMessageSize = 1024 * 1024,
				MaxSendMessageSize = 64 * 1024,
			});
		}

		public static AsyncDuplexStreamingCall<ClientMessage, ServerMessage> Connect(GrpcChannel channel, string sessionToken, System.Threading.CancellationToken ct)
		{
			Metadata headers = new Metadata { { "authorization", "Bearer " + sessionToken } };
			return channel.CreateCallInvoker().AsyncDuplexStreamingCall(ConnectMethod, null, new CallOptions(headers, cancellationToken: ct));
		}
	}

	/// <summary>Something the player should be told about the co-op connection.</summary>
	public sealed class CoopException : Exception
	{
		public CoopException(string code, string message) : base(message)
		{
			Code = code;
		}

		public string Code { get; }
	}
}
