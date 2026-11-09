// Adapted from OurTaikoPlay's IosProjectBranding and MajdataPlay's iOSPostprocessBuild (both GPLv3). See NOTICE.md.
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace OpenSekai.Editor
{
	/// <summary>Info.plist keys the iOS export needs. Runs on Append builds too, which keep the old Info.plist.</summary>
	public static class IosBuildPostprocess
	{
		[PostProcessBuild(999)]
		public static void OnPostprocessBuild(BuildTarget target, string root)
		{
			if (target != BuildTarget.iOS)
			{
				return;
			}

			string plistPath = Path.Combine(root, "Info.plist");
			PlistDocument plist = new PlistDocument();
			plist.ReadFromFile(plistPath);
			plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
			// Let ProMotion displays run at 120 Hz (滑らかさ(FPS) offers 120 and Unlimited).
			plist.root.SetBoolean("CADisableMinimumFrameDuration", true);
			plist.root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);
			// Show Documents (GamePaths.Root) in the Files app, so custom charts and logs can be copied in and out.
			plist.root.SetBoolean("UIFileSharingEnabled", true);
			plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
			plist.WriteToFile(plistPath);
		}
	}
}
#endif
