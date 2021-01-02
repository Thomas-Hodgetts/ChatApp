using System;
using System.Net;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Packets
{
    public enum PacketType
    {
        ChatMessage,
        PrivateMessage,
        ClientName,
        ErrorMessage,
        UserNameMessage,
        AnnouncementMessage,
        EmptyPacket,
        ClientData,
        LoginPacket,
        DisconnectPacket,
        ConversationPacket,
        RequestForPrivateChat,
        PublicKey,
        GameRequest,
        GameAccepted,
        GameUpdate,
        GameFinished,
        RPSPacket,
        RPSPacketPersonal,
        RPSPacketAction,
        RPSPacketResult,
        RPSSignal
    }

    [Serializable]
    public class Packet
    {
        public PacketType m_PacketType { get; protected set; }
    }

    [Serializable]
    public class ChatMessagePacket : Packet
    {
        public string m_Message;

        public ChatMessagePacket(string Message)
        {
            m_Message = Message;
            m_PacketType = PacketType.ChatMessage;
        }
    }
    [Serializable]
    public class ErrorMessagePacket : Packet
    {
        public string m_Message;

        public ErrorMessagePacket(string Message)
        {
            m_Message = Message;
            m_PacketType = PacketType.ErrorMessage;
        }
    }

    [Serializable]
    public class UserNamePacket : Packet
    {
        public string m_Username;

        public UserNamePacket(string username)
        {
            m_Username = username;
            m_PacketType = PacketType.UserNameMessage;
        }
    }

    [Serializable]
    public class AnnouncementPacket : Packet
    {
        public string m_Announcement;

        public AnnouncementPacket(string Annoucement)
        {
            m_Announcement = Annoucement;
            m_PacketType = PacketType.AnnouncementMessage;
        }
    }

    [Serializable]
    public class EmptyPacket : Packet
    {
        public EmptyPacket()
        {
            m_PacketType = PacketType.EmptyPacket;
        }
    }

    [Serializable]
    public class ClientData : Packet
    {
        public List<String> m_ClientNames;
        public ClientData()
        {
            m_ClientNames = new List<string>();
            m_PacketType = PacketType.ClientData;
        }
    }

    [Serializable]
    public class LoginPacket : Packet
    {
        public IPEndPoint m_IPEndPoint;

        public LoginPacket(IPEndPoint IPEP)
        {
            m_IPEndPoint = IPEP;
            m_PacketType = PacketType.LoginPacket;
        }
    }

    [Serializable]
    public class ConversationPackets : Packet
    {
        public string Who;
        public List<Packets.Packet> Conversation;
        public ConversationPackets(string user, List<Packets.Packet> C)
        {
            Who = user;
            m_PacketType = PacketType.ConversationPacket;
            Conversation = C;
        }
    }

    [Serializable]
    public class DisconnectPacket : Packet
    {
        public DisconnectPacket()
        {
            m_PacketType = PacketType.DisconnectPacket;
        }
    }

    [Serializable]
    public class RequestForPrivateChat : Packet
    {
        public string m_Target;
        public RequestForPrivateChat(string Target)
        {
            m_PacketType = PacketType.RequestForPrivateChat;
            m_Target = Target;
        }
    }

    [Serializable]
    public class PrivateMessage : Packet
    {
        public byte[] m_Message;
        public string m_Who;
        public PrivateMessage(string who, byte[] message)
        {
            m_PacketType = PacketType.PrivateMessage;
            m_Who = who;
            m_Message = message;
        }
    }

    [Serializable]
    public class PublicKeys : Packet
    {
        public RSAParameters m_PublicKey;
        public PublicKeys(RSAParameters PK)
        {
            m_PacketType = PacketType.PublicKey;
            m_PublicKey = PK;
        }
    }

    [Serializable]
    public class GameRequest : Packet
    {
        public string m_From;
        public string m_Who;
        public GameRequest(string From, string who)
        {
            m_PacketType = PacketType.GameRequest;
            m_Who = who;
            m_From = From;
        }
    }

    [Serializable]
    public class GameAccepted : Packet
    {
        public string m_From;
        public string m_Who;
        public bool m_Accepted;
        public GameAccepted(string From, string who, bool accepted)
        {
            m_PacketType = PacketType.GameAccepted;
            m_Who = who;
            m_From = From;
            m_Accepted = accepted;
        }
    }

    [Serializable]
    public class GameUpdate : Packet
    {
        public int m_PaddlePosX;
        public int m_BallX, m_BallY;
        public List<bool> m_BrickState;

        public GameUpdate(int PPX, int BX, int BY, List<bool>BL)
        {
            m_PacketType = PacketType.GameUpdate;
            m_PaddlePosX = PPX;
            m_BallX = BX;
            m_BallY = BY;
            m_BrickState = BL;
        }
    }

    [Serializable]
    public class GameFinished : Packet
    {
        public int m_TimeCompleted;
        public bool Winner;

        public GameFinished(int Time)
        {
            m_PacketType = PacketType.GameFinished;
            m_TimeCompleted = Time;
        }
    }

    [Serializable]
    public class PlayerDisconnected : Packet
    {
        public string m_Who;
        PlayerDisconnected(string who)
        {
            m_Who = who;
        }
    }

    [Serializable]
    public class RPSPacket : Packet
    {
        public string m_Choice;
        public RPSPacket(string Choice)
        {
            m_PacketType = PacketType.RPSPacket;
            m_Choice = Choice;
        }
    }

    [Serializable]
    public class RPSPacketAction : Packet
    {
        public string m_From;
        public string m_Choice;
        public RPSPacketAction(string Choice, string mFrom)
        {
            m_PacketType = PacketType.RPSPacketAction;
            m_From = mFrom;
            m_Choice = Choice;
        }
    }

    [Serializable]
    public class RPSPacketPersonal : Packet
    {
        public string m_Who;
        public string m_Choice;
        public RPSPacketPersonal(string Choice, string who)
        {
            m_PacketType = PacketType.RPSPacketPersonal;
            m_Who = who;
            m_Choice = Choice;
        }
    }

    [Serializable]
    public class RPSSignal : Packet
    {
        public string m_From;
        public RPSSignal(string sender)
        {
            m_PacketType = PacketType.RPSSignal;
            m_From = sender;
        }
    }

    [Serializable]
    public class RPSPacketResult : Packet
    {
        public string m_Who;
        public string m_Choice;
        public RPSPacketResult(string Choice, string who)
        {
            m_PacketType = PacketType.RPSPacketResult;
            m_Who = who;
            m_Choice = Choice;
        }
    }


}
