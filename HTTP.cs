using System.Collections.Concurrent;

namespace OPG.Signage.APIServer
{
	internal static class HTTPExtensions
	{
		public static string GetStatusCode(string codeIn)
		{
            ConcurrentDictionary<string, string> Statuscodes = new ConcurrentDictionary<string, string>()
			{
				["200"] = "OK",
				["400"] = "Bad Request",
				["404"] = "Not Found",
				["405"] = "Method Not Allowed",
				["417"] = "Expectation Failed",
				["418"] = "I\'m a teapot",
				["500"] = "Internal Server Error",
				["501"] = "Not Implmented",
				["505"] = "HTTP Version not supported"
			};

			return Statuscodes[codeIn];
		}
	}
}
