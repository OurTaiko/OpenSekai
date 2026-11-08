// Ported from OurTaikoPlay (https://github.com/OurTaiko/OurTaikoPlay, GPLv3),
// Assets/OurTaiko/Editor/AudioBuildSettings.cs. See NOTICE.md.
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace OpenSekai.Editor
{
	/// <summary>
	/// Keeps the BASS native plugin import settings and the iOS static-linking define correct before every player build.
	/// </summary>
	public sealed class AudioBuildSettings : IPreprocessBuildWithReport
	{
		private const string BassPluginRoot = "Assets/Plugins/Un4seen.Bass/";

		public int callbackOrder => 0;

		public void OnPreprocessBuild(BuildReport report)
		{
			if (report.summary.platform == BuildTarget.StandaloneWindows)
			{
				throw new BuildFailedException("The supplied BASS Windows libraries are x64. Select Windows x86_64.");
			}

			Configure();
		}

		[MenuItem("OpenSekai/Audio/Reapply BASS Plugin Settings")]
		public static void Configure()
		{
			var iosTarget = NamedBuildTarget.iOS;
			var symbols = PlayerSettings.GetScriptingDefineSymbols(iosTarget);
			if (!Array.Exists(symbols.Split(';'), symbol => symbol == "__STATIC_LINKING__"))
			{
				PlayerSettings.SetScriptingDefineSymbols(iosTarget, string.IsNullOrEmpty(symbols) ? "__STATIC_LINKING__" : symbols + ";__STATIC_LINKING__");
			}

			foreach (var path in AssetDatabase.GetAllAssetPaths())
			{
				if (!path.StartsWith(BassPluginRoot, StringComparison.Ordinal))
				{
					continue;
				}

				if (!(AssetImporter.GetAtPath(path) is PluginImporter plugin))
				{
					continue;
				}

				// Nested files in xcframeworks are handled by the framework importer.
				if (path.Contains(".xcframework/"))
				{
					continue;
				}

				plugin.SetCompatibleWithAnyPlatform(false);
				plugin.SetCompatibleWithEditor(false);
				foreach (var target in new[]
				         {
					         BuildTarget.StandaloneOSX, BuildTarget.StandaloneLinux64, BuildTarget.StandaloneWindows,
					         BuildTarget.StandaloneWindows64, BuildTarget.Android, BuildTarget.iOS, BuildTarget.WebGL
				         })
				{
					plugin.SetCompatibleWithPlatform(target, false);
				}

				if (path.Contains("/macOS/"))
				{
					plugin.SetCompatibleWithEditor(true);
					plugin.SetEditorData("OS", "OSX");
					plugin.SetEditorData("CPU", "AnyCPU");
					plugin.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
					plugin.SetPlatformData(BuildTarget.StandaloneOSX, "CPU", "AnyCPU");
				}
				else if (path.Contains("/Windows/"))
				{
					plugin.SetCompatibleWithEditor(true);
					plugin.SetEditorData("OS", "Windows");
					plugin.SetEditorData("CPU", "x86_64");
					plugin.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
					plugin.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");
				}
				else if (path.Contains("/Linux/"))
				{
					plugin.SetCompatibleWithEditor(true);
					plugin.SetEditorData("OS", "Linux");
					plugin.SetEditorData("CPU", "x86_64");
					plugin.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, true);
					plugin.SetPlatformData(BuildTarget.StandaloneLinux64, "CPU", "x86_64");
				}
				else if (path.Contains("/Android/"))
				{
					plugin.SetCompatibleWithPlatform(BuildTarget.Android, true);
					var arch = path.Contains("/arm64-v8a/") ? "ARM64"
						: path.Contains("/armeabi-v7a/") ? "ARMv7"
						: path.Contains("/x86_64/") ? "x86_64"
						: "x86";
					plugin.SetPlatformData(BuildTarget.Android, "CPU", arch);
				}
				else if (path.EndsWith(".xcframework", StringComparison.Ordinal))
				{
					plugin.SetCompatibleWithPlatform(BuildTarget.iOS, true);
					plugin.SetPlatformData(BuildTarget.iOS, "AddToEmbeddedBinaries", "true");
				}

				plugin.SaveAndReimport();
			}
		}

#if UNITY_IOS
		[UnityEditor.Callbacks.PostProcessBuild(45)]
		private static void ConfigureIos(BuildTarget target, string path)
		{
			if (target != BuildTarget.iOS)
			{
				return;
			}

			var project = new UnityEditor.iOS.Xcode.PBXProject();
			var file = UnityEditor.iOS.Xcode.PBXProject.GetPBXProjectPath(path);
			project.ReadFromFile(file);
			foreach (var id in new[] { project.GetUnityMainTargetGuid(), project.GetUnityFrameworkTargetGuid() })
			{
				project.AddBuildProperty(id, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
			}

			// A .tbd is a system library, not a System/Library/Frameworks bundle.
			var cpp = project.AddFile("usr/lib/libc++.tbd", "Frameworks/libc++.tbd", UnityEditor.iOS.Xcode.PBXSourceTree.Sdk);
			project.AddFileToBuild(project.GetUnityFrameworkTargetGuid(), cpp);
			project.WriteToFile(file);
		}
#endif
	}
}
