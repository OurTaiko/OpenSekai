namespace Sekai.MusicScoreMaker.Ingame.Models
{
	public enum MusicScoreSkillFeverMarkerType
	{
		Skill = 0,
		FeverBegin = 1,
		// No FeverEnd: the end is not authored. It falls a tenth of the chart's judgment notes after FeverStart
		// (MusicScoreMakerData.SetupFeverBeginAndFindEndInfo), and SUS has no way to write one either.
		FeverStart = 2
	}
}
