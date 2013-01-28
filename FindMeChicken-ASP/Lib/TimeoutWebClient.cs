using System;
using System.Net;

namespace FindMeChicken_ASP.Lib
{
    public class TimeoutWebClient : WebClient
    {
        int _timeout;

        public TimeoutWebClient()
            : base()
        {
            this._timeout = 0;
        }

        public void SetTimeout(int timeout_seconds) { this._timeout = timeout_seconds*1000; }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest w = base.GetWebRequest(address);
            w.Timeout = this._timeout;
            return w;
        }
    }
}