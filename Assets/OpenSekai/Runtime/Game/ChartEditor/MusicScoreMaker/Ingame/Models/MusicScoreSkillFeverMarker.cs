namespace Sekai.MusicScoreMaker.Ingame.Models
{
	// A Skill or Fever marker kept in score.json, such as the SUS lane 0 Skill and lane f Fever notes.
	// Live play does not read these yet.
	public class MusicScoreSkillFeverMarker
	{
		public MusicScoreSkillFeverMarkerType type;

		public long ticks;

		public MusicScoreSkillFeverMarker()
		{
		}

		public MusicScoreSkillFeverMarker(MusicScoreSkillFeverMarkerType type, long ticks)
		{
			this.type = type;
			this.ticks = ticks;
		}

		public MusicScoreSkillFeverMarker Clone()
		{
			return new MusicScoreSkillFeverMarker(type, ticks);
		}
	}
}
