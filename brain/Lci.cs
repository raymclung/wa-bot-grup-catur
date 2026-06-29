using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

// ============================================================
//  LCI (Liga Catur Indonesia) API client - pembungkus WAbot.asmx.
//  Port dari tools/manual-pairing/pair.js: lookup -> pair -> results.
//  ASMX balas XML  <string xmlns="...">ISI-JSON</string>  -> dikupas jadi JSON.
//  Token diambil dari config.WabotToken (di-set dari secrets.json "wabotToken").
// ============================================================

internal class LciConfig
{
	public bool Enabled { get; set; } = false;

	public string BaseUrl { get; set; } = "https://services.chessstream.com/webservices/WAbot.asmx";

	public string DefaultVariant { get; set; } = "standard";

	public bool DefaultRated { get; set; } = false;
}

internal static class LciClient
{
	public sealed class LookupResult
	{
		public bool Found;
		public bool Verified;
		public string FullName = "";
		public string Handle = "";
	}

	public sealed class PairResult
	{
		public bool Success;
		public string BulkId = "";
		public string GameId = "";
		public string Url = "";
		public string White = "";
		public string Black = "";
		public string Message = "";
	}

	private static readonly Regex XmlDecl = new Regex("<\\?xml[^>]*\\?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex StringTag = new Regex("</?string[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static async Task<JsonElement?> Call(AppConfig config, HttpClient http, string method, Dictionary<string, string> form, ILogger logger)
	{
		try
		{
			string baseUrl = config.Lci?.BaseUrl ?? "";
			if (baseUrl.Length == 0 || string.IsNullOrEmpty(config.WabotToken))
			{
				logger.LogWarning("LCI {Method} dilewati: baseUrl/token kosong", method);
				return null;
			}
			Dictionary<string, string> body = new Dictionary<string, string>(form) { ["Token"] = config.WabotToken };
			using FormUrlEncodedContent content = new FormUrlEncodedContent(body);
			using HttpResponseMessage r = await http.PostAsync(baseUrl + "/" + method, content);
			string xml = await r.Content.ReadAsStringAsync();
			string inner = StringTag.Replace(XmlDecl.Replace(xml, ""), "").Trim();
			if (inner.Length == 0 || (inner[0] != '{' && inner[0] != '['))
			{
				logger.LogWarning("LCI {Method} balas non-JSON: {Body}", method, (inner.Length > 120) ? inner.Substring(0, 120) : inner);
				return null;
			}
			using JsonDocument doc = JsonDocument.Parse(inner);
			return doc.RootElement.Clone();
		}
		catch (Exception ex)
		{
			logger.LogWarning("LCI {Method} gagal: {Msg}", method, ex.Message);
			return null;
		}
	}

	// LANGKAH 1: nomor HP -> user Lichess (found / verified / handle).
	public static async Task<LookupResult> LookupByPhone(AppConfig config, HttpClient http, string phone, ILogger logger)
	{
		LookupResult res = new LookupResult();
		JsonElement? j = await Call(config, http, "GetLichessUserByPhone", new Dictionary<string, string> { ["Phone"] = phone }, logger);
		if (j.HasValue)
		{
			JsonElement e = j.Value;
			res.Found = GetBool(e, "found");
			res.Verified = GetBool(e, "is_verified");
			res.FullName = GetStr(e, "full_name");
			res.Handle = GetStr(e, "lichess_handle");
		}
		return res;
	}

	// LANGKAH 2: 2 username -> BulkID + link game (white_url).
	public static async Task<PairResult> Pair(AppConfig config, HttpClient http, string white, string black, int limitSec, int incSec, bool rated, string variant, ILogger logger)
	{
		Dictionary<string, string> p = new Dictionary<string, string>
		{
			["WhitePlayer"] = white,
			["BlackPlayer"] = black,
			["ClockLimit"] = limitSec.ToString(),
			["ClockIncrement"] = incSec.ToString(),
			["IsRated"] = (rated ? "true" : "false"),
			["Variant"] = (string.IsNullOrWhiteSpace(variant) ? "standard" : variant)
		};
		PairResult res = new PairResult();
		JsonElement? j = await Call(config, http, "PairLichessUsernames", p, logger);
		if (j.HasValue)
		{
			JsonElement e = j.Value;
			res.Success = GetBool(e, "success");
			res.BulkId = GetStr(e, "bulk_pairing_id");
			res.GameId = GetStr(e, "lichess_game_id");
			res.Url = GetStr(e, "white_url");
			res.White = GetStr(e, "white_player");
			res.Black = GetStr(e, "black_player");
			res.Message = GetStr(e, "Message");
		}
		return res;
	}

	private static bool GetBool(JsonElement e, string name)
	{
		if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v))
		{
			if (v.ValueKind == JsonValueKind.True)
			{
				return true;
			}
			if (v.ValueKind == JsonValueKind.String)
			{
				return string.Equals(v.GetString(), "true", StringComparison.OrdinalIgnoreCase);
			}
		}
		return false;
	}

	private static string GetStr(JsonElement e, string name)
	{
		if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v))
		{
			if (v.ValueKind == JsonValueKind.String)
			{
				return v.GetString() ?? "";
			}
			if (v.ValueKind == JsonValueKind.Number)
			{
				return v.ToString();
			}
		}
		return "";
	}
}
