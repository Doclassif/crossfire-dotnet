using System.Net.Sockets;
using Login.Network;
using Login.Session;
using Shared;
using Shared.Enum;
using Shared.Config;

namespace Login {
    public class LoginServer : Server {
        public LoginServer(string[] args) : base(args)
        {
            ConfigModel config = new ConfigModel();
            name = "Login Server";
            type = ServerType.Authentication;
            address = config.LOGIN_ADDRESS;
            port = config.LOGIN_PORT;
            maxConnections = config.LOGIN_MAX_CONNECTIONS;
            network = new LoginNetwork();
            base.RegisterDefaultSchedulers();
            // Scheduler.AddTask(new TestTask(Scheduler), 1, true);
        }

        public override void OnRun(TcpClient client)
        {
            if (sessions.Count < maxConnections)
            {
                LoginSession session = new LoginSession(this, client);
                session.Start();
            }
            else
            {
                client.Client.Shutdown(SocketShutdown.Both);
            }
            base.OnRun(client);
        }
    }
}