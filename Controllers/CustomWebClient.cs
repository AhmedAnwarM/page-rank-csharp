using System;
using System.Net;

namespace test.Controllers
{
    public class CustomWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            var w = base.GetWebRequest(uri);
            if (w == null) throw new Exception();
            w.Timeout = 2000;
            return w;
        }
    }
}