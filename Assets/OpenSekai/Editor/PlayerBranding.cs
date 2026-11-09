// Adapted from OurTaikoPlay (https://github.com/OurTaiko/OurTaikoPlay, GPLv3), Assets/OurTaiko/Editor/PlayerBranding.cs.
// See NOTICE.md.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace OpenSekai.Editor
{
	/// <summary>
	/// The app icon on every platform, from one square picture (<see cref="IconPath"/>, 1024×1024, no transparency).
	/// Android's adaptive icon layers are made from it: the picture fills the middle 84 of the 108 dp background layer
	/// (launchers show the middle 72), the rest of that layer is the picture's average color, and the foreground is empty.
	/// </summary>
	public static class PlayerBranding
	{
		public const string IconPath = "Assets/OpenSekai/Branding/AppIcon.png";
		private const string BackgroundPath = "Assets/OpenSekai/Branding/AndroidBackground.png";
		private const string ForegroundPath = "Assets/OpenSekai/Branding/AndroidForeground.png";
		private const int LayerSize = 1024;
		private const float PictureShare = 84f / 108f;

		[MenuItem("OpenSekai/Build/Configure Icons", false, 1)]
		public static void Configure()
		{
			ImportIcon(IconPath);
			Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
			if (icon == null)
			{
				throw new BuildFailedException("Missing application icon: " + IconPath);
			}

			PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
			PlayerSettings.SetIcons(NamedBuildTarget.Standalone,
				PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any).Select(_ => icon).ToArray(), IconKind.Any);

			foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
			{
				PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
				foreach (PlatformIcon slot in slots)
				{
					slot.SetTexture(icon);
				}

				PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, slots);
			}

			EnsureAdaptiveLayers();
			Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
			Texture2D foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);
			foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
			{
				PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
				foreach (PlatformIcon slot in slots)
				{
					if (slot.minLayerCount == 2)
					{
						slot.SetTextures(background, foreground);
					}
					else
					{
						slot.SetTexture(icon);
					}
				}

				PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
			}

			AssetDatabase.SaveAssets();
		}

		// Uncompressed and without mipmaps, so every platform scales the icon from the full picture.
		private static void ImportIcon(string path)
		{
			AssetDatabase.ImportAsset(path);
			if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
			{
				throw new BuildFailedException("Missing application icon: " + path);
			}

			if (importer.textureType == TextureImporterType.Default && !importer.mipmapEnabled &&
			    importer.npotScale == TextureImporterNPOTScale.None && importer.maxTextureSize == 2048 &&
			    importer.textureCompression == TextureImporterCompression.Uncompressed)
			{
				return;
			}

			importer.textureType = TextureImporterType.Default;
			importer.mipmapEnabled = false;
			importer.npotScale = TextureImporterNPOTScale.None;
			importer.maxTextureSize = 2048;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SaveAndReimport();
		}

		private static void EnsureAdaptiveLayers()
		{
			Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			Texture2D background = new Texture2D(LayerSize, LayerSize, TextureFormat.RGB24, false);
			Texture2D foreground = new Texture2D(LayerSize, LayerSize, TextureFormat.RGBA32, false);
			try
			{
				source.LoadImage(File.ReadAllBytes(IconPath));
				Color32[] sourcePixels = source.GetPixels32();
				long r = 0, g = 0, b = 0;
				foreach (Color32 pixel in sourcePixels)
				{
					r += pixel.r;
					g += pixel.g;
					b += pixel.b;
				}

				int count = sourcePixels.Length;
				Color32 fill = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
				Color32[] pixels = Enumerable.Repeat(fill, LayerSize * LayerSize).ToArray();
				int size = Mathf.RoundToInt(LayerSize * PictureShare);
				int inset = (LayerSize - size) / 2;
				for (int y = 0; y < size; y++)
				{
					for (int x = 0; x < size; x++)
					{
						pixels[(y + inset) * LayerSize + x + inset] = source.GetPixelBilinear((x + .5f) / size, (y + .5f) / size);
					}
				}

				background.SetPixels32(pixels);
				background.Apply();
				foreground.SetPixels32(new Color32[LayerSize * LayerSize]);
				foreground.Apply();
				SavePng(BackgroundPath, background.EncodeToPNG());
				SavePng(ForegroundPath, foreground.EncodeToPNG());
			}
			finally
			{
				Object.DestroyImmediate(source);
				Object.DestroyImmediate(background);
				Object.DestroyImmediate(foreground);
			}

			ImportIcon(BackgroundPath);
			ImportIcon(ForegroundPath);
		}

		private static void SavePng(string path, byte[] bytes)
		{
			if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
			{
				File.WriteAllBytes(path, bytes);
			}
		}
	}
}
