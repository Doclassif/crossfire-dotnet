﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Login.Enum;
using Login.Model;
using Login.Network.packet;
using Login.Task;
using Newtonsoft.Json;
using Shared;
using Shared.Model;
using Shared.Network;
using Shared.Util;
using Shared.Util.Log.Factories;
using Shared.Config;

namespace Login.Session
{
    public class LoginSession : Shared.Session.Session
    {
        private KickInactiveSession _kickTask;
        private User _user;
        private string api;

        public LoginSession(Server server, TcpClient client) : base(server, client)
        {
            ConfigModel config = ConfigModel.load();
            api = "http://" + config.HOST + ":" + config.PORT + "/";

            _kickTask = new KickInactiveSession(this, server.Scheduler);
            server.Scheduler.AddTask(_kickTask, 1, true);
        }
        protected override void OnRun(byte[] buffer)
        {
            try
            {
                DataPacket packet = server.Network.GetPacket((short)server.Network.GetTypeOf(buffer));
                if (packet != null)
                {
                    packet.Buffer = buffer;
                    if (packet.IsValid)
                    {
                        _kickTask.Inactive = 0;
                        packet.Decode();
                        LogFactory.GetLog(server.Name).LogInfo($"Received Packet [{packet.Pid().ToString()}] [{packet.Buffer.Length}]");
                        LogFactory.GetLog(server.Name).LogInfo($"\n{NetworkUtil.DumpPacket(packet.Buffer)}");
                        HandlePacket(packet);
                    }
                    else
                    {
                        LogFactory.GetLog(server.Name).LogWarning($"Received Invalid Packet [{packet.Pid().ToString()}] [{packet.Buffer.Length}]");
                        packet.Decode();
                        LogFactory.GetLog(server.Name).LogInfo($"\n{NetworkUtil.DumpPacket(packet.Buffer)}");
                    }
                }
                else
                {
                    LogFactory.GetLog(server.Name).LogWarning($"Unknown Packet with ID {(short)server.Network.GetTypeOf(buffer)}.");
                    LogFactory.GetLog(server.Name).LogInfo($"\n{NetworkUtil.DumpPacket(buffer)}");
                } 
                base.OnRun(buffer);
            }catch (Exception e){
                LogFactory.GetLog(server.Name).LogFatal(e);
            }
        }

        protected override void HandlePacket(DataPacket packet)
        {
            switch (packet.Pid())
            {
                case LoginRequestDataPacket.NetworkId:
                    LoginRequestDataPacket loginRequestDataPacket = (LoginRequestDataPacket) packet;
                    Validate(loginRequestDataPacket);
                    break;
                case LoginExitRequestPacket.NetworkId:
                    SendPacket(packet);
                    break;
                case LoginToGameServerRequestStep1Packet.NetworkId:
                    LoginToGameServerRequestStep1Packet response = (LoginToGameServerRequestStep1Packet) packet;
                    response.User = _user;
                    SendPacket(response);
                    break;
                case CreateAccountPacket.NetworkId:
                    if (User.IsFirstTimeJoined())
                    {
                        CreateAccountPacket createAccountPacket = (CreateAccountPacket) packet;
                        if (createAccountPacket.Confirmed)
                        {
                            Internet.Get(api, $"user/nickname/change/{User.Id}/{createAccountPacket.Nickname}", result =>
                            {
                                VerifyResult data = JsonConvert.DeserializeObject<VerifyResult>(result);
                                if (data == null || !data.Result)
                                {
                                    SendPacket(new LoginErrorResponsePacket { Error = ErrorsType.UnknownError });   
                                }
                            }, error =>
                            {
                                SendPacket(new LoginErrorResponsePacket { Error = ErrorsType.CouldNotConnectToTheServer });
                            });
                        }
                        SendPacket(createAccountPacket);
                    }
                    break;
                case CheckNameExistencePacket.NetworkId:
                    CheckNameExistencePacket p = (CheckNameExistencePacket) packet;
                    Internet.Get(api, $"user/nickname/exists/{p.Nickname}", result =>
                    {
                        VerifyResult data = JsonConvert.DeserializeObject<VerifyResult>(result);
                        if (data != null)
                        {
                            SendPacket(p);   
                        }
                        else
                        {
                            SendPacket(new LoginErrorResponsePacket { Error = ErrorsType.CouldNotConnectToTheServer });
                        }
                    }, error =>
                    {
                        SendPacket(new LoginErrorResponsePacket { Error = ErrorsType.CouldNotConnectToTheServer });
                    });
                    break;
            }
            base.HandlePacket(packet);
        }

        private void Validate(LoginRequestDataPacket packet)
        {
            Internet.Get(api, $"user/{packet.Username}", result =>
            {
                UserData data = JsonConvert.DeserializeObject<UserData>(result);
                if (data != null)
                {
                    bool exists = data.Status != (int) HttpStatusCode.NotFound;
                    bool connected = false;
                    bool validPassword = data.User.Verify(packet.Password);
                    if (exists && validPassword && !connected)
                    {
                        Authenticate(ErrorsType.NoError, packet);
                        _user = data.User;
                        _user.Identifier = id;
                    }
                    else if(validPassword)
                    {
                        Authenticate(ErrorsType.PlayerAlreadyLoggedIn, packet);
                    }
                    else
                    {
                        Authenticate(ErrorsType.UnknownUsernameOrPassword, packet);
                    }
                }
            }, error => Authenticate(ErrorsType.UnknownError, packet));
        }

        private void Authenticate(ErrorsType type, LoginRequestDataPacket request)
        {
            if (type == ErrorsType.NoError)
            {
                Id = request.Identifier;
                LoginResponsePacket packet = new LoginResponsePacket();
                SendPacket(packet);
                LogFactory.GetLog(server.Name).LogInfo($"[SESSION] [AUTHENTICATE STATUS: {type.ToString()}].");
            }
            else
            {
                LoginErrorResponsePacket packet = new LoginErrorResponsePacket {Identifier = 0, Error = type};
                SendPacket(packet);
                LogFactory.GetLog(server.Name).LogInfo($"[SESSION] [AUTHENTICATE STATUS: {type.ToString()}].");
            }
        }

        public override void OnFinishPacketSent(DataPacket packet)
        {
            switch (packet.Pid())
            {
                case (short) PacketType.S2CValidAccount:
                    DataPacket response = null;
                    Internet.Get(api, "server/all", result =>
                    {
                        List<GameServer> servers = JsonConvert.DeserializeObject<List<GameServer>>(result);
                        response = new SendServerListPacket { Servers = servers, User = _user };
                    }, error =>
                    {
                        response = new LoginErrorResponsePacket { Error = ErrorsType.CouldNotConnectToTheServer };
                    });
                    SendPacket(response);
                    break;
                case (short) PacketType.C2SExit:
                    Close();
                    break;
            }
            _kickTask.Inactive = 0;
            base.OnFinishPacketSent(packet);
        }

        public User User => _user;
    }
}