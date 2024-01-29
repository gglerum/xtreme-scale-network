using System;
using System.Globalization;
using System.IO.Ports;
using System.Timers;
using System.ComponentModel;
using System.Text.RegularExpressions;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using Timer = System.Timers.Timer;
using System.Windows.Threading;

namespace PC_XTREM
{

    public class Xtrem : INotifyPropertyChanged
    {

        //events
        public event EventHandler WeightChanged;
        public event EventHandler Recallscaledef;
        //public event EventHandler NameChanged;
        public event EventHandler NewStableWeight;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        public event EventHandler ConnectProgress;

        private const string StartSendingCommand = "\u000200FFE10110000\u0003\r\n";

        private const string AdcCtsWriteCommand = "\u000200FFR01110000\u0003\r\n";

        private const string VinCtsWriteCommand = "\u000200FFR02100000\u0003\r\n";

        private const string XTempWriteCommand = "\u000200FFR02020000\u0003\r\n";

        private const string GetScaleInfoCommand = "\u000200FFE10100000\u0003\r\n";
        private const int IdParam = 0x01;
        private const int TypeParam = 0x07;
        private const int SerialNumberParam = 0;
        private const int ResolutionParam = 0x28;
        private const int DecimalPlacesParam = 0x26;
        private const int ResFactorParam = 0x28;
        private const int CurMaxParam = 0x22;
        private const int CurEscParam = 0x23;
        private const int UnitParam = 0x20;
        private const int BaudRateCodeParam = 0x10;
        private const int OutputRateParam = 0x13;
        private const int FirmwareVersionParam = 0x08;
        private const int SealSwitchStateParam = 0x09;
        private const int RangeModeParam = 0x21;
        private const int NameParam = 0x500;
        private const int ZeroTrackingParam = 0x50;
        private const int ZeroTrackingRangeParam = 0x51;
        private const int ZeroInitParam = 0x52;
        private const int ZeroInitRangeParam = 0x53;
        private const int TareAutoParam = 0x61;
        private const int TareOnStabilityParam = 0x62;
        private const int TarModeParam = 0x60;
        private const int NegativeWeightParam = 0x29;
        private const int FilterLevelParam = 0x70;
        private const int FilterAnimalParam = 0x72;
        private const int StabilityRangeParam = 0x73;
        private const int WifiBoardCodeParam = 0x0A;
        private const int ApPasswordParam = 0x501;
        private const int ApIpAddressParam = 0x502;
        private const int ApDHCPParam = 0x503;
        private const int WifiApParam = 0x504;
        private const int StaSsidParam = 0x600;
        private const int StaPasswordParam = 0x601;
        private const int StaIpAddressParam = 0x602;
        private const int StaDHCPParam = 0x603;
        private const int TcpServerPortParam = 0x702;
        private const int UdpApRemotePortParam = 0x700;
        private const int UdpApLocalPortParam = 0x701;
        private const int InitZeroParam = 0x0030;
        private const int SlopeFactorParam = 0x0031;
        private const int MaxCountsParam = 0x0032;
        private const int GeoLocalParam = 0x0041;
        private const int GeoAdjustParam = 0x0042;
        private const int VinMinParam = 0x0002;
        private const int VinMaxParam = 0x0003;
        private const int VoutMinParam = 0x0004;
        private const int VoutMaxParam = 0x0005;
        private const int BullModeParam = 0x0015;

        //UDP comms
        public bool Udp = false;
        public UdpClient Listener;

        public Thread ReadUdpData;
        public bool StopThread = false;

        private IPEndPoint udpSendEndpoint;
        public IPEndPoint UdpSendEndpoint { get => udpSendEndpoint; set => udpSendEndpoint = value; }

        public int UdpRecPort;

        public bool IsWaitingData = true;

        //end UDP comms


        //communications error
        public Timer aTimer;
        private bool isNotConnected;
        private static bool ConnectState;
        public string rx_buffer = "";
        private static bool scale_info = false;

        private int id = 0xff;

        //Scale definition
        private int curEsc;
        private int decimalPlaces;
        public int ResFactor;

        //adjust information
        private string vInput;
        private long adcCts;
        private string maxCounts;

        //weighing information
        private double w_Brut = 0;
        private double w_Tare = 0;
        private double w_Net = 0;
        private string w_Display = "";
        private string w_Unit = "";

        private bool w_Flag_Zero = false;
        private bool w_Flag_Tare = false;
        private bool w_Flag_Stability = false;

        private DispatcherTimer StabilityTimer;
        private TimeSpan StabilityElapsedTime;
        private double stabilityTime;

        private bool w_Flag_NetoDisp = false;
        private bool w_Flag_HighRes = false;

        public static bool holdWeightChange;
        public double W_Hold;
        public bool HoldMode = false;

        private static readonly string[] Unit = { "", "g ", "kg", "oz", "lb" };

        public double W_Net
        {
            get => w_Net;
            set
            {
                w_Net = value;
                OnPropertyChanged("W_Net");
            }
        }

        public double W_Tare
        {
            get => w_Tare;
            set
            {
                w_Tare = value;
                OnPropertyChanged("W_Tare");
            }
        }


        public double W_Brut
        {
            get => w_Brut;
            set
            {
                w_Brut = value;
                OnPropertyChanged("W_Brut");
            }
        }

        public string VInput
        {
            get => vInput;
            set
            {
                vInput = value;
                OnPropertyChanged("VInput");
            }
        }

        public long AdcCts
        {
            get => adcCts;
            set
            {
                adcCts = value;
                OnPropertyChanged("AdcCts");
            }
        }

        public string W_Display
        {
            get => w_Display;
            set
            {
                w_Display = value;

                OnPropertyChanged("W_Display");
            }
        }

        public bool IsNotConnected
        {
            get => isNotConnected;
            set
            {
                isNotConnected = value;
                OnPropertyChanged("IsNotConnected");
            }
        }

        private string vinCts;
        private string vinVolt;

        public string VinCts
        {
            get => vinCts;
            set
            {
                vinCts = value;
                OnPropertyChanged("VinCts");
            }
        }

        public string VinVolt
        {
            get => vinVolt;
            set
            {
                vinVolt = value;
                OnPropertyChanged("VinVolt");
            }
        }

        protected virtual void OnWeightChanged()
        {
            WeightChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnRecallscaledef()
        {
            Recallscaledef?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnNewStableWeight()
        {
            NewStableWeight?.Invoke(this, EventArgs.Empty);
        }


        protected virtual void OnConnectProgress()
        {
            ConnectProgress?.Invoke(this, EventArgs.Empty);
        }

        private async void Listen_UDP_Async()
        {
            UdpReceiveResult rec;

            while (!StopThread)
            {
                try
                {

                    rec = await Listener.ReceiveAsync();

                    rx_buffer = System.Text.Encoding.ASCII.GetString(rec.Buffer);
                    //Console.WriteLine(udpSendEndpoint.ToString() + " " + rx_buffer);

                    if (IsWaitingData == false)
                    {
                        ParseWeightStream(rx_buffer.Substring(1, rx_buffer.Length - 4));
                    }

                    //Thread.Sleep(5);

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    //Console.WriteLine("Error en Listen_UDP_Async()");
                }


            }

            //Console.WriteLine("Stop thread ReadUdpData");

        }


        public void Init_Udp_Comms()
        {
            //UDP communication
            //byte[] _ip = udpSendEndpoint.Address.GetAddressBytes();

            IPEndPoint LocalIP = new IPEndPoint(address: IPAddress.Any, port: UdpRecPort);

            Listener = new UdpClient()
            {
                EnableBroadcast = false,
                ExclusiveAddressUse = false,

            };
            Listener.Client.Blocking = false;
            Listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            Listener.Client.Bind(LocalIP);
            Listener.Client.Connect(udpSendEndpoint);

            ReadUdpData = new Thread(Listen_UDP_Async);
            StopThread = false;
            ReadUdpData.IsBackground = true;
            ReadUdpData.Start();


        }

        public void Init_Scale()
        {
            ConnectState = false;

            IsWaitingData = false;

            //QR encoder
            //Encoder.ErrorCorrectionLevel = ErrorCorrectionLevel.H;

            //Start sending
            SendCommand(StartSendingCommand);


            // Create a timer and set a two second interval.
            aTimer = new Timer
            {
                Interval = 1000
            };

            // Have the timer fire repeated events (true is the default)
            aTimer.AutoReset = true;

            // Start the timer
            aTimer.Enabled = true;

            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;

            //Timer for stability elapsed time
            StabilityTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(50),

            };
            StabilityElapsedTime = TimeSpan.Zero;
            StabilityTimer.Tick += StabilityTimer_Tick;
        }

        private void StabilityTimer_Tick(object sender, EventArgs e)
        {

            StabilityElapsedTime += StabilityTimer.Interval;
            //throw new NotImplementedException();
        }

        public void GetScaleDef()
        {
            string _get;
            int unit = 0;
            int _rangemode;
            int datacount = 0;

            if (aTimer != null)
            {
                aTimer.Enabled = false;
            }

            scale_info = true;

            SendCommand(GetScaleInfoCommand);

            OnConnectProgress();
            Thread.Sleep(1);

            _get = Get_Param(IdParam, 5);
            if (_get != "-")
            {
                if (int.TryParse(_get, NumberStyles.AllowHexSpecifier, NumberFormatInfo.CurrentInfo, out int result))
                {
                    id = result;
                    datacount++;
                }
            }

            OnConnectProgress();
            Thread.Sleep(1);

            _get = Get_Param(ResolutionParam, 5);
            if (_get != "-")
            {
                datacount++;
                if (_get == "10")
                {
                    w_Flag_HighRes = true;
                }
                else
                {
                    w_Flag_HighRes = false;
                }
            }
            OnConnectProgress();
            Thread.Sleep(1);

            _get = Get_Param(DecimalPlacesParam, 5);
            if (_get != "-")
            {
                datacount++;
                decimalPlaces = Convert.ToInt32(_get);
                if (w_Flag_HighRes) decimalPlaces--;

            }
            OnConnectProgress();
            Thread.Sleep(1);

            _get = Get_Param(ResFactorParam, 5);
            if (_get != "-")
            {
                datacount++;
                ResFactor = Convert.ToInt32(_get);
            }
            OnConnectProgress();
            Thread.Sleep(1);

            int dec = decimalPlaces - (int)ResFactor / 10;

            _get = Get_Param(CurEscParam, 5);
            if (_get != "-")
            {
                datacount++;
                curEsc = Convert.ToInt32(_get);
            }
            OnConnectProgress();
            Thread.Sleep(1);

            _get = Get_Param(UnitParam, 5);
            if (_get != "-")
            {
                datacount++;
                unit = Convert.ToInt32(_get);
            }
            OnConnectProgress();
            Thread.Sleep(1);

            w_Unit = Unit[unit];

            double Esc = ((double)curEsc / Math.Pow(10, dec));
            double Min = (20 * Esc);

            _get = Get_Param(MaxCountsParam, 5);
            if (_get != "-")
            {
                datacount++;
                maxCounts = _get;
            }
            OnConnectProgress();
            Thread.Sleep(1);


            scale_info = true;
            isNotConnected = false;
            SendCommand(StartSendingCommand);

            if (aTimer != null)
            {
                aTimer.Enabled = true;
            }

        }

        public string Get_Param(int param, int cops)
        {

            string resposta_esperada;
            string data = "-";
            int l;
            int n_send = 0;


            ConnectState = false;

            //prepara comando lectura a XTREM
            string command = string.Format("\u000200{0:X2}R{1:X4}0000\u0003\r\n", 0xff, param);

            //respuesta esperada
            resposta_esperada = string.Format("\u0002..00r{0:X4}..*..\u0003", param);

            string datarec = "";

            //DataReceived recv = new DataReceived();

            int _cops = cops * 2;

            byte[] Udp_Send = System.Text.Encoding.UTF8.GetBytes(command);

            IsWaitingData = true;

            while (data == "-")
            {
                //rx_buffer = "";
                Listener.Send(Udp_Send, Udp_Send.Length, UdpSendEndpoint);

                bool stop = false;
                decimal milliseconds = DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond;
                decimal currentms = 0;
                decimal timeout = 200;
                while (!stop)
                {
                    if (rx_buffer.Length > 0)
                    {

                        if (Regex.IsMatch(rx_buffer, resposta_esperada) == true)
                        {
                            datarec = rx_buffer;
                            stop = true;
                        }

                    }

                    currentms = DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond;
                    if ((currentms - milliseconds) > timeout)
                    {
                        stop = true;
                    }

                }

                //Console.WriteLine("Stops before timeout ends " + (currentms - milliseconds) + "," + n_send);

                if (datarec.Length > 0)
                {
                    int first = Regex.Match(datarec, resposta_esperada).Index;
                    int len = Regex.Match(datarec, resposta_esperada).Length;
                    if (first > -1)
                    {
                        if (len > 11)
                        {
                            l = Convert.ToInt32(datarec.Substring(first + 10, 2), 16);

                            if (len >= 14 + l)
                            {
                                data = datarec.Substring(first + 12, l);

                            }
                        }
                    }
                    //rx_buffer = "";
                    //ConnectState = true;

                }


                if (data == "-")
                {
                    //Console.WriteLine("Get_Param(" + string.Format("0x{0:X4}", param) + ") error in " + udpSendEndpoint.Address + ":" + udpSendEndpoint.Port);
                }


                //Console.Write(string.Format("{1} {0:X4}", param, n_send) + " | " + string.Format("{0:X4}", recv.Address) + ":" + recv.Data + " | " + rx_buffer);


                n_send++;

                if (n_send > _cops)
                {
                    break;
                }
            }

            IsWaitingData = false;


            return data;
        }

        public int WriteParam(int param, string value)
        {
            string data = "";

            IsWaitingData = true;

            try
            {

                //Send write command
                string command = string.Format("\u000200{0:X2}W{1:X4}{2:X2}{3}00\u0003\r\n", id, param, value.Length, value);
                byte[] Udp_Send = System.Text.ASCIIEncoding.UTF8.GetBytes(command);

                DataReceived recv = new DataReceived();

                IPEndPoint local = new IPEndPoint(0, 0);
                int n_send = 0;
                int _cops = 5;
                if (param == 0x500 || param == 0x501 || param == 0x502 || param == 0x700 || param == 0x701 || param == 0x702)
                {
                    _cops = 20;
                }

                while (data.Length == 0)
                {
                    rx_buffer = "";

                    Listener.Send(Udp_Send, Udp_Send.Length, UdpSendEndpoint);

                    bool stop = false;
                    decimal milliseconds = DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond;
                    decimal currentms = 0;
                    decimal timeout = 200;
                    while (!stop)
                    {
                        if (rx_buffer.Length > 0)
                        {
                            recv = ParseDataReceived(rx_buffer);
                            if (recv.Address == param)
                            {
                                stop = true;
                            }

                        }

                        currentms = DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond;
                        if ((currentms - milliseconds) > timeout)
                        {
                            stop = true;
                        }

                    }

                    //Console.WriteLine("Stops before timeout ends " + (currentms - milliseconds) + "," + n_send);

                    if (recv.Address == param)
                    {
                        data = recv.Data;

                    }

                    n_send++;

                    if (n_send > _cops)
                    {
                        break;
                    }
                }

                isNotConnected = false;

                IsWaitingData = false;

                if (data == "0")
                {
                    return 0;
                }
                else
                {
                    return -1;
                }

            }
            catch (Exception ex)
            {
                isNotConnected = false;

                IsWaitingData = false;

                Console.WriteLine(ex.Message);
                //Console.WriteLine("Error en WriteParam() UDP");

                return 1;
            }
        }

        public void SendCommand(string command)
        {
            IsWaitingData = true;

            try
            {
                byte[] Udp_Send;
                string data = "";
                int param = int.Parse(command.Substring(6, 4), NumberStyles.AllowHexSpecifier, NumberFormatInfo.CurrentInfo);

                //Send command
                Udp_Send = System.Text.ASCIIEncoding.UTF8.GetBytes(command);

                Listener.Send(Udp_Send, Udp_Send.Length, UdpSendEndpoint);

                DataReceived recv = new DataReceived();
                IPEndPoint local = new IPEndPoint(0, 0);
                int n_send = 0;
                int _cops = 5;


                while (data.Length == 0)
                {

                    Listener.Send(Udp_Send, Udp_Send.Length, UdpSendEndpoint);

                    bool stop = false;
                    decimal milliseconds = DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond;
                    decimal currentms = 0;
                    decimal timeout = 200;
                    while (!stop)
                    {
                        if (rx_buffer.Length > 0)
                        {
                            recv = ParseDataReceived(rx_buffer);
                            if (recv.Address == param)
                            {
                                stop = true;
                            }

                        }

                        currentms = DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond;
                        if ((currentms - milliseconds) > timeout)
                        {
                            stop = true;
                        }

                    }

                    //onsole.WriteLine("Stops before timeout ends " + (currentms - milliseconds) + "," + n_send);


                    if (recv.Address == param)
                    {
                        data = recv.Data;
                        isNotConnected = false;

                    }

                    n_send++;

                    if (n_send > _cops)
                    {
                        break;
                    }
                }

                IsWaitingData = false;

            }
            catch (Exception ex)
            {
                isNotConnected = true;
                IsWaitingData = false;

                Console.WriteLine(ex.Message);
                //Console.WriteLine("Error en SendCommand() UDP");

            }


        }


        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {

            //Console.WriteLine("Timeout elapsed at {0}", e.SignalTime);

            if (ConnectState == false)
            {
                if (Udp == true && udpSendEndpoint.Address != null)
                {
                    if (ReadUdpData.IsAlive == false)
                    {
                        ReadUdpData = new Thread(Listen_UDP_Async);

                        StopThread = false;
                        ReadUdpData.IsBackground = true;
                        ReadUdpData.Start();
                    }

                    string check = Get_Param(0x100, 2);

                    if (check != "-")
                    {
                        IsNotConnected = false;

                        if (scale_info == false)
                        {
                            OnRecallscaledef();
                        }

                        SendCommand(StartSendingCommand);

                    }
                    else
                    {
                        IsNotConnected = true;
                        W_Display = "";
                    }
                }
            }
            else
            {
                if (IsNotConnected)
                {
                    OnRecallscaledef();
                }

                IsNotConnected = false;
            }

            ConnectState = false;

        }

        public struct DataReceived
        {
            public int Address;
            public string Data;
        }

        public DataReceived ParseDataReceived(string data_received)
        {
            DataReceived result = new DataReceived
            {
                Address = -1,
                Data = ""
            };

            if (data_received.Length < 17)
            {
                return result;
            }

            //device destination
            string dest_id = data_received.Substring(3, 2);
            if (dest_id != "00" && dest_id != "FF")
            {
                //Console.WriteLine("dest_id=" + dest_id);
                return result;
            }

            //Parameter code (Address)
            string address = data_received.Substring(6, 4);
            if (int.TryParse(address, NumberStyles.AllowHexSpecifier, NumberFormatInfo.CurrentInfo, out int ad) == true)
            {
                result.Address = ad;
            }

            //data length
            if (int.TryParse(data_received.Substring(10, 2), NumberStyles.AllowHexSpecifier, NumberFormatInfo.CurrentInfo, out int l) == false)
            {
                //Console.WriteLine("Parse subs(9,2)=" + data_received.Substring(9, 2));
                return result;
            }

            if (data_received.Length != l + 17)
            {
                //Console.WriteLine("data length = " + data_received.Length + " / l +13 = " + (l + 13));
                return result;
            }

            result.Data = data_received.Substring(12, l);

            return result;
        }

        public void ParseWeightStream(string data_received)
        {
            bool Flag_Change = false;
            bool CurrentFlag;
            string Disp_weight;

            //Device Id
            //string sender_id = cmd.Substring(0, 2);

            //device destination
            string dest_id = data_received.Substring(2, 2);
            if (dest_id != "00" && dest_id != "FF")
            {
                //Console.WriteLine("dest_id=" + dest_id);
                return;
            }

            //Console.WriteLine(data_received);

            //Parameter code (Address)
            string address = data_received.Substring(5, 4);


            //data length
            if (int.TryParse(data_received.Substring(9, 2), NumberStyles.AllowHexSpecifier, NumberFormatInfo.CurrentInfo, out int l) == false)
            {
                //Console.WriteLine("Parse subs(9,2)=" + data_received.Substring(9, 2));
                return;
            }

            if (data_received.Length != l + 13)
            {
                //Console.WriteLine("data length = " + data_received.Length + " / l +13 = " + (l + 13));
                return;
            }

            //data received
            //string data_rec = cmd.Substring(11, l);


            switch (address)
            {
                case "0107":

                    //decimal milliseconds = DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond;
                    //tlast = milliseconds - tlast;
                    //Console.WriteLine("tlast = " + tlast);
                    //tlast = milliseconds;

                    ConnectState = true;

                    //weighing flags
                    ushort Weight_status = 0;

                    if (ushort.TryParse(data_received.Substring(34, 3), NumberStyles.AllowHexSpecifier, NumberFormatInfo.CurrentInfo, out ushort result))
                    {
                        Weight_status = result;
                    }


                    CurrentFlag = Convert.ToBoolean(Weight_status & 1);
                    if (CurrentFlag != w_Flag_Zero)
                    {
                        Flag_Change |= true;
                    }
                    else
                    {
                        OnPropertyChanged("W_Flag_Zero");
                    }

                    _ = (CurrentFlag) == true
                        ? w_Flag_Zero = true
                        : w_Flag_Zero = false;

                    CurrentFlag = Convert.ToBoolean(Weight_status & 2);
                    if (CurrentFlag != w_Flag_Tare)
                    {
                        Flag_Change |= true;
                    }
                    else
                    {
                        OnPropertyChanged("W_Flag_Tare");
                    }

                    _ = (CurrentFlag) == true
                        ? w_Flag_Tare = true
                        : w_Flag_Tare = false;

                    CurrentFlag = Convert.ToBoolean(Weight_status & 8);
                    if (CurrentFlag != w_Flag_NetoDisp)
                    {
                        Flag_Change |= true;
                    }
                    else
                    {
                        OnPropertyChanged("W_Flag_NetoDisp");
                    }

                    _ = (CurrentFlag) == true
                        ? w_Flag_NetoDisp = true
                        : w_Flag_NetoDisp = false;

                    CurrentFlag = Convert.ToBoolean(Weight_status & 4);
                    if (CurrentFlag != w_Flag_Stability)
                    {
                        Flag_Change |= true;
                        if (CurrentFlag == true)
                        {
                            if (StabilityTimer != null)
                                StabilityTimer.Start();
                        }
                        else
                        {
                            if (StabilityTimer != null)
                                StabilityTimer.Stop();
                        }
                    }
                    else
                    {
                        OnPropertyChanged("W_Flag_Stability");
                    }

                    _ = (CurrentFlag) == true
                        ? w_Flag_Stability = true
                        : w_Flag_Stability = false;

                    if (w_Flag_Stability == false)
                    {
                        StabilityElapsedTime = TimeSpan.Zero;

                    }
                    else
                    {
                        if (StabilityElapsedTime > TimeSpan.FromMilliseconds(stabilityTime) && stabilityTime > 0)
                        {
                            StabilityElapsedTime = TimeSpan.Zero;
                            if (StabilityTimer != null)
                                StabilityTimer.Stop();
                            OnNewStableWeight();
                        }
                    }

                    CurrentFlag = Convert.ToBoolean(Weight_status & 0x20);
                    if (CurrentFlag != w_Flag_HighRes)
                    {
                        if (w_Flag_HighRes == true)
                        {
                            W_Hold = 0;
                        }
                        Flag_Change |= true;
                    }
                    else
                    {
                        OnPropertyChanged("W_Flag_HighRes");
                    }
                    _ = (CurrentFlag) == true
                        ? w_Flag_HighRes = true
                        : w_Flag_HighRes = false;


                    string w = data_received.Substring(12, 8);
                    if (w.IndexOf('.') > 0)
                    {
                        decimalPlaces = w.Length - w.IndexOf('.') - 1;
                    }
                    else
                    {
                        decimalPlaces = 0;
                    }


                    ResFactor = (Weight_status & 0x20) == 0x20 ? 10 : 1;

                    //hold mode
                    int dec = decimalPlaces - (int)ResFactor / 10;
                    double noload = 20 * curEsc / Math.Pow(10, dec);
                    if (w_Flag_Stability && (w_Net < noload))
                    {
                        holdWeightChange = true;

                    }

                    if (w_Flag_Stability && w_Net > (double)curEsc)
                    {

                        if (holdWeightChange == true || w_Net > W_Hold)       // && Scale.W_Net > holdWeight)
                        {
                            holdWeightChange = false;
                            W_Hold = W_Brut;
                        }
                    }

                    W_Brut = Convert.ToDouble(data_received.Substring(12, 8), NumberFormatInfo.InvariantInfo);

                    W_Tare = Convert.ToDouble(data_received.Substring(23, 8), NumberFormatInfo.InvariantInfo);
                    W_Net = W_Brut - W_Tare;
                    w_Unit = data_received.Substring(20, 2);
                    Disp_weight = w_Flag_NetoDisp
                        ? string.Format(new NumberFormatInfo() { NumberDecimalDigits = decimalPlaces, NumberDecimalSeparator = "." }, "{0:F} {1}", w_Net, w_Unit)
                        : string.Format(new NumberFormatInfo() { NumberDecimalDigits = decimalPlaces, NumberDecimalSeparator = "." }, "{0:F} {1}", W_Brut, w_Unit);


                    CurrentFlag = Convert.ToBoolean(w_Display == Disp_weight);
                    if (CurrentFlag == false)
                    {
                        Flag_Change |= true;
                    }



                    if (HoldMode == true)
                    {
                        W_Display = string.Format(new NumberFormatInfo() { NumberDecimalDigits = decimalPlaces, NumberDecimalSeparator = "." }, "{0:F} {1}", W_Hold, w_Unit);
                    }
                    else
                    {
                        W_Display = Disp_weight;
                    }


                    //QR Code 
                    string _weight_value;
                    if (Double.TryParse(w_Display.Substring(0, w_Display.Length - 2), NumberStyles.Any, NumberFormatInfo.InvariantInfo, out double w_value))
                    {

                        _weight_value = Convert.ToString(w_value, NumberFormatInfo.CurrentInfo);
                    }
                    else
                    {
                        _weight_value = w_Display;
                    }



                    //_weight_value = "-9999999,9";
                    //Encoder.TryEncode(string.Format("{0,12}",_weight_value), out W_qrCode);

                    break;

                case "0111":

                    Flag_Change = false;

                    if (w_Display != "ADC L" && w_Display != "ADC H")
                    {
                        //Console.WriteLine(data_received.Substring(11, l));

                        AdcCts = Convert.ToInt64(data_received.Substring(11, l));

                        double _VInput = (double)adcCts / Convert.ToDouble(maxCounts) * 10;
                        VInput = string.Format(new NumberFormatInfo() { NumberDecimalDigits = 3, NumberDecimalSeparator = "." }, "{0:F} mV", _VInput);
                    }

                    break;

                case "0210":

                    Flag_Change = false;

                    VinCts = data_received.Substring(11, l);
                    double _Vin = Convert.ToDouble(VinCts) / 71.8;
                    VinVolt = string.Format(new NumberFormatInfo() { NumberDecimalDigits = 1, NumberDecimalSeparator = "." }, "{0:F} Vcc", _Vin);

                    break;


                case "0100":

                    ConnectState = true;

                    //error flags
                    ushort error_status = ushort.Parse(data_received.Substring(11, 2), NumberStyles.AllowHexSpecifier);
                    error_status &= 0x1F;

                    if (error_status == 0)
                    {
                        break;
                    }


                    if (error_status == 1)
                    {
                        Disp_weight = "Error 01";       //Flash memory error
                    }
                    else if (error_status == 2)
                    {
                        Disp_weight = "Error 02";       //ADC fail
                    }
                    else if (error_status == 3)
                    {
                        Disp_weight = "Error 03";       //Load cell input signal out of range (>30mV)
                    }
                    else if (error_status == 4)
                    {
                        Disp_weight = "ADC H";          //Load cell input signal too high
                        VInput = "> 20 mV";
                        AdcCts = 8388608;

                    }
                    else if (error_status == 5)
                    {
                        Disp_weight = "ADC L";          //Load cell input signal too low
                        VInput = "< -20 mV";
                        AdcCts = -8388608;
                    }
                    else if (error_status == 7)
                    {
                        //Disp_weight = "-OL-        ";           //Over load, weight > Max+9e
                        Disp_weight = "Over Load   ";           //Over load, weight > Max+9e
                    }
                    else
                    {
                        Disp_weight = error_status == 8 ? "_ _ _ _ _ _ _ _ _ " : "Error";
                    }


                    CurrentFlag = Convert.ToBoolean(w_Display == Disp_weight);
                    if (CurrentFlag == false)
                    {
                        Flag_Change |= true;



                    }

                    W_Display = Disp_weight;

                    break;

                default:
                    break;
            }

            if (Flag_Change == true)
            {
                OnWeightChanged();

            }

        }
    }
}
