namespace Sekai.MusicScoreMaker.Ingame.Events
{
	/// <summary>Menu: make the current position the start of bar 1 (sets the chart's fillerSec).</summary>
	public class SetFirstBarToCurrentPositionEvent : MusicScoreMakerDispatcherEventBase
	{
		public SetFirstBarToCurrentPositionEvent()
		{
		}
	}
}
