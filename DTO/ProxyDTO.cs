using System;

namespace selenium_dotnet.DTO
{
    public class ProxyDTO
    {
        public string host { get; set; }
        public string port { get; set; }

        public override int GetHashCode()
        {
            return host.GetHashCode() + port.GetHashCode();
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