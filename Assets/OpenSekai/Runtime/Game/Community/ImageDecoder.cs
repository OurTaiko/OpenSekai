using System;
using unity.libwebp;
using unity.libwebp.Interop;
using UnityEngine;

namespace Sekai.Community
{
	/// <summary>
	/// Turns downloaded image bytes into a texture: PNG and JPEG with <see cref="Texture2D.LoadImage(byte[], bool)"/>,
	/// WebP (SSO avatars are WebP) with libwebp from the unity.webp package. An animated WebP gives its first frame.
	/// </summary>
	public static class ImageDecoder
	{
		/// <summary>The image as a non-readable RGBA texture, or null if it is not PNG, JPEG or WebP or does not decode.</summary>
		public static Texture2D Decode(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return null;
			}
			if (IsWebP(bytes))
			{
				return DecodeWebP(bytes);
			}
			Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
			if (texture.LoadImage(bytes, true))
			{
				return texture;
			}
			UnityEngine.Object.Destroy(texture);
			return null;
		}

		/// <summary>A RIFF container of type WEBP.</summary>
		public static bool IsWebP(byte[] bytes) =>
			bytes != null && bytes.Length >= 12
			&& bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
			&& bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P';

		private static unsafe Texture2D DecodeWebP(byte[] bytes)
		{
			try
			{
				WebPBitstreamFeatures features;
				fixed (byte* data = bytes)
				{
					if (NativeLibwebp.WebPGetFeatures(data, (UIntPtr)bytes.Length, &features) != VP8StatusCode.VP8_STATUS_OK)
					{
						return null;
					}
				}
				if (features.width <= 0 || features.height <= 0)
				{
					return null;
				}
				byte[] pixels = features.has_animation != 0 ? FirstFrame(bytes, out int width, out int height) : Still(bytes, out width, out height);
				if (pixels == null)
				{
					return null;
				}
				Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
				texture.LoadRawTextureData(pixels);
				texture.Apply(false, true);
				return texture;
			}
			catch (Exception exception)
			{
				// Texture2DExt throws on a broken file; a missing native library is a DllNotFoundException.
				Debug.LogWarning("[Community] WebP failed to decode: " + exception.Message);
				return null;
			}
		}

		/// <summary>A still WebP as RGBA rows from the bottom up, as Unity textures store them.</summary>
		private static byte[] Still(byte[] bytes, out int width, out int height)
		{
			WebP.Texture2DExt.GetWebPDimensions(bytes, out width, out height);
			byte[] pixels = WebP.Texture2DExt.LoadRGBAFromWebP(bytes, ref width, ref height, false, out WebP.Error error);
			return error == WebP.Error.Success ? pixels : null;
		}

		/// <summary>The first frame of an animated WebP, flipped to bottom-up rows.</summary>
		private static unsafe byte[] FirstFrame(byte[] bytes, out int width, out int height)
		{
			width = 0;
			height = 0;
			WebPAnimDecoderOptions options;
			if (NativeLibwebpdemux.WebPAnimDecoderOptionsInit(&options) == 0)
			{
				return null;
			}
			options.color_mode = WEBP_CSP_MODE.MODE_RGBA;
			options.use_threads = 0;
			fixed (byte* data = bytes)
			{
				WebPData webpData = new WebPData { bytes = data, size = (UIntPtr)bytes.Length };
				WebPAnimDecoder* decoder = NativeLibwebpdemux.WebPAnimDecoderNew(&webpData, &options);
				if (decoder == null)
				{
					return null;
				}
				try
				{
					WebPAnimInfo info;
					byte* frame = null;
					int timestamp = 0;
					if (NativeLibwebpdemux.WebPAnimDecoderGetInfo(decoder, &info) == 0
						|| NativeLibwebpdemux.WebPAnimDecoderGetNext(decoder, &frame, &timestamp) == 0
						|| frame == null)
					{
						return null;
					}
					width = (int)info.canvas_width;
					height = (int)info.canvas_height;
					int stride = width * 4;
					byte[] pixels = new byte[stride * height];
					fixed (byte* output = pixels)
					{
						for (int y = 0; y < height; y++)
						{
							Buffer.MemoryCopy(frame + (long)y * stride, output + (long)(height - 1 - y) * stride, stride, stride);
						}
					}
					return pixels;
				}
				finally
				{
					NativeLibwebpdemux.WebPAnimDecoderDelete(decoder);
				}
			}
		}
	}
}
