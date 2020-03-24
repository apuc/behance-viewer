using System;

namespace selenium_dotnet.DTO
{
    public class ProxyDTO
    {
        public string host { get; set; }
        public string port { get; set; }

        public string Proxy {get => host + ":" + port;}

        public override int GetHashCode()
        {
            return this.Proxy.GetHashCode();
        }

        public override bool Equals(object obj) {
            try {
                var t = obj as ProxyDTO;
                return this.host.Equals(t.host) && this.port.Equals(t.port);
            }
            catch (Exception exc) {
                return false;
            }
        }

        public override string ToString()
        {
            return host + ":" + port;
        }
    }
}