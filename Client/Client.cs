using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Timers;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;


namespace Client
{
    public class ClientSide
    {
        [STAThread]
        static void Main(string[] args)
       {
            Client client = new Client();
        }
    }
    public class Client
    {
        string m_Nickname;
        string m_View;
        private TcpClient tcpClient;
        private NetworkStream m_Stream;
        private BinaryWriter m_Writer;
        private BinaryReader m_Reader;
        private BinaryFormatter m_Formatter;
        private UserForm m_ClientForm;
        private Thread m_ProcessServerResponse;
        private Thread m_ProcessUDPServerResponse;
        private Thread m_ManageGameUpdate;
        private object m_ReadLock;
        private object m_RSALock;
        private object m_UDPLock;
        private object m_GameLock;
        private System.Timers.Timer m_CheckForMessages;
        private System.Timers.Timer m_GameUpdate;
        private UdpClient m_UDPClient;
        private RSACryptoServiceProvider m_RSACrypto;
        private RSAParameters m_PublicKey;
        private RSAParameters m_PrivateKey;
        private RSAParameters m_ServerKey;
        private string GameRequestUser;
        private bool m_Disconnect = false;
        private Game1 m_Game;
        private Packets.GameUpdate m_CachedUpdate;


        public Client()
        {
            m_ReadLock = new object();
            m_RSALock = new object();
            m_UDPLock = new object();
            tcpClient = new TcpClient();
            m_UDPClient = new UdpClient();
            m_Nickname = "";
            m_UDPClient.Connect("127.0.0.1", 4444);
            if (Connect("127.0.0.1", 4444))
            {
                m_RSACrypto = new RSACryptoServiceProvider();
                m_PrivateKey = m_RSACrypto.ExportParameters(true);
                m_PublicKey = m_RSACrypto.ExportParameters(false);
                Run();
            }
            else
            {
                Console.WriteLine("CLIENT END");
                Console.ReadLine();
            }

        }

        private byte[] Encrpt(byte[] Data)
        {
            lock (m_RSALock)
            {
                m_RSACrypto.ImportParameters(m_ServerKey);
                return m_RSACrypto.Encrypt(Data, true);
            }
        }
        private byte[] Decrpt(byte[] Data)
        {
            lock (m_RSALock)
            {
                m_RSACrypto.ImportParameters(m_PrivateKey);
                return m_RSACrypto.Decrypt(Data, true);
            }
        }

        public byte[] EncryptString(string Message)
        {
            byte[] message = UTF8Encoding.UTF8.GetBytes(Message);
            return Encrpt(message);
        }

        public string DecyrptString(byte[] Message)
        {
            byte[] message = Decrpt(Message);
            return UTF8Encoding.UTF8.GetString(message);
        }

        public void Login()
        {
            Packets.LoginPacket LP = new Packets.LoginPacket((IPEndPoint)m_UDPClient.Client.LocalEndPoint);
            Send(LP);
        }

        public void UdpSendMessage(Packets.Packet packets)
        {
            MemoryStream WMS = new MemoryStream();
            m_Formatter.Serialize(WMS, packets);
            byte[] buffer = WMS.GetBuffer();
            m_UDPClient.Send(buffer, buffer.Length);
        }

        private void UdpProcessServerResponse()
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
                while (true)
                {
                    if (!m_Disconnect)
                    {
                        lock (m_UDPLock)
                        {
                            byte[] NewMessage = m_UDPClient.Receive(ref endPoint);
                            MemoryStream RMS = new MemoryStream(NewMessage);
                            Packets.Packet NewPackets = m_Formatter.Deserialize(RMS) as Packets.Packet;
                            switch (NewPackets.m_PacketType)
                            {
                                case Packets.PacketType.GameUpdate:
                                    {
                                        if (m_Game != null)
                                            m_Game.IncommingPacket(NewPackets);
                                        break;
                                    }
                            }
                        }
                    }

                }
            }
            catch (SocketException e)
            {
                if (m_Disconnect)
                {

                }
                else
                {
                    Console.WriteLine("Client UDP Read Method exception: " + e.Message);
                }
                
            }
        }

        private void SetTimer(Client C)
        {
            m_CheckForMessages = new System.Timers.Timer(500);
            m_CheckForMessages.Elapsed += (sender, e) => OnTimedEvent(sender, e, C);
            m_CheckForMessages.AutoReset = true;
            m_CheckForMessages.Enabled = true;

        }

        public void UpdateGame()
        {
            if (m_Game != null)
            {
                Packets.Packet P = m_Game.ReturnPacket();
                switch (P.m_PacketType)
                {
                    case Packets.PacketType.GameUpdate:
                        {
                            Packets.GameUpdate GameUpdate = (Packets.GameUpdate)P;
                            if (!GameUpdate.Equals(m_CachedUpdate))
                            {
                                m_CachedUpdate = GameUpdate;
                                UdpSendMessage(P);
                            }
                            break;
                        }
                    case Packets.PacketType.GameFinished:
                        {
                            UdpSendMessage(P);
                            break;
                        }
                    default:
                        break;
                }
            }
            else
            {
                
            }
        }

        private void SetGameUpdate(Client C)
        {
            Thread.Sleep(300);
            m_GameUpdate = new System.Timers.Timer(100);
            m_GameUpdate.Elapsed += (sender, e) => UpdateGameData(sender, e, C);
            m_GameUpdate.AutoReset = true;
            m_GameUpdate.Enabled = true;
        }

        static void UpdateGameData(Object source, ElapsedEventArgs e, Client C)
        {

            object TL = new object();
            Monitor.Enter(TL);
            {
                C.UpdateGame();
                return;
            }
        }

        public void RequestMessages(string Who)
        {
            List<Packets.Packet> newChat = new List<Packets.Packet>();
            if (Who == "General")
            {
                m_View = "General";
                Packets.ConversationPackets RequestChat = new Packets.ConversationPackets(Who, newChat);
                Send(RequestChat);
            }
            else
            {
                m_View = Who;
                Packets.RequestForPrivateChat RFPC = new Packets.RequestForPrivateChat(Who);
                Send(RFPC);
                Thread.Sleep(500);
                Packets.ConversationPackets RequestChat = new Packets.ConversationPackets(Who, newChat);
                Send(RequestChat);
            }

        }

        static void OnTimedEvent(Object source, ElapsedEventArgs e, Client C)
        {
            object TL = new object();
            Monitor.Enter(TL);
            {
                C.ProcessServerResponse();
                return;
            }

        }

        public bool Connect(string ipAddress, int Port)
        {
            try
            {
                tcpClient.Connect("127.0.0.1", Port);
                m_Stream = tcpClient.GetStream();
                m_Reader = new BinaryReader(m_Stream);
                m_Writer = new BinaryWriter(m_Stream);
                m_Formatter = new BinaryFormatter();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
                return false;
            }
        }

        public bool HasNickname()
        {
            if (m_Nickname == "")
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        [STAThread]
        public void Run()
        {
            Login();
            CreateForm();
            ProcessServerResponse();
            Packets.PublicKeys PK = new Packets.PublicKeys(m_PublicKey);
            m_CachedUpdate = new Packets.GameUpdate(0,0,0,null);
            Send(PK);
            m_ProcessServerResponse = new Thread(() => SetTimer(this));
            m_ProcessUDPServerResponse = new Thread(() => UdpProcessServerResponse());
            m_ManageGameUpdate = new Thread(() => SetGameUpdate(this));
            m_ProcessServerResponse.Start();
            m_ProcessUDPServerResponse.Start();
            m_ClientForm.ShowDialog();
            Close();

        }

        [STAThread]
        public void CreateForm()
        {
            m_ClientForm = new UserForm(this);
            return;

        }

        public void ProcessServerResponse()
        {
                Packets.Packet NewResponse = Read(); 
                switch(NewResponse.m_PacketType)
                {
                    case Packets.PacketType.ChatMessage:
                        {
                            Packets.ChatMessagePacket CMP = (Packets.ChatMessagePacket)NewResponse;
                            m_ClientForm.UpdateChatWindow(CMP.m_Message);
                            break;
                        }
                    case Packets.PacketType.ErrorMessage:
                        {
                            Packets.ErrorMessagePacket EM = (Packets.ErrorMessagePacket)NewResponse;
                            m_ClientForm.GiveError(EM.m_Message);
                            Console.WriteLine("Error: " + EM.m_Message);
                            Close();
                            break;
                        }
                    case Packets.PacketType.AnnouncementMessage:
                        {
                            Packets.AnnouncementPacket AP = (Packets.AnnouncementPacket)NewResponse;
                            bool UserName = AP.m_Announcement.Contains(" has joined the chat.");
                            if (UserName)
                            {
                                string UN = AP.m_Announcement.Split()[0];
                                m_ClientForm.UpdateDropwDown(UN);
                            }
                            m_ClientForm.GiveAnnouncement(AP.m_Announcement);
                            break;
                        }
                    case Packets.PacketType.ClientData:
                        {
                            Packets.ClientData CD = (Packets.ClientData)NewResponse;
                            if (CD.m_ClientNames != null)
                            {
                                for (int i = 0; i < CD.m_ClientNames.Count(); i++)
                                {
                                    m_ClientForm.UpdateDropwDown(CD.m_ClientNames[i]);
                                }
                            }
                            break;
                        }
                    case Packets.PacketType.ConversationPacket:
                        {
                            Packets.ConversationPackets CP = (Packets.ConversationPackets)NewResponse;
                            foreach (Packets.Packet message in CP.Conversation)
                            {
                                switch (message.m_PacketType)
                                {
                                    case Packets.PacketType.ChatMessage:
                                        {
                                            Packets.ChatMessagePacket CMP = (Packets.ChatMessagePacket)message;
                                            m_ClientForm.UpdateChatWindow(CMP.m_Message);
                                            break;
                                        }
                                    case Packets.PacketType.AnnouncementMessage:
                                        {
                                            Packets.AnnouncementPacket AP = (Packets.AnnouncementPacket)message;
                                            m_ClientForm.GiveAnnouncement(AP.m_Announcement);
                                            break;
                                        }
                                    case Packets.PacketType.PrivateMessage:
                                        {
                                            Packets.PrivateMessage PM = (Packets.PrivateMessage)message;
                                            if (m_View == PM.m_Who || PM.m_Who == m_Nickname)
                                            {
                                                m_ClientForm.UpdateChatWindow(DecyrptString(PM.m_Message));
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            break;
                                        }

                                }
                            }
                            break;
                        }
                    case Packets.PacketType.PrivateMessage:
                        {
                            Packets.PrivateMessage PM = (Packets.PrivateMessage)NewResponse;
                            if (m_View == PM.m_Who || PM.m_Who == m_Nickname)
                            {
                                m_ClientForm.UpdateChatWindow(DecyrptString(PM.m_Message));
                            }
                            break;
                        }
                    case Packets.PacketType.PublicKey:
                        {
                            Packets.PublicKeys PK = (Packets.PublicKeys)NewResponse;
                            m_ServerKey = PK.m_PublicKey;
                            break;
                        }
                    case Packets.PacketType.GameRequest:
                        {
                            Packets.GameRequest GR = (Packets.GameRequest)NewResponse;
                            GameRequestUser = GR.m_From;
                            m_ClientForm.GameInvite();
                            m_ClientForm.GiveAnnouncement("You have been invited to a game by "+ GR.m_From +". Do you Accept?");
                            break;
                        }
                    case Packets.PacketType.GameAccepted:
                        {
                            using (m_Game = new Game1())
                            {
                                m_ManageGameUpdate.Start();
                                m_Game.Run();        
                            }
                            m_GameUpdate.Dispose();
                           
                            break;
                        }
                    case Packets.PacketType.RPSSignal:
                        {
                        Packets.RPSSignal S = (Packets.RPSSignal)NewResponse;
                            m_ClientForm.ReadyToRock(S.m_From);
                             break;
                        }
                    default:
                        {
                            break;
                        }
                }
        }

        public void Disconnect()
        {
            m_Disconnect = true;
            Packets.DisconnectPacket DC = new Packets.DisconnectPacket();
            Send(DC);

        }

        public void Close()
        {
            if (m_Game != null)
            {
                m_Game.Exit();
            }
            m_CheckForMessages.Stop();
            m_CheckForMessages.Dispose();
            m_ClientForm.Close();
            m_Stream.Close();
            m_Reader.Close();
            m_Writer.Close();
            tcpClient.Close();
            m_UDPClient.Close();
        }

        public void Send(Packets.Packet NewMessage)
        {
            if (!m_Disconnect)
            {
                if (NewMessage.m_PacketType == Packets.PacketType.PrivateMessage)
                {
                    Packets.PrivateMessage PM = (Packets.PrivateMessage)NewMessage;
                    PM.m_Message = Encrpt(PM.m_Message);
                    NewMessage = PM;
                }
                if (NewMessage.m_PacketType == Packets.PacketType.UserNameMessage)
                {
                    Packets.UserNamePacket UP = (Packets.UserNamePacket)NewMessage;
                    m_Nickname = UP.m_Username;
                }
                MemoryStream WMS = new MemoryStream();
                m_Formatter.Serialize(WMS, NewMessage);
                byte[] buffer = WMS.GetBuffer();
                m_Writer.Write(buffer.Length);
                m_Writer.Write(buffer);
                m_Writer.Flush();
            }
        }

        public Packets.Packet Read()
        {
            if (!m_Disconnect)
            {
                lock (m_ReadLock)
                {
                    int SizeOfMessage = 0;
                    if ((SizeOfMessage = m_Reader.ReadInt32()) != -1)
                    {
                        byte[] buffer = m_Reader.ReadBytes(SizeOfMessage);
                        MemoryStream RMS = new MemoryStream(buffer);
                        return m_Formatter.Deserialize(RMS) as Packets.Packet;
                    }
                    else
                    {
                        Packets.ErrorMessagePacket RE = new Packets.ErrorMessagePacket("ReadInt32 Determined the size of the Packet was -1");
                        return RE;
                    }
                }
            }
            else
            {
                Packets.ErrorMessagePacket RE = new Packets.ErrorMessagePacket("Client Disconnected");
                return RE;
            }
        }

        public void RespondToRequest(Packets.GameAccepted GA)
        {
            if (GA.m_Accepted)
            {
                GA.m_From = GameRequestUser;
                GA.m_Who = m_Nickname;
                Send(GA);
            }
        }

        public void RequestGame(string who)
        {
            Packets.GameRequest GR = new Packets.GameRequest(m_Nickname, who);
            Send(GR);
        }

    }
    public class Game1 : Game
    {
        public bool IsActive { get; protected set; }
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private List<ObjectClass> m_Bricks;
        private List<ObjectClass> m_CompetitorBricks;
        private List<ObjectClass> m_PlayerAndObjects;
        private List<ObjectClass> m_CompetitorsAndObjects;
        private Packets.GameUpdate m_NewestUpdate;
        private Packets.GameUpdate m_CurrentObjects;
        private Packets.GameFinished m_Finished;
        private bool IsGameFinished = false;
        private bool Submitted = false;
        private int TimeTaken = 0;
        private object m_UpdateLock;


        public Game1()
        {
            IsActive = true;
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            m_UpdateLock = new object();
        }

        public void Start()
        {
            Run();
        }

        public void IncommingPacket(Packets.Packet packet)
        {
            switch (packet.m_PacketType)
            {
                case Packets.PacketType.GameUpdate:
                    {
                        m_NewestUpdate = (Packets.GameUpdate)packet;
                        break;
                    }
            }

        }

        public Packets.Packet ReturnPacket()
        {
            switch (IsGameFinished)
            {
                case true:
                    {
                        Submitted = true;
                        return m_Finished;
                    }

                case false:
                    {
                        return m_CurrentObjects;
                    }
                default:
                    {
                        break;
                    }
            }
            return null;

        }

        public void Close()
        {
            IsActive = false;
            Exit();
        }

        protected override void Initialize()
        {

            // TODO: Add your initialization logic here
            m_Bricks = new List<ObjectClass>();
            m_CompetitorBricks = new List<ObjectClass>();
            m_PlayerAndObjects = new List<ObjectClass>();
            m_CompetitorsAndObjects = new List<ObjectClass>();

            Paddle Player = new Paddle();
            Player.IsPlayer(true);
            m_PlayerAndObjects.Add(Player);

            Paddle Competitor = new Paddle();
            Competitor.IsPlayer(false);
            m_CompetitorsAndObjects.Add(Competitor);

            Ball PlayerBall = new Ball();
            PlayerBall.m_PlayerControlled = false;
            PlayerBall.Active = false;
            m_PlayerAndObjects.Add(PlayerBall);

            Ball CBall = new Ball();
            CBall.m_PlayerControlled = false;
            m_CompetitorsAndObjects.Add(CBall);

            Vector2 baseVec = new Vector2(0, 0);

            for (int i = 0; i < 8; i++)
            {
                Brick b = new Brick();
                b.SetBrickPos(baseVec);
                baseVec.X += 300;
                if ((i % 4) == 0 && i > 1)
                {
                    baseVec.X = 0;
                    baseVec.Y += 46;
                }
                b.Active = true;
                m_Bricks.Add(b);
            }

            Vector2 baseVec2 = new Vector2(0, 0);
            for (int i = 0; i < 8; i++)
            {
                Brick b = new Brick();
                b.SetBrickPos(baseVec2);
                baseVec2.X += 300;
                if ((i % 4) == 0 && i > 1)
                {
                    baseVec2.X = 0;
                    baseVec2.Y += 46;
                }
                b.Active = true;
                m_CompetitorBricks.Add(b);
            }

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            // TODO: use this.Content to load your game content here
            m_PlayerAndObjects[0].m_Tex = Content.Load<Texture2D>("User");
            m_PlayerAndObjects[1].m_Tex = Content.Load<Texture2D>("Ball");

            m_CompetitorsAndObjects[0].m_Tex = Content.Load<Texture2D>("Competitor");
            m_CompetitorsAndObjects[1].m_Tex = Content.Load<Texture2D>("Ball");

            int texNum = 1;
            for (int i = 0; i < m_Bricks.Count; i++)
            {
                if (texNum > 3)
                {
                    texNum = 1;
                }
                switch (texNum)
                {
                    default:
                        {
                            m_Bricks[i].m_Tex = Content.Load<Texture2D>("Brick1");
                            m_CompetitorBricks[i].m_Tex = Content.Load<Texture2D>("Brick1");
                            break;
                        }
                    case 01:
                        {
                            m_Bricks[i].m_Tex = Content.Load<Texture2D>("Brick1");
                            m_CompetitorBricks[i].m_Tex = Content.Load<Texture2D>("Brick1");
                            break;
                        }
                    case 02:
                        {
                            m_Bricks[i].m_Tex = Content.Load<Texture2D>("Brick2");
                            m_CompetitorBricks[i].m_Tex = Content.Load<Texture2D>("Brick2");
                            break;
                        }
                    case 03:
                        {
                            m_Bricks[i].m_Tex = Content.Load<Texture2D>("Brick3");
                            m_CompetitorBricks[i].m_Tex = Content.Load<Texture2D>("Brick3");
                            break;
                        }
                }
                texNum++;
            }
        }

        protected override void Update(GameTime gameTime)
        {



            //Check for finish

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape) || !IsActive)
            {
                _graphics.Dispose();
                IsGameFinished = true;
                TimeTaken = 0;
                Close();
            }


            //Handle Enemy Graphics


            if (m_NewestUpdate != null)
            {
                Vector2 OppositionPos = m_CompetitorsAndObjects[0].ReturnLocale();
                OppositionPos.X = m_NewestUpdate.m_PaddlePosX;
                m_CompetitorsAndObjects[0].Update(m_NewestUpdate.m_PaddlePosX);
                Vector2 CurrentBallPos = new Vector2(m_NewestUpdate.m_BallX, m_NewestUpdate.m_BallY);
                Vector2 SuspectedBallPosition = new Vector2(OppositionPos.X + 150, OppositionPos.Y - 46);

                if (CurrentBallPos != SuspectedBallPosition && CurrentBallPos.Y > SuspectedBallPosition.Y + 5 || CurrentBallPos.Y < SuspectedBallPosition.Y - 5)
                {
                    if (!m_CompetitorsAndObjects[1].Active)
                    {
                        m_CompetitorsAndObjects[1].Active = true;
                    }
                    else
                    {
                        m_CompetitorsAndObjects[1].MoveY(-3);

                    }
                }
                else
                {
                    SuspectedBallPosition.X -= 150;
                    SuspectedBallPosition.Y += 46;
                    m_CompetitorsAndObjects[1].Active = false;
                    m_CompetitorsAndObjects[1].SetBallPos(SuspectedBallPosition);
                }

                for (int i = 0; i < m_CompetitorBricks.Count; i++)
                {
                    if (m_NewestUpdate.m_BrickState[i])
                    {
                        m_CompetitorBricks[i].Active = true;
                    }
                    else
                    {
                        m_CompetitorBricks[i].Active = false;
                    }
                }
            }



            //Handle local 
            m_PlayerAndObjects[1].UpdateRectangle(m_PlayerAndObjects[1].ReturnLocale());
            if (Keyboard.GetState().IsKeyDown(Keys.Space))
            {
                m_PlayerAndObjects[1].Active = true;
            }
            else if (!m_PlayerAndObjects[1].Active)
            {
                m_PlayerAndObjects[1].SetBallPos(m_PlayerAndObjects[0].ReturnLocale());
            }
            else
            {
                m_PlayerAndObjects[1].MoveY(-3);
                if (m_PlayerAndObjects[1].ReturnLocale().Y < -10)
                {
                    m_PlayerAndObjects[1].Active = false;
                }
            }

            if (Keyboard.GetState().IsKeyDown(Keys.A))
            {
                m_PlayerAndObjects[0].MoveX(-1);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.D))
            {
                m_PlayerAndObjects[0].MoveX(1);
            }

            //Collision
            if (m_PlayerAndObjects[1].Active)
            {
                for (int i = 0; i < m_Bricks.Count; i++)
                {
                    if (m_Bricks[i].Active)
                    {
                        if (m_Bricks[i].CheckForCollision(m_Bricks[i], m_PlayerAndObjects[1]))
                        {
                            m_Bricks[i].Active = false;
                            m_PlayerAndObjects[1].Active = false;
                        }
                    }
                }
            }

            List<bool> CheckIfCompleted = new List<bool>();
            int truecount = 0;
            for (int i = 0; i < m_Bricks.Count; i++)
            {
                if (!m_Bricks[i].Active)
                {
                    CheckIfCompleted.Add(false);
                    truecount++;
                }
                else
                {
                    CheckIfCompleted.Add(true);
                }

            }
            if (truecount == m_Bricks.Count - 2)
            {
                TimeTaken = (int)gameTime.TotalGameTime.TotalSeconds;
                m_Finished = new Packets.GameFinished(TimeTaken);
                IsGameFinished = true;
                if (Submitted)
                {
                    Close();
                }
            }
            Packets.GameUpdate GU = new Packets.GameUpdate((int)m_PlayerAndObjects[0].ReturnLocale().X, (int)m_PlayerAndObjects[1].ReturnLocale().X, (int)m_PlayerAndObjects[1].ReturnLocale().Y, CheckIfCompleted);
            m_CurrentObjects = GU;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here
            _spriteBatch.Begin();

            if (m_NewestUpdate != null)
            {
                _spriteBatch.Draw(m_CompetitorsAndObjects[0].m_Tex, m_CompetitorsAndObjects[0].ReturnLocale(), new Color(Color.White, 0.5f));

                for (int i = 0; i < m_CompetitorBricks.Count; i++)
                {
                    if (m_CompetitorBricks[i].Active)
                    {
                        _spriteBatch.Draw(m_CompetitorBricks[i].m_Tex, m_CompetitorBricks[i].ReturnLocale(), new Color(Color.White, 0.5f));
                    }
                }
                if (m_CompetitorsAndObjects[1].Active)
                {
                    _spriteBatch.Draw(m_CompetitorsAndObjects[1].m_Tex, m_CompetitorsAndObjects[1].ReturnLocale(), new Color(Color.White, 0.5f));
                }

            }


            _spriteBatch.Draw(m_PlayerAndObjects[0].m_Tex, m_PlayerAndObjects[0].ReturnLocale(), Color.White);

            for (int i = 0; i < m_Bricks.Count; i++)
            {
                if (m_Bricks[i].Active)
                {
                    _spriteBatch.Draw(m_Bricks[i].m_Tex, m_Bricks[i].ReturnLocale(), Color.White);
                }
            }
            if (m_PlayerAndObjects[1].Active)
            {
                _spriteBatch.Draw(m_PlayerAndObjects[1].m_Tex, m_PlayerAndObjects[1].ReturnLocale(), Color.White);
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}


