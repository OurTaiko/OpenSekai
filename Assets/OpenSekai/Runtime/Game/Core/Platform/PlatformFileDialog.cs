using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sekai
{
	/// <summary>
	/// Opens and saves user files on every platform, so callers have no platform branches.
	/// <list type="bullet">
	/// <item>Editor and desktop players: StandaloneFileBrowser's modal dialogs. Callbacks run before the call returns.</item>
	/// <item>Android and iOS: NativeFilePicker's system picker and share sheet. Callbacks run later on the main thread.</item>
	/// <item>Anything else: no picker; <see cref="PickFile"/> reports <see cref="PickResult.Unsupported"/>, and exports stay in the app folder.</item>
	/// </list>
	/// </summary>
	public static class PlatformFileDialog
	{
		public enum PickResult
		{
			/// <summary>The picker opened; the callback gets the path, or an empty string when cancelled.</summary>
			Opened,
			/// <summary>Another system picker is still open. The callback is not called.</summary>
			Busy,
			/// <summary>This platform has no file picker. The callback is not called.</summary>
			Unsupported,
		}

		public enum ExportResult
		{
			/// <summary>The file was written where the user chose, or handed to the system share sheet.</summary>
			Exported,
			/// <summary>The file is in the app folder, but the platform cannot hand it on.</summary>
			SavedInAppFolder,
			/// <summary>The user closed the save dialog or share sheet, or the share failed.</summary>
			Cancelled,
			/// <summary>The write callback returned no path.</summary>
			WriteFailed,
			/// <summary>Another system picker is still open.</summary>
			Busy,
		}

		/// <summary>Desktop dialogs can pick folders; the mobile pickers cannot.</summary>
		public static bool CanPickFolder
		{
			get
			{
#if UNITY_EDITOR || UNITY_STANDALONE
				return true;
#else
				return false;
#endif
			}
		}

		/// <summary>
		/// Lets the user pick one file. <paramref name="extensions"/> are without dots; Android shows every file
		/// (its picker filters by MIME type and greys out custom extensions such as .sus), so validate the result.
		/// </summary>
		public static PickResult PickFile(string title, string filterName, string[] extensions, Action<string> onPicked)
		{
#if UNITY_EDITOR || UNITY_STANDALONE
			string[] paths = SFB.StandaloneFileBrowser.OpenFilePanel(title, string.Empty, new[] { new SFB.ExtensionFilter(filterName, extensions) }, false);
			onPicked?.Invoke(paths != null && paths.Length > 0 ? paths[0] : string.Empty);
			return PickResult.Opened;
#elif UNITY_ANDROID || UNITY_IOS
			if (NativeFilePicker.IsFilePickerBusy())
			{
				return PickResult.Busy;
			}

			NativeFilePicker.PickFile(path => onPicked?.Invoke(path ?? string.Empty), ToNativeFileTypes(extensions));
			return PickResult.Opened;
#else
			return PickResult.Unsupported;
#endif
		}

		/// <summary>Lets the user pick a folder. Returns an empty string when cancelled or unsupported (<see cref="CanPickFolder"/>).</summary>
		public static string PickFolder(string title)
		{
#if UNITY_EDITOR || UNITY_STANDALONE
			string[] paths = SFB.StandaloneFileBrowser.OpenFolderPanel(title, string.Empty, false);
			return paths != null && paths.Length > 0 ? paths[0] : string.Empty;
#else
			return string.Empty;
#endif
		}

		/// <summary>
		/// Saves a file the user keeps outside the game.
		/// On desktop the user picks the destination first and <paramref name="write"/> gets it; when they cancel,
		/// nothing is written and <paramref name="onDone"/> gets <see cref="ExportResult.Cancelled"/> with no path.
		/// Elsewhere <paramref name="write"/> gets null and writes into the app folder; on Android and iOS the file is
		/// then handed to the system share sheet. <paramref name="write"/> returns the written path, or null on failure.
		/// <paramref name="onDone"/> always gets that path when there is one.
		/// </summary>
		public static void Export(string title, string defaultDirectory, string defaultName, string extension,
			Func<string, string> write, Action<ExportResult, string> onDone)
		{
#if UNITY_EDITOR || UNITY_STANDALONE
			string destination = SFB.StandaloneFileBrowser.SaveFilePanel(title, defaultDirectory ?? string.Empty, defaultName ?? string.Empty, extension);
			if (string.IsNullOrEmpty(destination))
			{
				onDone?.Invoke(ExportResult.Cancelled, null);
				return;
			}

			string path = write(destination);
			onDone?.Invoke(string.IsNullOrEmpty(path) ? ExportResult.WriteFailed : ExportResult.Exported, path);
#elif UNITY_ANDROID || UNITY_IOS
			if (NativeFilePicker.IsFilePickerBusy())
			{
				onDone?.Invoke(ExportResult.Busy, null);
				return;
			}

			string path = write(null);
			if (string.IsNullOrEmpty(path))
			{
				onDone?.Invoke(ExportResult.WriteFailed, null);
				return;
			}

			if (!NativeFilePicker.CanExportFiles())
			{
				onDone?.Invoke(ExportResult.SavedInAppFolder, path);
				return;
			}

			NativeFilePicker.ExportFile(path, success => onDone?.Invoke(success ? ExportResult.Exported : ExportResult.Cancelled, path));
#else
			string path = write(null);
			onDone?.Invoke(string.IsNullOrEmpty(path) ? ExportResult.WriteFailed : ExportResult.SavedInAppFolder, path);
#endif
		}

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && !UNITY_STANDALONE
		private static string[] ToNativeFileTypes(string[] extensions)
		{
#if UNITY_ANDROID
			return Array.Empty<string>();
#else
			List<string> fileTypes = new List<string>();
			if (extensions == null)
			{
				return fileTypes.ToArray();
			}

			foreach (string raw in extensions)
			{
				if (string.IsNullOrWhiteSpace(raw))
				{
					continue;
				}

				string extension = raw.Trim().TrimStart('.');
				// A value with a slash is already a MIME type / UTI.
				string fileType = extension.IndexOf('/') >= 0 ? extension : NativeFilePicker.ConvertExtensionToFileType(extension);
				if (!string.IsNullOrEmpty(fileType) && !fileTypes.Contains(fileType))
				{
					fileTypes.Add(fileType);
				}
			}

			return fileTypes.ToArray();
#endif
		}
#endif
	}
}
