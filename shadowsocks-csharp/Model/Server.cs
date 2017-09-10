using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Web;
using Shadowsocks.Controller;

namespace Shadowsocks.Model
{
    [Serializable]
    public class Server
    {
        private const int DefaultServerTimeoutSec = 5;
        public const int MaxServerTimeoutSec = 20;

        public string server;
        public int server_port;
        public string password;
        public string method;
        public string plugin;
        public string plugin_opts;
        public string remarks;
        public int timeout;

        public override int GetHashCode()
        {
            return server.GetHashCode() ^ server_port;
        }

        public override bool Equals(object obj)
        {
            Server o2 = (Server)obj;
            return server == o2.server && server_port == o2.server_port;
        }

        public string FriendlyName()
        {
            if (server.IsNullOrEmpty())
            {
                return I18N.GetString("New server");
            }
            string serverStr;
            // CheckHostName() won't do a real DNS lookup
            var hostType = Uri.CheckHostName(server);

            switch (hostType)
            {
                case UriHostNameType.IPv6:
                    serverStr = $"[{server}]:{server_port}";
                    break;
                default:
                    // IPv4 and domain name
                    serverStr = $"{server}:{server_port}";
                    break;
            }
            return remarks.IsNullOrEmpty()
                ? serverStr
                : $"{remarks} ({serverStr})";
        }

        public Server()
        {
            server = "";
            server_port = 8388;
            method = "aes-256-cfb";
            plugin = "";
            plugin_opts = "";
            password = "";
            remarks = "";
            timeout = DefaultServerTimeoutSec;
        }

        public static List<Server> GetServers(string ssURL)
        {
            int prefixLength = "ss://".Length;
            var serverUrls = ssURL.Split('\r', '\n');

            List<Server> servers = new List<Server>();
            foreach (string serverUrl in serverUrls)
            {
                string _serverUrl = serverUrl.Trim();

                if (!_serverUrl.BeginWith("ss://", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                // Decode the Base64 part of Uri
                int indexOfHashOrSlash = _serverUrl.IndexOfAny(new[] { '@', '/', '#' }, 
                                                               prefixLength, _serverUrl.Length - prefixLength);
                string webSafeBase64Str = indexOfHashOrSlash == -1 ?
                    _serverUrl.Substring(prefixLength) :
                    _serverUrl.Substring(prefixLength, indexOfHashOrSlash - prefixLength);

                string base64Str = webSafeBase64Str.Replace('-', '+').Replace('_', '/');
                string base64 = base64Str.PadRight(base64Str.Length + (4 - base64Str.Length % 4) % 4, '=');
                string decodedBase64 = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                string decodedServerUrl = serverUrl.Replace(webSafeBase64Str, decodedBase64);

                Uri parsedUrl;
                try
                {
                    parsedUrl = new Uri(decodedServerUrl);
                }
                catch (UriFormatException)
                {
                    continue;
                }

                Server tmp = new Server
                {
                    remarks = parsedUrl.GetComponents(UriComponents.Fragment, UriFormat.Unescaped)
                };

                string userInfo = parsedUrl.GetComponents(UriComponents.UserInfo, UriFormat.Unescaped);
                tmp.server = parsedUrl.GetComponents(UriComponents.Host, UriFormat.Unescaped);
                tmp.server_port = parsedUrl.Port;

                string[] userInfoParts = userInfo.Split(new[] { ':' }, 2);
                if (userInfoParts.Length != 2)
                {
                    continue;
                }

                tmp.method = userInfoParts[0];
                tmp.password = userInfoParts[1];

                NameValueCollection queryParameters = HttpUtility.ParseQueryString(parsedUrl.Query);
                string[] pluginParts = HttpUtility.UrlDecode(queryParameters["plugin"] ?? "").Split(new[] { ';' }, 2);
                if (pluginParts.Length > 0)
                {
                    tmp.plugin = pluginParts[0] ?? "";
                }

                if (pluginParts.Length > 1)
                {
                    tmp.plugin_opts = pluginParts[1] ?? "";
                }

                servers.Add(tmp);
            }
            return servers;
        }

        public string Identifier()
        {
            return server + ':' + server_port;
        }
    }
}
