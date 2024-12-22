using System;
using System.Net;
using System.Net.Sockets;
using Game.Network.packet;
using Newtonsoft.Json;
using Shared;
using Shared.Model;
using Shared.Network;
using Shared.Util;
using Shared.Util.Log.Factories;
using Shared.Config;

namespace Game.Session
{
    public class GameSession : Shared.Session.Session
    {
        private User _user;
        private FeverData _fever = new FeverData();
        private string api;

        public GameSession(Server server, TcpClient client) : base(server, client)
        {
            ConfigModel config = new ConfigModel();
            config.Load();
            api = "http://" + config.HOST + ":" + config.PORT + "/";
        }

        protected override void OnRun(byte[] buffer)
        {
            try
            {
                DataPacket packet = server.Network.GetPacket((short) server.Network.GetTypeOf(buffer));
                if (packet != null)
                {
                    packet.Buffer = buffer;
                    if (packet.IsValid)
                    {
                        packet.Decode();
                        LogFactory.GetLog(server.Name)
                            .LogInfo($"Received Packet [{packet.Pid().ToString()}] [{packet.Buffer.Length}]");
                        LogFactory.GetLog(server.Name).LogInfo($"\n{NetworkUtil.DumpPacket(packet.Buffer)}");
                        HandlePacket(packet);
                    }
                    else
                    {
                        LogFactory.GetLog(server.Name)
                            .LogWarning(
                                $"Received Invalid Packet [{packet.Pid().ToString()}] [{packet.Buffer.Length}]");
                        packet.Decode();
                        LogFactory.GetLog(server.Name).LogInfo($"\n{NetworkUtil.DumpPacket(packet.Buffer)}");
                    }
                }
                else
                {
                    LogFactory.GetLog(server.Name)
                        .LogWarning($"Unknown Packet with ID {(short) server.Network.GetTypeOf(buffer)}.");
                    LogFactory.GetLog(server.Name).LogInfo($"\n{NetworkUtil.DumpPacket(buffer)}");
                }
            }
            catch (Exception e)
            {
                LogFactory.GetLog(server.Name).LogFatal(e);
            }

            base.OnRun(buffer);
        }

        protected override void HandlePacket(DataPacket packet)
        {
            switch (packet.Pid())
            {
                case AuthToChannelServerPacket.NetworkId:
                    AuthToChannelServerPacket authToChannelServerPacket = (AuthToChannelServerPacket) packet;
                    Id = authToChannelServerPacket.Identifier;
                    SendPacket(authToChannelServerPacket);
                    
                    Internet.Get(api, $"user/{authToChannelServerPacket.Username}", result =>
                    {
                        UserData data = JsonConvert.DeserializeObject<UserData>(result);
                        if (data != null)
                        {
                            _user = data.User;
                            _user.Identifier = id;
                            Internet.Get(api, $"user/fever/{_user.Id}",
                                res =>
                                {
                                    FeverData fData = JsonConvert.DeserializeObject<FeverData>(result);
                                    if (fData != null) _fever = fData;
                                }, error => { });
                            _fever.Callback = () =>
                            {
                                LogFactory.GetLog(server.Name)
                                    .LogInfo($"Fever Progress of {_user.Identifier} is {_fever.Progress}%.");
                            };
                        }
                    }, error => {});
                    break;
                case ZettaPointsPacket.NetworkId:
                    ZettaPointsPacket zettaPointsPacket = (ZettaPointsPacket) packet;
                    zettaPointsPacket.User = _user;
                    SendPacket(zettaPointsPacket);
                    break;
                case GetChannelsRequestPacket.NetworkId:
                    GetChannelsRequestPacket getChannelsRequestPacket = (GetChannelsRequestPacket) packet;
                    getChannelsRequestPacket.Server = server;
                    SendPacket(getChannelsRequestPacket);
                    break;
                case FeverInfoUpdatePacket.NetworkId:
                    FeverInfoUpdatePacket ferverInfoUpdatePacket = (FeverInfoUpdatePacket) packet;
                    ferverInfoUpdatePacket.Session = this;
                    SendPacket(ferverInfoUpdatePacket);
                    break;
                case ServerTimePacket.NetworkId:
                    ServerTimePacket serverTimePacket = (ServerTimePacket) packet;
                    serverTimePacket.User = _user;
                    SendPacket(serverTimePacket);
                    break;
            }

            base.HandlePacket(packet);
        }

        public override void OnFinishPacketSent(DataPacket packet)
        {
            switch (packet.Pid())
            {
                case AuthToChannelServerPacket.NetworkId:
                    Internet.Get(api, $"battle/statistics/{User.Id}", result =>
                    {
                        BattleData data = JsonConvert.DeserializeObject<BattleData>(result);
                        if (data != null)
                        {
                            PlayerDataPacket p = new PlayerDataPacket { User = User, Statistics = data.Statistics };
                            SendPacket(p);
                        }
                    }, error => {});
                    break;
            }

            base.OnFinishPacketSent(packet);
        }

        public User User => _user;
        public FeverData Fever => _fever;
    }
}