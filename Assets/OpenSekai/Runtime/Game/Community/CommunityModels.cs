using System;
using System.Collections.Generic;

namespace Sekai.Community
{
	/// <summary>A chart on the community site (https://sekai.ourtaiko.org), as /api/v1/charts returns it.</summary>
	public sealed class CommunityChart
	{
		/// <summary>32 random hex digits.</summary>
		public string Id = string.Empty;
		/// <summary>Short public number, what 譜面ID検索 takes.</summary>
		public long NumberId;
		public string Title = string.Empty;
		public string ScoreTitle = string.Empty;
		public string Composer = string.Empty;
		public string Lyricist = string.Empty;
		public string Arranger = string.Empty;
		public string Singer = string.Empty;
		public string CollaborationLabel = string.Empty;
		public string Description = string.Empty;
		public string Difficulty = "master";
		public int PlayLevel;
		public float PreviewStartSec;
		public bool HasVideo;
		public int Revision;
		public long PackageSize;
		public string JacketUrl;
		public string AudioUrl;
		/// <summary>The chart folder as parts. Only in single-chart reads (<see cref="CommunityClient.GetAsync"/>).</summary>
		public CommunityPackage Package;
		public int DownloadCount;
		public int PlayCount;
		/// <summary>Upvotes minus downvotes.</summary>
		public int Score;
		public int Upvotes;
		public int Downvotes;
		public int CommentCount;
		public List<string> Tags = new List<string>();
		public bool AllowDerivative;
		/// <summary>The 親譜面's id, or null.</summary>
		public string ParentChartId;
		/// <summary>The signed-in player's vote: 1, 0 or -1; null when signed out.</summary>
		public int? MyVote;
		public bool? Bookmarked;
		public DateTime CreatedAt;
		public DateTime UpdatedAt;
		public CommunityUser Uploader = new CommunityUser();

		/// <summary>The artist line shown under the title: singer, else composer.</summary>
		public string ArtistLine
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(Singer) && !string.IsNullOrWhiteSpace(Composer) && Singer != Composer)
				{
					return Singer + " / " + Composer;
				}
				return !string.IsNullOrWhiteSpace(Singer) ? Singer : Composer ?? string.Empty;
			}
		}
	}

	/// <summary>
	/// A chart package as the site keeps it: no ZIP, only manifest.json and the files it names. Writing the
	/// manifest and each file under its name makes the chart folder.
	/// </summary>
	public sealed class CommunityPackage
	{
		public Newtonsoft.Json.Linq.JObject Manifest;
		public List<CommunityPackageFile> Files = new List<CommunityPackageFile>();
	}

	public sealed class CommunityPackageFile
	{
		/// <summary>score, audio, jacket or video.</summary>
		public string Role = string.Empty;
		/// <summary>The file's name in the chart folder.</summary>
		public string Name = string.Empty;
		public string Url = string.Empty;
		public long Size;
		/// <summary>Lowercase hex SHA-256 of the file.</summary>
		public string Sha256 = string.Empty;
	}

	public sealed class CommunityUser
	{
		public string Id = string.Empty;
		public string Nickname = string.Empty;
		public string AvatarUrl;
	}

	public sealed class CommunityChartPage
	{
		public List<CommunityChart> Items = new List<CommunityChart>();
		public int Total;
		public int Page = 1;
		public int Pages = 1;
	}

	public sealed class CommunityTagCount
	{
		public string Tag = string.Empty;
		public int Count;
	}

	/// <summary>The site's /api/v1/meta: where to sign in and which service to trade the account token for.</summary>
	public sealed class CommunityMeta
	{
		public int ApiVersion;
		public CommunitySsoInfo Sso = new CommunitySsoInfo();
		public int PageSize = 24;
	}

	public sealed class CommunitySsoInfo
	{
		public string Issuer = "https://sso.ourtaiko.org";
		public string Service = "sekai-fanmade-prod";
	}

	/// <summary>Sort orders the site understands (the `sort` query parameter).</summary>
	public static class CommunitySort
	{
		/// <summary>Reddit's hot ranking (score against age). おすすめ and 人気.</summary>
		public const string Hot = "hot";
		/// <summary>Score, upvotes minus downvotes. ランキング 総合.</summary>
		public const string Top = "top";
		/// <summary>Plays in the last 24 hours. ランキング デイリー.</summary>
		public const string Daily = "daily";
		public const string New = "new";
		public const string Hard = "hard";
		public const string Random = "random";
	}

	public sealed class CommunityQuery
	{
		public string Sort = CommunitySort.New;
		/// <summary>null or one of easy, normal, hard, expert, master, append.</summary>
		public string Difficulty;
		/// <summary>Song title, artists, uploader, or #id.</summary>
		public string Text;
		public string Tag;
		/// <summary>Keeps sort=random stable across pages.</summary>
		public string Seed;
		public int Page = 1;

		public CommunityQuery Clone()
		{
			return (CommunityQuery)MemberwiseClone();
		}
	}

	public sealed class CommunityException : Exception
	{
		public CommunityException(string code, string message, long status = 0, Exception inner = null)
			: base(message, inner)
		{
			Code = code;
			Status = status;
		}

		/// <summary>The site's or SSO's error code (AUTH_REQUIRED, LOGIN_INVALID, RATE_LIMITED, ...), or NETWORK_ERROR.</summary>
		public string Code { get; }

		/// <summary>HTTP status, 0 when the request did not get an answer.</summary>
		public long Status { get; }
	}
}
