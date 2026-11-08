namespace Sekai
{
	public class UserDataManager
	{
		private static UserDataManager instance;

		public static UserDataManager Instance => instance ??= new UserDataManager();

		public int SelectedDeckId { get; set; }

		private UserDataManager()
		{
			SelectedDeckId = 0;
		}
	}
}
