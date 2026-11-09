// Adapted from OurTaikoPlay's IosProjectBranding and MajdataPlay's iOSPostprocessBuild (both GPLv3). See NOTICE.md.
#if UNITY_IOS
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace OpenSekai.Editor
{
	/// <summary>
	/// Finishes the iOS export: the build number and the Info.plist keys it needs (on Append builds too, which keep the
	/// old Info.plist), and the Xcode project, app target, scheme and folders named OpenSekai instead of Unity-iPhone.
	/// UnityFramework keeps its name.
	/// </summary>
	public static class IosBuildPostprocess
	{
		private const string UnityName = "Unity-iPhone";
		private const string AppName = PlayerBuilds.ProductName;

		/// <summary>
		/// Unity's incremental exporter expects its own project, target and folder names, so a renamed export is named
		/// back before Unity writes into it, and renamed again after all other build callbacks. Returns true when the
		/// folder holds an earlier export, which the build then appends to.
		/// </summary>
		public static bool PrepareForExport(string root)
		{
			string branded = Path.Combine(root, AppName + ".xcodeproj");
			string original = Path.Combine(root, UnityName + ".xcodeproj");
			if (!Directory.Exists(branded))
			{
				return false;
			}

			if (Directory.Exists(original))
			{
				throw new BuildFailedException("Both " + AppName + ".xcodeproj and " + UnityName +
				                               ".xcodeproj exist. Export to a new folder to avoid overwriting project edits.");
			}

			Rename(root, AppName, UnityName);
			return true;
		}

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
			// The build number is the commit count, so each upload to App Store Connect is larger than the last.
			plist.root.SetString("CFBundleVersion", PlayerBuilds.CommitCount());
			plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
			// Let ProMotion displays run at 120 Hz (滑らかさ(FPS) offers 120 and Unlimited).
			plist.root.SetBoolean("CADisableMinimumFrameDuration", true);
			plist.root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);
			// Show Documents (GamePaths.Root) in the Files app, so custom charts and logs can be copied in and out.
			plist.root.SetBoolean("UIFileSharingEnabled", true);
			plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
			plist.WriteToFile(plistPath);

			Rename(root, UnityName, AppName);
		}

		/// <summary>Renames the project, app target, product, schemes and the generated folders from one name to the other.</summary>
		public static void Rename(string root, string from, string to)
		{
			string source = Path.Combine(root, from + ".xcodeproj");
			string destination = Path.Combine(root, to + ".xcodeproj");
			if (!Directory.Exists(source))
			{
				if (!Directory.Exists(destination))
				{
					throw new BuildFailedException("Missing Xcode project: " + source);
				}

				source = destination;
			}

			if (source != destination && Directory.Exists(destination))
			{
				throw new BuildFailedException("Xcode project already exists: " + destination);
			}

			foreach (string suffix in new[] { "", " Tests" })
			{
				ValidateDirectoryRename(Path.Combine(root, from + suffix), from, to);
			}

			string file = Path.Combine(source, "project.pbxproj");
			PBXProject project = new PBXProject();
			project.ReadFromFile(file);
			string main = project.TargetGuidByName(AppName);
			if (string.IsNullOrEmpty(main))
			{
				main = project.GetUnityMainTargetGuid();
			}

			if (string.IsNullOrEmpty(main))
			{
				throw new BuildFailedException("Missing app target: " + from);
			}

			// Rename the generated app and test names everywhere they are referenced: groups, products, build settings
			// and file paths.
			project.SetBuildProperty(main, "PRODUCT_NAME", AppName);
			string text = RenameText(project.WriteToString(), from, to);
			project.ReadFromString(text);
			project.SetBuildProperty(main, "PRODUCT_NAME", AppName);
			text = project.WriteToString();
			// PBXProject's serializer hardcodes this comment even for a renamed project.
			text = text.Replace("PBXProject \"" + UnityName + "\"", "PBXProject \"" + to + "\"");

			Regex target = new Regex(@"(?m)^(\s*" + Regex.Escape(main) + @" /\* [^\r\n]* \*/ = \{\r?\n)(.*?)(^\s*\};)",
				RegexOptions.Singleline | RegexOptions.Multiline);
			Match match = target.Match(text);
			if (!match.Success)
			{
				throw new BuildFailedException("Cannot locate the app target in the Xcode project.");
			}

			string body = Regex.Replace(match.Groups[2].Value, @"(?m)^(\s*name = )[^;]+;", "$1\"" + to + "\";");
			body = Regex.Replace(body, @"(?m)^(\s*productName = )[^;]+;", "$1" + AppName + ";");
			text = text.Substring(0, match.Index) + match.Groups[1].Value + body + match.Groups[3].Value +
			       text.Substring(match.Index + match.Length);

			string productGuid = project.GetTargetProductFileRef(main);
			Regex product = new Regex(@"(?m)^(\s*" + Regex.Escape(productGuid) + @" /\* [^\r\n]* \*/ = \{)(.*?)(\};)",
				RegexOptions.Singleline | RegexOptions.Multiline);
			text = product.Replace(text, m => m.Groups[1].Value +
			                                  Regex.Replace(m.Groups[2].Value, @"\bpath = [^;]+;", "path = " + AppName + ".app;") +
			                                  m.Groups[3].Value, 1);
			text = text.Replace(productGuid + " /* Unity-Target-New.app */", productGuid + " /* " + AppName + ".app */");
			File.WriteAllText(file, text);

			foreach (string schemePath in Directory.GetFiles(source, "*.xcscheme", SearchOption.AllDirectories))
			{
				XDocument scheme = XDocument.Load(schemePath);
				foreach (XAttribute attribute in scheme.Descendants().Attributes())
				{
					attribute.Value = RenameText(attribute.Value, from, to);
				}

				foreach (XElement reference in scheme.Descendants("BuildableReference"))
				{
					if ((string)reference.Attribute("BlueprintIdentifier") == main)
					{
						reference.SetAttributeValue("BlueprintName", to);
						reference.SetAttributeValue("BuildableName", AppName + ".app");
					}
				}

				scheme.Save(schemePath);
				string renamedScheme = Path.Combine(Path.GetDirectoryName(schemePath), RenameText(Path.GetFileName(schemePath), from, to));
				if (schemePath != renamedScheme)
				{
					File.Move(schemePath, renamedScheme);
				}
			}

			foreach (string managementPath in Directory.GetFiles(source, "xcschememanagement.plist", SearchOption.AllDirectories))
			{
				PlistDocument management = new PlistDocument();
				management.ReadFromFile(managementPath);
				if (management.root.values.TryGetValue("SchemeUserState", out PlistElement state))
				{
					var entries = state.AsDict().values;
					foreach (string key in entries.Keys.Where(key => RenameText(key, from, to) != key).ToArray())
					{
						PlistElement value = entries[key];
						entries.Remove(key);
						entries[RenameText(key, from, to)] = value;
					}

					management.WriteToFile(managementPath);
				}
			}

			foreach (string workspace in Directory.GetFiles(source, "contents.xcworkspacedata", SearchOption.AllDirectories))
			{
				File.WriteAllText(workspace, RenameText(File.ReadAllText(workspace), from, to));
			}

			foreach (string suffix in new[] { "", " Tests" })
			{
				RenameDirectory(Path.Combine(root, from + suffix), from, to);
			}

			if (source != destination)
			{
				Directory.Move(source, destination);
			}
		}

		// Only whole names change. The app name is also the prefix of OpenSekai's assemblies, whose IL2CPP sources
		// (OpenSekai.Game.cpp, OpenSekai.AcbDecoder.cpp, ...) are in the same project and must keep their names when an
		// export is named back to Unity-iPhone; a name followed by a dot is renamed only before these extensions.
		private static string RenameText(string text, string from, string to)
		{
			string name = @"(?<![A-Za-z0-9_.])" + Regex.Escape(from) +
			              @"(?![A-Za-z0-9_])(?!\.(?!(?:app|xcodeproj|xcscheme|xctest)\b)[A-Za-z0-9_])";
			return Regex.Replace(text.Replace(from.Replace('-', '_') + "_Tests", to.Replace('-', '_') + "_Tests"), name,
				to.Replace("$", "$$"));
		}

		private static void ValidateDirectoryRename(string path, string from, string to)
		{
			if (!Directory.Exists(path))
			{
				return;
			}

			string destination = Path.Combine(Path.GetDirectoryName(path), RenameText(Path.GetFileName(path), from, to));
			if (path != destination && (Directory.Exists(destination) || File.Exists(destination)))
			{
				throw new BuildFailedException("Cannot rename an exported folder; this already exists: " + destination);
			}

			foreach (string child in Directory.GetDirectories(path))
			{
				ValidateDirectoryRename(child, from, to);
			}

			foreach (string file in Directory.GetFiles(path))
			{
				string renamed = Path.Combine(path, RenameText(Path.GetFileName(file), from, to));
				if (file != renamed && (File.Exists(renamed) || Directory.Exists(renamed)))
				{
					throw new BuildFailedException("Cannot rename an exported file; this already exists: " + renamed);
				}
			}
		}

		private static void RenameDirectory(string path, string from, string to)
		{
			if (!Directory.Exists(path))
			{
				return;
			}

			foreach (string child in Directory.GetDirectories(path))
			{
				RenameDirectory(child, from, to);
			}

			foreach (string file in Directory.GetFiles(path))
			{
				string renamed = Path.Combine(path, RenameText(Path.GetFileName(file), from, to));
				if (file != renamed)
				{
					File.Move(file, renamed);
				}
			}

			string destination = Path.Combine(Path.GetDirectoryName(path), RenameText(Path.GetFileName(path), from, to));
			if (path != destination)
			{
				Directory.Move(path, destination);
			}
		}
	}

	/// <summary>Names an earlier export back to Unity-iPhone for builds that skip both PlayerBuilds and the Build window handler.</summary>
	internal sealed class IosBuildPreprocess : IPreprocessBuildWithReport
	{
		public int callbackOrder => -100;

		public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
		{
			if (report.summary.platform == BuildTarget.iOS)
			{
				IosBuildPostprocess.PrepareForExport(report.summary.outputPath);
			}
		}
	}
}
#endif
