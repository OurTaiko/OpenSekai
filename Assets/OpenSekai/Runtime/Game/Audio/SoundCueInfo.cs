namespace Sekai
{
	/// <summary>
	/// Cue metadata exposed by <see cref="SoundManager.GetCueInfos"/>. Replaces CriAtomEx.CueInfo; the field names
	/// match so existing callers only change the type.
	/// </summary>
	public struct SoundCueInfo
	{
		public string name;

		/// <summary>Length in milliseconds; -1 for an infinite (looping) cue, 0 when unknown.</summary>
		public long length;
	}

	/// <summary>Replaces CriAtomExPlayback in fields that only held a playback id.</summary>
	public struct SoundPlayback
	{
		public enum Status
		{
			Prep = 0,
			Playing = 1,
			Removed = 2
		}

		public uint id;

		public SoundPlayback(uint id)
		{
			this.id = id;
		}
	}
}
