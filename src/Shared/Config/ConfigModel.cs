using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.Config
{
    public class ConfigModel
    {
        public string HOST;
        public int PORT;
        public string DB_IP, DB_NAME, DB_USER, DB_PASS, LOGIN_ADDRESS;
        public int DB_PORT, LOGIN_PORT, LOGIN_MAX_CONNECTIONS;

        public ConfigModel()
        {
            ConfigLoader config = new ConfigLoader("Settings.ini");
            HOST = config.getValue("Host", "127.0.0.1");
            PORT = int.Parse(config.getValue("Port", "39190"));
            DB_IP = config.getValue("DataBaseHost", "localhost");
            DB_NAME = config.getValue("DataBaseName", "cf");
            DB_USER = config.getValue("DataBaseUser", "root");
            DB_PASS = config.getValue("DataBasePass", "");
            DB_PORT = int.Parse(config.getValue("DataBasePort", "3306"));
            LOGIN_ADDRESS = config.getValue("LoginAddress", "127.0.0.1");
            LOGIN_PORT = int.Parse(config.getValue("LoginPort", "13008"));
            LOGIN_MAX_CONNECTIONS = int.Parse(config.getValue("LoginMaxConnections", "200"));
        }
    }
}
