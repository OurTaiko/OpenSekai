// Ported from OurTaikoPlay (https://github.com/OurTaiko/OurTaikoPlay, GPLv3),
// Assets/OurTaiko/Editor/PlayerBuilds.cs. See NOTICE.md.
using System;
using System.IO;
using System.Linq;
using Sekai.EditorTools;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenSekai.Editor
{
	/// <summary>
	/// One place that builds the player for every platform OpenSekai ships on.
	/// Menu builds queue their targets and switch the active build target between them; batch
	/// builds (CI) pass <c>-buildTarget</c> and call one of the <c>Build*</c> methods with
	/// <c>-executeMethod</c>. Every build first rebuilds the AssetBundles for its target.
	/// </summary>
	[InitializeOnLoad]
	public static class PlayerBuilds
	{
		public const string ApplicationIdentifier = "com.sparkbarry.sekai";
		public const string BuildName = "OpenSekai";

		private const string PendingKey = "OpenSekai.PlayerBuilds.Pending";
		private const string MenuRoot = "OpenSekai/Build/";

		private static readonly double LoadedAt;
		private static bool _building;

		[Serializable]
		private sealed class QueueState
		{
			public BuildTarget[] targets;
			public int next;
		}

		[Serializable]
		private sealed class Result
		{
			public string platform, status, output, timestamp, message;
			public int errors, warnings;
			public long bytes;
			public double seconds;
		}

		static PlayerBuilds()
		{
			LoadedAt = EditorApplication.timeSinceStartup;
			EditorApplication.update += ResumeQueue;
		}

		/// <summary>Player settings that differ per platform. Applied before every build.</summary>
		[MenuItem(MenuRoot + "Configure Platforms", false, 0)]
		public static void ConfigurePlatforms()
		{
			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, ApplicationIdentifier);
			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationIdentifier);
			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationIdentifier);

			// Mono on desktop lets one host (or a Linux CI runner) build Windows, macOS and Linux;
			// IL2CPP is required on iOS and needed for ARM64 on Android.
			PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
			PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
			PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
			PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
			PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);

			PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
			PlayerSettings.Android.forceInternetPermission = true;
			PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;

			PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
			PlayerSettings.allowedAutorotateToPortrait = false;
			PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
			PlayerSettings.allowedAutorotateToLandscapeLeft = true;
			PlayerSettings.allowedAutorotateToLandscapeRight = true;

			// BASS import settings and the iOS __STATIC_LINKING__ define.
			AudioBuildSettings.Configure();
			AssetDatabase.SaveAssets();
		}

		[MenuItem(MenuRoot + "All Platforms", false, 20)]
		public static void QueueAll() => Enqueue(BuildTarget.StandaloneWindows64, BuildTarget.StandaloneOSX,
			BuildTarget.StandaloneLinux64, BuildTarget.Android, BuildTarget.iOS);

		[MenuItem(MenuRoot + "Windows x64", false, 40)]
		public static void QueueWindows() => Enqueue(BuildTarget.StandaloneWindows64);

		[MenuItem(MenuRoot + "macOS Universal", false, 41)]
		public static void QueueMacOS() => Enqueue(BuildTarget.StandaloneOSX);

		[MenuItem(MenuRoot + "Linux x64", false, 42)]
		public static void QueueLinux() => Enqueue(BuildTarget.StandaloneLinux64);

		[MenuItem(MenuRoot + "Android APK", false, 43)]
		public static void QueueAndroid() => Enqueue(BuildTarget.Android);

		[MenuItem(MenuRoot + "iOS Xcode Project", false, 44)]
		public static void QueueIOS() => Enqueue(BuildTarget.iOS);

		// CI entry points (-executeMethod OpenSekai.Editor.PlayerBuilds.BuildWindows ...).
		public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64);
		public static void BuildMacOS() => Build(BuildTarget.StandaloneOSX);
		public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64);
		public static void BuildAndroid() => Build(BuildTarget.Android);
		public static void BuildIOS() => Build(BuildTarget.iOS);

		public static string OutputPath(BuildTarget target)
		{
			switch (target)
			{
				case BuildTarget.StandaloneWindows64: return "Builds/Windows/" + BuildName + ".exe";
				case BuildTarget.StandaloneOSX: return "Builds/macOS/" + BuildName + ".app";
				case BuildTarget.StandaloneLinux64: return "Builds/Linux/" + BuildName + ".x86_64";
				case BuildTarget.Android: return "Builds/Android/" + BuildName + ".apk";
				case BuildTarget.iOS: return "Builds/iOS";
				default: throw new ArgumentOutOfRangeException(nameof(target), target, "OpenSekai does not build for this target.");
			}
		}

		private static void Enqueue(params BuildTarget[] targets)
		{
			if (_building || SessionState.GetString(PendingKey, string.Empty) != string.Empty)
			{
				throw new InvalidOperationException("An OpenSekai build is already queued.");
			}

			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				throw new InvalidOperationException("Exit Play mode before building.");
			}

			// Switching platforms reloads the scenes; never drop or silently save authored edits.
			if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			foreach (BuildTarget target in targets)
			{
				CheckModule(target);
			}

			ConfigurePlatforms();
			SessionState.SetString(PendingKey, JsonUtility.ToJson(new QueueState { targets = targets }));
			WriteStatus(new Result { status = "Queued", platform = targets[0].ToString() });
		}

		// Runs from EditorApplication.update so that it survives the domain reload of a platform switch.
		private static void ResumeQueue()
		{
			if (_building || EditorApplication.isCompiling || EditorApplication.isUpdating ||
			    EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup - LoadedAt < 2)
			{
				return;
			}

			string pending = SessionState.GetString(PendingKey, string.Empty);
			if (pending == string.Empty)
			{
				return;
			}

			QueueState queue = JsonUtility.FromJson<QueueState>(pending);
			try
			{
				if (EditorUtility.scriptCompilationFailed)
				{
					throw new BuildFailedException("Scripts failed to compile for the selected platform.");
				}

				BuildTarget target = queue.targets[queue.next];
				if (EditorUserBuildSettings.activeBuildTarget != target)
				{
					WriteStatus(new Result { status = "Switching", platform = target.ToString() });
					if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildPipeline.GetBuildTargetGroup(target), target))
					{
						throw new BuildFailedException("Could not switch to " + target);
					}

					return;
				}

				_building = true;
				Build(target);
				queue.next++;
				SessionState.SetString(PendingKey, queue.next == queue.targets.Length ? string.Empty : JsonUtility.ToJson(queue));
			}
			catch (Exception error)
			{
				SessionState.EraseString(PendingKey);
				WriteStatus(new Result { status = "Failed", platform = queue.targets[queue.next].ToString(), message = error.ToString() });
				Debug.LogException(error);
			}
			finally
			{
				_building = false;
			}
		}

		private static void CheckModule(BuildTarget target)
		{
			if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
			{
				throw new BuildFailedException("Install the Unity " + Application.unityVersion + " build module for " + target + ".");
			}
		}

		private static void Build(BuildTarget target)
		{
			CheckModule(target);
			if (EditorUserBuildSettings.activeBuildTarget != target)
			{
				throw new BuildFailedException("Select " + target + " first. In batch mode pass -buildTarget; in the Editor use " + MenuRoot + ".");
			}

			ConfigurePlatforms();
			ApplyCommandLine(target);
#if UNITY_STANDALONE_OSX
			// This API lives in the macOS build module; other Editor hosts need not install it.
			if (target == BuildTarget.StandaloneOSX)
			{
				UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.x64ARM64;
			}
#endif
			if (target == BuildTarget.Android)
			{
				EditorUserBuildSettings.buildAppBundle = false;
				EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
			}

			string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
			if (scenes.Length == 0)
			{
				throw new BuildFailedException("No scenes are enabled in Build Settings.");
			}

			// BuildPipeline.BuildPlayer skips the Build window handler, so pack the bundles here.
			OpenSekaiAssetBundleBuildPipeline.BuildForTarget(target, false);

			string output = Path.GetFullPath(Argument("-customBuildPath") ?? OutputPath(target));
			Directory.CreateDirectory(Path.GetDirectoryName(output));
			WriteStatus(new Result { status = "Building", platform = target.ToString(), output = output });
			UnityEngine.Object[] preloaded = PlayerSettings.GetPreloadedAssets();
			try
			{
				BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
				{
					scenes = scenes,
					target = target,
					locationPathName = output,
					options = BuildOptions.CompressWithLz4HC,
				});
				BuildSummary summary = report.summary;
				Result result = new Result
				{
					platform = target.ToString(),
					status = summary.result.ToString(),
					output = output,
					errors = summary.totalErrors,
					warnings = summary.totalWarnings,
					bytes = (long)summary.totalSize,
					seconds = summary.totalTime.TotalSeconds,
					message = string.Join("\n", report.steps.SelectMany(step => step.messages)
						.Where(message => message.type == LogType.Error || message.type == LogType.Exception)
						.Select(message => message.content)),
				};
				WriteStatus(result);
				Directory.CreateDirectory("Builds/Reports");
				File.WriteAllText("Builds/Reports/" + target + ".json", JsonUtility.ToJson(result, true));
				if (summary.result != BuildResult.Succeeded)
				{
					throw new BuildFailedException(result.message);
				}

				Debug.Log("OpenSekai " + target + " build: " + output);
			}
			finally
			{
				// The Input System's build hook adds its settings to this list for the build only.
				PlayerSettings.SetPreloadedAssets(preloaded);
				AssetDatabase.SaveAssets();
			}
		}

		// CI (game-ci unity-builder) passes the version and signing values; menu builds keep Player Settings.
		private static void ApplyCommandLine(BuildTarget target)
		{
			string version = Argument("-buildVersion");
			if (version != null && version != "none")
			{
				PlayerSettings.bundleVersion = version;
			}

			if (target != BuildTarget.Android)
			{
				return;
			}

			if (int.TryParse(Argument("-androidVersionCode"), out int code) && code > 0)
			{
				PlayerSettings.Android.bundleVersionCode = code;
			}

			string keystore = Argument("-androidKeystoreName");
			if (keystore != null && File.Exists(keystore))
			{
				PlayerSettings.Android.useCustomKeystore = true;
				PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
				PlayerSettings.Android.keystorePass = Argument("-androidKeystorePass") ?? string.Empty;
				PlayerSettings.Android.keyaliasName = Argument("-androidKeyaliasName") ?? string.Empty;
				PlayerSettings.Android.keyaliasPass = Argument("-androidKeyaliasPass") ?? string.Empty;
			}
		}

		private static string Argument(string name)
		{
			string[] args = Environment.GetCommandLineArgs();
			int index = Array.IndexOf(args, name);
			return index >= 0 && index + 1 < args.Length && args[index + 1] != string.Empty ? args[index + 1] : null;
		}

		private static void WriteStatus(Result result)
		{
			Directory.CreateDirectory("Builds");
			result.timestamp = DateTime.UtcNow.ToString("O");
			File.WriteAllText("Builds/build-status.json", JsonUtility.ToJson(result, true));
		}
	}
}
