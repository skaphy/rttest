using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using CoreTweet.Core;
using CoreTweet;
using Newtonsoft.Json;

namespace RtTest
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Tokens tokens;

			// ConsumerKey等読み込み
			var config = Config.Load ();
			if (String.IsNullOrEmpty(config.ConsumerKey) || String.IsNullOrEmpty(config.ConsumerSecret)) {
				Console.WriteLine ("config.jsonにConsumerKeyとConsumerSecretを指定してください");
				return;
			}
			if (String.IsNullOrEmpty (config.AccessToken) || String.IsNullOrEmpty (config.AccessTokenSecret)) {
				var session = OAuth.Authorize (config.ConsumerKey, config.ConsumerSecret);
				Console.WriteLine ("このURLにアクセスしてPINコードを取得してください: " + session.AuthorizeUri);
				Console.Write ("PIN: ");
				var pin = Console.ReadLine ();
				tokens = OAuth.GetTokens (session, pin);
				config.AccessToken = tokens.AccessToken;
				config.AccessTokenSecret = tokens.AccessTokenSecret;
				config.Save ();
				return;
			} else {
				tokens = Tokens.Create (config.ConsumerKey, config.ConsumerSecret, config.AccessToken, config.AccessTokenSecret);
			}

			// 引数チェック
			if (args.Length < 2) {
				Console.WriteLine ("RtTest.exe status_id screen_name");
				return;
			}
			var targetStatus = tokens.Statuses.Show (id => long.Parse (args [0]));
			var targetScreenName = args[1];

			// 次のツイートを取得
			var nextTweet = GetNextTweet(tokens, targetStatus, targetScreenName);
			if (nextTweet == null) {
				Console.WriteLine ("見つかりませんでした");
			} else {
				Console.WriteLine ("@{0}: {1}", nextTweet.User.ScreenName, nextTweet.Text);
			}
		}

		/// <summary>
		/// screenNameのユーザがtargetStatusをRTした時のツイートを取得
		/// </summary>
		/// <returns>The retweeted status by screen name.</returns>
		/// <param name="tokens">tokens.</param>
		/// <param name="targetStatus">取得対象のツイート</param>
		/// <param name="screenName">Screen name.</param>
		private static Status FindRetweetedStatusByScreenName(Tokens tokens, Status targetStatus, string screenName)
		{
			var searchQuery = "RT @" + targetStatus.User.ScreenName + ": from:" + screenName;
			var searchResult = tokens.Search.Tweets (q => searchQuery, include_entities => true);
			if (searchResult.Count < 1) return null;
			foreach (var tweet in searchResult) {
				if (tweet.RetweetedStatus.Id == targetStatus.Id) return tweet;
			}
			return null;
		}

		/// <summary>
		/// 指定したツイートの次のツイートを取得
		/// </summary>
		/// <returns>The next tweet.</returns>
		/// <param name="tokens">tokens.</param>
		/// <param name="targetStatus">取得対象のツイート</param>
		/// <param name="screenName">Screen name.</param>
		private static Status GetNextTweet(Tokens tokens, Status targetStatus, string screenName)
		{
			var retweetedStatus = FindRetweetedStatusByScreenName (tokens, targetStatus, screenName);
			if (retweetedStatus == null) return null;

			var query = "from:" + screenName;
			query += " since:" + retweetedStatus.CreatedAt.ToString ("yyyy-MM-dd");
			query += " until:" + (retweetedStatus.CreatedAt.AddDays(1)).ToString("yyyy-MM-dd");
			long max_id = -1;
			SearchResult searchResult;
			do {
				var searchArgs = new Dictionary<string, object>() {
					{"q", query},
					{"count", 100},
				};
				if (max_id == -1) {
					searchArgs.Add("max_id", max_id);
				}
				searchResult = tokens.Search.Tweets(searchArgs);
				for (var i = 1; i < searchResult.Count; i++) {
					if (searchResult[i].Id == retweetedStatus.Id) return searchResult[i-1];
				}
				max_id = searchResult.Max(x => x.Id);
			} while(searchResult.Count == 100);
			return null;
		}
	}

	class Config
	{
		private static readonly string ConfigFilename = "config.json";

		public string ConsumerKey { set; get; }
		public string ConsumerSecret { set; get; }
		public string AccessToken { set; get; }
		public string AccessTokenSecret { set; get; }

		public static Config Load()
		{
			var configText = File.ReadAllText (ConfigFilename);
			var json = JsonConvert.DeserializeObject<Config>(configText);
			return json;
		}

		public void Save()
		{
			File.WriteAllText(ConfigFilename, JsonConvert.SerializeObject (this));
		}
	}

}
