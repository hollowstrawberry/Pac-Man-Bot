using System;
using System.Net;
using System.Threading.Tasks;

namespace PacManBot.Utils
{
    public static class WebUtil
    {
        /// <summary>Ensures that the provided string is a valid URL, then performs a web request to determine
        /// whether the URL links to an image.</summary>
        public static async ValueTask<bool> IsImageUrlAsync(string value)
        {
            try
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri)) return false;

                var req = WebRequest.Create(uri);
                req.Method = "HEAD";
                using var resp = await req.GetResponseAsync();
                return resp.ContentType.ToLowerInvariant().StartsWith("image/");
            }
            catch (WebException)
            {
                return false;
            }
        }
    }
}
