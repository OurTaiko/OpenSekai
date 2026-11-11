namespace Sekai.CoopLive
{
	/// <summary>
	/// The スタンプ players can send in the room and on the result (the only chat, as in the original).
	/// The server only checks the id is below its STAMP_COUNT (16); the texts are <c>stamp.&lt;id&gt;</c> in the
	/// CoopLive string table.
	/// </summary>
	public static class CoopStamps
	{
		/// <summary>How many stamps the game offers.</summary>
		public const int Count = 8;

		public static string Text(int id) => id >= 0 && id < Count ? L10n.Get("CoopLive", "stamp." + id) : "…";
	}
}
