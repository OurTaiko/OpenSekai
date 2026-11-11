using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenSekai.Editor
{
	/// <summary>
	/// Keeps the two dynamic TMP fonts empty on disk, the way OurTaikoPlay's UiFontAtlas does.
	/// </summary>
	/// <remarks>
	/// A dynamic font asset adds each glyph the first time a text needs it: the glyph's rectangle goes into the font
	/// asset's tables and its picture into the atlas texture. Here the atlas is an imported PNG, which Unity never
	/// writes back, while the tables are saved with the font asset. So after a save and an Editor restart, or whenever
	/// the PNG is imported again (a platform switch), the tables point at rectangles of a texture that no longer holds
	/// those glyphs, and the text is drawn from empty space: it disappears.
	///
	/// The tables and the atlas are therefore emptied together; every glyph is added again on demand. That happens
	/// when Play mode exits and before every build (saved), and when an atlas PNG is imported (in memory). Any other
	/// save of the font assets is dropped, so the committed assets stay empty and match the blank PNGs.
	/// </remarks>
	[InitializeOnLoad]
	public static class DynamicFontAtlas
	{
		private static readonly string[] FontPaths =
		{
			"Assets/OpenSekai/Resources/font/FOT-RodinNTLGPro-DB SDF_Dynamic.asset",
			"Assets/OpenSekai/Resources/font/FOT-RodinNTLGPro-EB SDF_Dynamic.asset",
		};

		private static bool regenerating;

		static DynamicFontAtlas()
		{
			EditorApplication.playModeStateChanged += state =>
			{
				if (state == PlayModeStateChange.EnteredEditMode)
				{
					Regenerate();
				}
			};
		}

		[MenuItem("OpenSekai/Regenerate Font Atlas")]
		public static void Regenerate()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				throw new InvalidOperationException("Exit Play mode before regenerating the font atlas.");
			}

			foreach (string path in FontPaths)
			{
				TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)
					?? throw new InvalidOperationException("The font " + path + " is missing.");
				font.ClearFontAssetData();
				EditorUtility.SetDirty(font);
				regenerating = true;
				try
				{
					AssetDatabase.SaveAssetIfDirty(font);
				}
				finally
				{
					regenerating = false;
				}
			}
		}

		private static bool IsAtlasTexture(string assetPath)
		{
			return FontPaths.Any(path =>
			{
				TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
				return font != null && font.atlasTextures != null && font.atlasTextures.Any(texture =>
					texture != null && AssetDatabase.GetAssetPath(texture) == assetPath);
			});
		}

		private sealed class SaveFilter : AssetModificationProcessor
		{
			private static string[] OnWillSaveAssets(string[] paths)
			{
				return regenerating ? paths : paths.Where(path => !FontPaths.Contains(path)).ToArray();
			}
		}

		private sealed class AtlasImport : AssetPostprocessor
		{
			private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
			{
				if (!imported.Any(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && IsAtlasTexture(path)))
				{
					return;
				}

				// The import put the PNG's blank pixels back; the tables still list the glyphs that were in it.
				// Nothing is saved here: the assets on disk are empty already.
				foreach (string path in FontPaths)
				{
					TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
					if (font != null)
					{
						font.ClearFontAssetData();
					}
				}
			}
		}

		private sealed class BeforeBuild : IPreprocessBuildWithReport
		{
			public int callbackOrder => 0;

			public void OnPreprocessBuild(BuildReport report)
			{
				Regenerate();
			}
		}
	}
}
