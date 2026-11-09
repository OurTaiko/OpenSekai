namespace Sekai.CoopLive
{
	/// <summary>
	/// The スタンプ players can send in the room and on the result (the only chat, as in the original).
	/// The server only checks the id is below its STAMP_COUNT (16); the text lives here.
	/// </summary>
	public static class CoopStamps
	{
		public static readonly string[] Texts =
		{
			"よろしく！",
			"ありがとう！",
			"おつかれさま！",
			"ナイス！",
			"がんばろう！",
			"もう一回！",
			"いいね！",
			"ごめんね",
		};

		public static string Text(int id) => id >= 0 && id < Texts.Length ? Texts[id] : "…";
	}
}
