using System;
using System.Net;

namespace PacManBot.Utils
{
    public static class WebUtil
    {
        /// <summary>Ensures that the provided string is a valid URL, then performs a web request to determine
        /// whether the URL links to an image. This is probably not a good idea?</summary>
        public static bool IsImageUrl(string value)
        {
            try
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri)) return false;

                var req = WebRequest.Create(uri);
                req.Method = "HEAD";
                using (var resp = req.GetResponse())
                {
                    return resp.ContentType.ToLowerInvariant().StartsWith("image/");
                }
            }
            catch (WebException)
            {
                return false;
            }
        }
    }
}
