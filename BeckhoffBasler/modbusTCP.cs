using System;
using System.Collections;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;

// Opening a TCP connection in C# with a custom timeout  https://www.splinter.com.au/opening-a-tcp-connection-in-c-with-a-custom-t/


namespace ModbusTCP
{
    /// <summary>
    /// Modbus TCP common driver class. This class implements a modbus TCP master driver.
    /// It supports the following commands:
    /// 
    /// Read coils
    /// Read discrete inputs
    /// Write single coil
    /// Write multiple cooils
    /// Read holding register
    /// Read input register
    /// Write single register
    /// Write multiple register
    /// 
    /// All commands can be sent in synchronous or asynchronous mode. If a value is accessed
    /// in synchronous mode the program will stop and wait for slave to response. If the 
    /// slave didn't answer within a specified time a timeout exception is called.
    /// The class uses multi threading for both synchronous and asynchronous access. For
    /// the communication two lines are created. This is necessary because the synchronous
    /// thread has to wait for a previous command to finish.
    /// 
    /// </summary>
    public class Master
    {
        // ------------------------------------------------------------------------
        // Constants for access
        private const byte fctReadCoil = 1;
        private const byte fctReadDiscreteInputs = 2;
        private const byte fctReadHoldingRegister = 3;
        private const byte fctReadInputRegister = 4;
        private const byte fctWriteSingleCoil = 5;
        private const byte fctWriteSingleRegister = 6;
        private const byte fctWriteMultipleCoils = 15;
        private const byte fctWriteMultipleRegister = 16;
        private const byte fctReadWriteMultipleRegister = 23;

        /// <summary>Constant for exception illegal function.</summary>
        public const byte excIllegalFunction = 1;
        /// <summary>Constant for exception illegal data address.</summary>
        public const byte excIllegalDataAdr = 2;
        /// <summary>Constant for exception illegal data value.</summary>
        public const byte excIllegalDataVal = 3;
        /// <summary>Constant for exception slave device failure.</summary>
        public const byte excSlaveDeviceFailure = 4;
        /// <summary>Constant for exception acknowledge.</summary>
        public const byte excAck = 5;
        /// <summary>Constant for exception slave is busy/booting up.</summary>
        public const byte excSlaveIsBusy = 6;
        /// <summary>Constant for exception gate path unavailable.</summary>
        public const byte excGatePathUnavailable = 10;
        /// <summary>Constant for exception not connected.</summary>
        public const byte excExceptionNotConnected = 253;
        /// <summary>Constant for exception connection lost.</summary>
        public const byte excExceptionConnectionLost = 254;
        /// <summary>Constant for exception response timeout.</summary>
        public const byte excExceptionTimeout = 255;
        /// <summary>Constant for exception wrong offset.</summary>
        private const byte excExceptionOffset = 128;
        /// <summary>Constant for exception send failt.</summary>
        private const byte excSendFailt = 100;
        /// <summary>Constant for exception not connected.</summary>
        public const byte excFailedSetKeepAlive = 252;

        // ------------------------------------------------------------------------
        /// <summary>Response data event. This event is called when new data arrives</summary>
        public delegate void ResponseData(ushort id, byte unit, byte function, byte[] data);
        /// <summary>Response data event. This event is called when new data arrives</summary>
        public event ResponseData OnResponseData;
        /// <summary>Exception data event. This event is called when the data is incorrect</summary>
        public delegate void ExceptionData(ushort id, byte unit, byte function, byte exception);
        /// <summary>Exception data event. This event is called when the data is incorrect</summary>
        public event ExceptionData OnException;

        // ------------------------------------------------------------------------
        // Private declarations
        private static ushort _timeout = 1000;  // original 500
        private static ushort _refresh = 10;
        private static bool _connected = false;

        private Socket tcpAsyCl;
        private byte[] tcpAsyClBuffer = new byte[2048];

        private Socket tcpSynCl;
        private byte[] tcpSynClBuffer = new byte[2048];

        private string iplocal;
        private ushort portlocal;
        private bool bReady;

        private Stopwatch[] timers = new Stopwatch[5];
        private BackgroundWorker[] backworkers = new BackgroundWorker[5];
        // 0 - delay up to Constructor's termination
        // 1 - life guard
        private long ConstructorTerminationInterval = 500;
        private long DisposeTerminationInterval = 500;
        private long LifeGuardInterval = 5000;
        private const bool bReconnectPossibility = false;


        System.Drawing.Color fcnorm = System.Drawing.Color.Black;
        System.Drawing.Color fcerr = System.Drawing.Color.White;
        System.Drawing.Color bcnorm = System.Drawing.Color.White;
        System.Drawing.Color bcnorm1 = System.Drawing.Color.Green;
        System.Drawing.Color bcerr = System.Drawing.Color.Red;

        // ------------------------------------------------------------------------
        /// <summary>Response timeout. If the slave didn't answers within in this time an exception is called.</summary>
        /// <value>The default value is 500ms.</value>
        public ushort timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        // ------------------------------------------------------------------------
        /// <summary>Refresh timer for slave answer. The class is polling for answer every X ms.</summary>
        /// <value>The default value is 10ms.</value>
        public ushort refresh
        {
            get { return _refresh; }
            set { _refresh = value; }
        }

        // ------------------------------------------------------------------------
        /// <summary>Shows if a connection is active.</summary>
        public bool connected
        {
            get { return _connected; }
        }

        // ------------------------------------------------------------------------
        /// <summary>Create master instance without parameters.</summary>
        public Master()
        {
        }

        // ------------------------------------------------------------------------
        /// <summary>Create master instance with parameters.</summary>
        /// <param name="ip">IP adress of modbus slave.</param>
        /// <param name="port">Port number of modbus slave. Usually port 502 is used.</param>
        public Master(string ip, ushort port)
        {
            for (int i = 0; i < timers.Length; i++)
                timers[i] = new Stopwatch();

            for (int i = 0; i < backworkers.Length; i++)
            {
                backworkers[i] = new BackgroundWorker();
                backworkers[i].WorkerReportsProgress = true;
                backworkers[i].WorkerSupportsCancellation = true;
                backworkers[i].DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker_DoWork);
                //backworkers[i].ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker_ProgressChanged);
                backworkers[i].RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker_RunWorkerCompleted);
            }

            connect(ip, port);
        }


        /// <summary>
        /// Sets the keep-alive interval for the socket.
        /// </summary>
        /// <param name="socket">The socket.</param>
        /// <param name="time">Time between two keep alive "pings".</param>
        /// <param name="interval">Time between two keep alive "pings" when first one fails.</param>
        /// <returns>If the keep alive infos were succefully modified.</returns>
        /// https://csharp.hotexamples.com/examples/-/Socket/SetSocketOption/php-socket-setsocketoption-method-examples.html
        /// https://www.codeproject.com/Articles/117557/Set-Keep-Alive-Values
        /// 
        private bool SetKeepAlive(Socket socket, ulong time, ulong interval) // static
        {
            try
            {
                // Array to hold input values.
                var input = new[]
                {
                    (time == 0 || interval == 0) ? 0UL : 1UL, // on or off
                    time,
                    interval
                };
                
                int BitsPerByte = 8;
                int BytesPerLong = 4; // sizeof(long);
                byte[] inValue = new byte[3 * BytesPerLong]; // Pack input into byte struct.
                for (int i = 0; i < input.Length; i++)
                {
                    inValue[i * BytesPerLong + 3] = (byte)(input[i] >> ((BytesPerLong - 1) * BitsPerByte) & 0xff);
                    inValue[i * BytesPerLong + 2] = (byte)(input[i] >> ((BytesPerLong - 2) * BitsPerByte) & 0xff);
                    inValue[i * BytesPerLong + 1] = (byte)(input[i] >> ((BytesPerLong - 3) * BitsPerByte) & 0xff);
                    inValue[i * BytesPerLong + 0] = (byte)(input[i] >> ((BytesPerLong - 4) * BitsPerByte) & 0xff);
                }

                // Create byte struct for result (bytes pending on server socket).
                byte[] outValue = BitConverter.GetBytes(0);

                // Write SIO_VALS to Socket IOControl.
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.IOControl(IOControlCode.KeepAliveValues, inValue, outValue);
            }
            catch (SocketException e)
            {
                //Console.WriteLine("Failed to set keep-alive: {0} {1}", e.ErrorCode, e);
                return false;
            }
            return true;
        }


        // ------------------------------------------------------------------------
        /// <summary>Start connection to slave.</summary>
        /// <param name="ip">IP adress of modbus slave.</param>
        /// <param name="port">Port number of modbus slave. Usually port 502 is used.</param>
        public void connect(string ip, ushort port)
        {
            bool bKeepAlive = false;
            try
            {
                IPAddress _ip;
                if (IPAddress.TryParse(ip, out _ip) == false)
                {
                    IPHostEntry hst = Dns.GetHostEntry(ip);
                    ip = hst.AddressList[0].ToString();
                }

                iplocal = ip;
                portlocal = port;

                // ----------------------------------------------------------------
                // Connect asynchronous client
                tcpAsyCl = new Socket(IPAddress.Parse(ip).AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                bKeepAlive = SetKeepAlive(tcpAsyCl, 36000000, 1000); //Set 10 Hours: 10 * 60 * 60 * 1000 = 36,000,000 every 1 Second 1000

                if (bKeepAlive)
                {
                    //IPEndPoint localEP = new IPEndPoint(IPAddress.Parse(ip), port); //IPAddress.Loopback
                    //tcpAsyCl.Connect(localEP);

                    tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(ip), port));

                    tcpAsyCl.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, _timeout);
                    tcpAsyCl.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, _timeout);
                    tcpAsyCl.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, 1);

                    tcpAsyCl.LingerState = new LingerOption(true, 1); // 10
                    tcpAsyCl.NoDelay = true; // Disable the Nagle Algorithm for this tcp socket.
                                             //tcpAsyCl.ExclusiveAddressUse = true; // Don't allow another socket to bind to this port.
                                             //tcpAsyCl.ReceiveBufferSize = 8192; // Set the receive buffer size to 8k
                                             //tcpAsyCl.SendBufferSize = 8192; // Set the send buffer size to 8k.
                                             //tcpAsyCl.Ttl = 42; // Set the Time To Live(TTL) to 42 router hops.
                    tcpAsyCl.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, true);

                    // ----------------------------------------------------------------
                    // Connect synchronous client
                    tcpSynCl = new Socket(IPAddress.Parse(ip).AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    bKeepAlive = false;
                    bKeepAlive = SetKeepAlive(tcpSynCl, 36000000, 1000);
                    if (bKeepAlive)
                    {
                        tcpSynCl.Connect(new IPEndPoint(IPAddress.Parse(ip), port));
                        tcpSynCl.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, _timeout);
                        tcpSynCl.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, _timeout);
                        tcpSynCl.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, 1);
                        tcpSynCl.LingerState = new LingerOption(true, 1); // 10
                        tcpSynCl.NoDelay = true;
                        tcpSynCl.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, true);

                        _connected = true;

                        timers[1].Restart();
                        backworkers[1].RunWorkerAsync();
                    }
                }
                if (!bKeepAlive)
                {
                    _connected = false;
                    timers[0].Restart();
                    backworkers[0].RunWorkerAsync();
                            //CallException(1, 0, 0, excFailedSetKeepAlive);
                }
            }
            catch (System.IO.IOException error)
            {
                _connected = false;
                throw (error);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Stop connection to slave.</summary>
        public void disconnect()
        {
            Dispose();
        }

        // ------------------------------------------------------------------------
        /// <summary>Destroy master instance.</summary>
        ~Master()
        {
            Dispose();
        }

        // ------------------------------------------------------------------------
        /// <summary>Destroy master instance</summary>
        public void Dispose()
        {
            if (tcpAsyCl != null)
            {
                if (tcpAsyCl.Connected)
                {
                    try { tcpAsyCl.Shutdown(SocketShutdown.Both); }
                    catch { }
                    tcpAsyCl.Close();
                }
                tcpAsyCl = null;
            }
            if (tcpSynCl != null)
            {
                if (tcpSynCl.Connected)
                {
                    try { tcpSynCl.Shutdown(SocketShutdown.Both); }
                    catch { }
                    tcpSynCl.Close();
                }
                tcpSynCl = null;
            }

            for (int i = 0; i < timers.Length; i++)
                timers[i].Stop();
            for (int i = 0; i < backworkers.Length; i++)
                backworkers[i].CancelAsync();

            timers[0].Restart();
            while (timers[0].ElapsedMilliseconds < DisposeTerminationInterval)  Thread.Sleep(50);
            timers[0].Stop();
        }


        internal void CallException(ushort id, byte unit, byte function, byte exception)
        {
            bool bErr = true;
            if ((tcpAsyCl == null) || (tcpSynCl == null)) return; // &&
            if (exception == excExceptionConnectionLost) {
                //if (!tcpAsyCl.Connected && iplocal != "" && portlocal != 0) {
                //    tcpAsyCl.Disconnect(true);
                //    tcpAsyCl = new Socket(IPAddress.Parse(iplocal).AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                //    tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));
                //    if (tcpAsyCl.Connected) bErr = false;
                //}
                ////tcpSynCl = null;
                ////tcpAsyCl = null;
            }
            if(OnException != null && bErr)
                OnException(id, unit, function, exception);
        }


        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // This event handler is where the actual work is done.
            // This method runs on the background thread.

            // Get the BackgroundWorker object that raised this event.
            System.ComponentModel.BackgroundWorker worker;
            worker = (System.ComponentModel.BackgroundWorker)sender;

            if (worker == backworkers[0])
            {
                while (timers[0].ElapsedMilliseconds < ConstructorTerminationInterval) Thread.Sleep(10);
            }
            else if (worker == backworkers[1])
            {
                while (timers[1].ElapsedMilliseconds < LifeGuardInterval) Thread.Sleep(10);
            }
        }

        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // This method runs on the main thread.
            //Words.CurrentState state = (Words.CurrentState)e.UserState;
            //this.LinesCounted.Text = state.LinesCounted.ToString();
            //this.WordsCounted.Text = state.WordsMatched.ToString();
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // This event handler is called when the background thread finishes.
            // This method runs on the main thread.

            bool bException = false;

            System.ComponentModel.BackgroundWorker worker;
            worker = (System.ComponentModel.BackgroundWorker)sender;

            if (worker == backworkers[0])
            {
                bException = true;
                CallException(1, 0, 0, excFailedSetKeepAlive);
            }
            else if (worker == backworkers[1])
            {
                if (tcpAsyCl != null && tcpAsyCl.Connected || tcpSynCl != null && tcpSynCl.Connected)
                {
                    byte[] write_data = CreateReadHeader(1, 0, Convert.ToUInt16("2048"), 1, fctReadCoil);

                    if (tcpAsyCl != null && tcpAsyCl.Connected)
                    {
                        // ReadCoils(1, 0, Convert.ToUInt16("2048"), 1);
                        //WriteAsyncData(CreateReadHeader(1, 0, Convert.ToUInt16("2048"), 1, fctReadCoil), 1);
                        try
                        {
                            tcpAsyCl.BeginSend(write_data, 0, write_data.Length, SocketFlags.None, new AsyncCallback(OnSend), tcpAsyCl);
                            tcpAsyCl.BeginReceive(tcpAsyClBuffer, 0, tcpAsyClBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceiveLifeGuard), tcpAsyCl);
                        }
                        catch
                        {
                            if (!bException)
                            {
                                bException = true;
                                CallException(1, write_data[6], write_data[7], excExceptionConnectionLost); //2
                            }
                        }
                    }

                    if (tcpSynCl != null && tcpSynCl.Connected)
                    {
                        try
                        {
                            tcpSynCl.Send(write_data, 0, write_data.Length, SocketFlags.None);
                            int result = tcpSynCl.Receive(tcpSynClBuffer, 0, tcpSynClBuffer.Length, SocketFlags.None);

                            byte unit = tcpSynClBuffer[6];
                            byte function = tcpSynClBuffer[7];
                            byte[] data;

                            if (result == 0)
                            {
                                if (!bException)
                                {
                                    bException = true;
                                    CallException(1, unit, write_data[7], excExceptionConnectionLost); //1
                                }
                            }

                            // Response data is slave exception ------------------------------------------------------------
                            if (function > excExceptionOffset)
                            {
                                function -= excExceptionOffset;
                                bException = true;
                                CallException(1, unit, function, tcpSynClBuffer[8]);
                            }
                            // Write response data ------------------------------------------------------------
                            else if ((function >= fctWriteSingleCoil) && (function != fctReadWriteMultipleRegister))
                            {
                                data = new byte[2];
                                Array.Copy(tcpSynClBuffer, 10, data, 0, 2);
                            }
                            // Read response data ------------------------------------------------------------
                            else
                            {
                                data = new byte[tcpSynClBuffer[8]];
                                Array.Copy(tcpSynClBuffer, 9, data, 0, tcpSynClBuffer[8]);
                            }
                            //return data;
                        }
                        catch (SystemException)
                        {
                            if (!bException)
                            {
                                bException = true;
                                CallException(1, write_data[6], write_data[7], excExceptionConnectionLost); //3
                            }
                        }
                    }

                    if (!bException)
                    {
                        timers[1].Restart();
                        backworkers[1].RunWorkerAsync();
                    }
                }


                int status = 0; bool M229 = false, M300 = false, M310 = false, M315 = false;
                string s1 = "", s2 = "", s3 = "", s4 = "", s5 = "";
                System.Drawing.Color fc1 = fcnorm, fc2 = fcnorm, fc3 = fcnorm, fc4 = fcnorm, fc5 = fcnorm;
                System.Drawing.Color bc1 = bcnorm1, bc2 = bcnorm, bc3 = bcnorm, bc4 = bcnorm, bc5 = bcnorm;

                if (bException)
                {
                    s1 = "Controller not connected"; fc1 = fcerr; bc1 = bcerr;
                } else {
                    int rc = GetErrCode(ref status, ref M229, ref M300, ref M310, ref M315);
                    if (status == -1) { s1 = "Controller not connected"; fc1 = fcerr; bc1 = bcerr; }
                    else if (status == -2) { s1 = "Controller IO read error"; fc1 = fcerr; bc1 = bcerr; }
                    else if (status == -3) { s1 = "Not in the cycle"; fc1 = fcnorm; bc1 = bcnorm; }
                    else if (status == 0) { s1 = "In the cycle"; fc1 = fcnorm; bc1 = bcnorm1; }

                    if (M229) { s2 = "Conveyor border crossed"; fc2 = fcerr; bc2 = bcerr; }
                    if (M300) { s3 = "No Air Pressure"; fc3 = fcerr; bc3 = bcerr; }
                    if (M310) { s4 = "Graphite conveyor Error"; fc4 = fcerr; bc4 = bcerr; }
                    if (M315) { s5 = "Plastic conveyor Error"; fc5 = fcerr; bc5 = bcerr; }
                }
                SumoNewMachine.MainHMI.NewMainHMI.lblErrCode1.Text = s1; SumoNewMachine.MainHMI.NewMainHMI.lblErrCode1.BackColor = bc1; SumoNewMachine.MainHMI.NewMainHMI.lblErrCode1.ForeColor = fc1;
                SumoNewMachine.MainHMI.NewMainHMI.lblErrCode2.Text = s2; SumoNewMachine.MainHMI.NewMainHMI.lblErrCode2.BackColor = bc2; SumoNewMachine.MainHMI.NewMainHMI.lblErrCode1.ForeColor = fc2;
                SumoNewMachine.MainHMI.NewMainHMI.lblErrCode3.Text = s3; SumoNewMachine.MainHMI.NewMainHMI.lblErrCode3.BackColor = bc3; SumoNewMachine.MainHMI.NewMainHMI.lblErrCode1.ForeColor = fc3;
                SumoNewMachine.MainHMI.NewMainHMI.lblErrCode4.Text = s4; SumoNewMachine.MainHMI.NewMainHMI.lblErrCode4.BackColor = bc4; SumoNewMachine.MainHMI.NewMainHMI.lblErrCode1.ForeColor = fc4;
                SumoNewMachine.MainHMI.NewMainHMI.lblErrCode5.Text = s5; SumoNewMachine.MainHMI.NewMainHMI.lblErrCode5.BackColor = bc5; SumoNewMachine.MainHMI.NewMainHMI.lblErrCode1.ForeColor = fc5;

                //if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.RedLightBit, 1, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                string s = "";
                if (M300 || M310 || M315)
                {
                    SumoNewMachine.MainHMI.NewMainHMI.MBmasterWriteM(SumoNewMachine.MyStatic.DigitalOutput.RedLightBit, 1, ref s);
                }
                //else
                //{
                //    SinteringUnloading.MainHMI.NewMainHMI.MBmasterWriteM(SinteringUnloading.MyStatic.DigitalOutput.RedLightBit, 0, ref s);
                //}
            }
        }


        private int GetErrCode(ref int status, ref bool M229, ref bool M300, ref bool M310, ref bool M315)
        {
            //if (!SinteringUnloading.MyStatic.bStartCycle) return (-1); // || MBmaster == null
            ushort ID = 1;
            byte unit = 0;
            ushort StartAddress = 2048;
            ushort offset = 229; // 300;
            StartAddress += offset;
            int nLength = 88; // 16;
            byte Length = Convert.ToByte(nLength);
            bool[] result = null;
            bool bRead = false;
            bRead = ReadCoils(ID, unit, StartAddress, Length, ref result);
            if (!bRead) {status = -1; return (-1);}

            int nUpperBound = -1;
            if (bRead) nUpperBound = result.GetUpperBound(0);
            if (nUpperBound == -1 || nUpperBound < nLength - 1) { status = -2; return (-1); } // IO read error

            if (!SumoNewMachine.MyStatic.bStartCycle) { status = -3; } // not in cycle

            int i = 0; if (result[i]) M229 = true; // alarm M229 (M238)
            i = 71; if (result[i]) M300 = true; // alarm M300 62
            i = 81; if (result[i]) M310 = true; // alarm M310 72
            i = 86; if (result[i]) M315 = true; // alarm M315 77
            return 0;
        }


        internal static UInt16 SwapUInt16(UInt16 inValue)
        {
            return (UInt16)(((inValue & 0xff00) >> 8) | ((inValue & 0x00ff) << 8));
        }

        // ------------------------------------------------------------------------
        /// <summary>Read coils from slave asynchronous. The result is given in the response function.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="numInputs">Length of data.</param>
        public void ReadCoils(ushort id, byte unit, ushort startAddress, ushort numInputs)
        {
            if (tcpAsyCl == null)
                return;
            else
            {
                if (bReconnectPossibility && !tcpAsyCl.Connected && iplocal != "" && portlocal != 0)
                    tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));

                WriteAsyncData(CreateReadHeader(id, unit, startAddress, numInputs, fctReadCoil), id);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Read coils from slave synchronous.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="numInputs">Length of data.</param>
        /// <param name="values">Contains the result of function.</param>

        public bool ReadCoils(
            ushort id, byte unit, ushort startAddress, ushort numInputs, ref bool[] result)
        {
            if (tcpSynCl == null)
                return false;
            else
            {
                byte[] values = null;
                values = WriteSyncData(CreateReadHeader(id, unit, startAddress, numInputs, fctReadCoil), id);
                if (values == null)
                {
                    Thread.Sleep(5);
                    values = WriteSyncData(CreateReadHeader(id, unit, startAddress, numInputs, fctReadCoil), id);
                    if (values == null)
                        return false;
                }
                BitArray bitArray = new BitArray(values);
                result = new bool[bitArray.Count];
                bitArray.CopyTo(result, 0);
                return true;
            }
        }

        public bool ReadCoils(
            ushort id, byte unit, ushort startAddress, ushort numInputs, ref bool[] result, ref string sresult) //ref byte[] values
        {
            if (tcpSynCl == null)
                return false;
            else
            {
                byte[] values = null;
                values = WriteSyncData(CreateReadHeader(id, unit, startAddress, numInputs, fctReadCoil), id);
                if (values == null)
                {
                    Thread.Sleep(5);
                    values = WriteSyncData(CreateReadHeader(id, unit, startAddress, numInputs, fctReadCoil), id);
                    if (values == null)
                        return false;
                }
                sresult = Array2String(ref values); // System.Text.Encoding.Default.GetString(result);
                BitArray bitArray = new BitArray(values);
                result = new bool[bitArray.Count];
                bitArray.CopyTo(result, 0);
                return true;
            }
        }

        private string Array2String(ref byte[] result)
        {
            string s = "";
            int n = result.Length; // result.GetUpperBound(0); 
            for (int i = 0; i < n; i++)
                s = s + result[i].ToString() + ";";
            return s;
        }

        // ------------------------------------------------------------------------
        /// <summary>Read discrete inputs from slave asynchronous. The result is given in the response function.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="numInputs">Length of data.</param>
        public void ReadDiscreteInputs(ushort id, byte unit, ushort startAddress, ushort numInputs)
        {
            if (tcpAsyCl == null)
                return;
            else
            {
                if (bReconnectPossibility && !tcpAsyCl.Connected && iplocal != "" && portlocal != 0)
                    tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));

                WriteAsyncData(CreateReadHeader(id, unit, startAddress, numInputs, fctReadDiscreteInputs), id);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Read discrete inputs from slave synchronous.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="numInputs">Length of data.</param>
        /// <param name="values">Contains the result of function.</param>
        public void ReadDiscreteInputs(ushort id, byte unit, ushort startAddress, ushort numInputs, ref byte[] values)
        {
            if (tcpSynCl == null)
                return;
            else
                values = WriteSyncData(CreateReadHeader(id, unit, startAddress, numInputs, fctReadDiscreteInputs), id);
        }

        // ------------------------------------------------------------------------
        /// <summary>Read holding registers from slave asynchronous. The result is given in the response function.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="numInputs">Length of data.</param>
        public void ReadHoldingRegister(ushort id, byte unit, ushort startAddress, ushort numInputs)
        {
            if (tcpAsyCl == null)
                return;
            else
            {
                if (bReconnectPossibility && !tcpAsyCl.Connected && iplocal != "" && portlocal != 0)
                    tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));

                WriteAsyncData(CreateReadHeader(id, unit, startAddress, numInputs, fctReadHoldingRegister), id);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Read holding registers from slave synchronous.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="numInputs">Length of data.</param>
        /// <param name="values">Contains the result of function.</param>
        public bool ReadHoldingRegister(
            ushort id, byte unit, ushort startAddress, ushort numInputs, ref byte[] values)
        {
            if (tcpSynCl == null)
                return false;
            else
            {
                values = WriteSyncData(CreateReadHeader(id, unit, startAddress, numInputs, fctReadHoldingRegister), id);
                if (values == null)
                    return false;
                else
                    return true;
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Read input registers from slave asynchronous. The result is given in the response function.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="numInputs">Length of data.</param>
        public void ReadInputRegister(ushort id, byte unit, ushort startAddress, ushort numInputs)
        {
            if (tcpAsyCl == null)
                return;
            else
            {
                if (bReconnectPossibility && !tcpAsyCl.Connected && iplocal != "" && portlocal != 0)
                    tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));

                WriteAsyncData(CreateReadHeader(id, unit, startAddress, numInputs, fctReadInputRegister), id);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Read input registers from slave synchronous.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="numInputs">Length of data.</param>
        /// <param name="values">Contains the result of function.</param>
        public void ReadInputRegister(ushort id, byte unit, ushort startAddress, ushort numInputs, ref byte[] values)
        {
            if (tcpSynCl == null)
                return;
            else
                values = WriteSyncData(CreateReadHeader(id, unit, startAddress, numInputs, fctReadInputRegister), id);
        }

        // ------------------------------------------------------------------------
        /// <summary>Write single coil in slave asynchronous. The result is given in the response function.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="OnOff">Specifys if the coil should be switched on or off.</param>
        public void WriteSingleCoils(ushort id, byte unit, ushort startAddress, bool OnOff)
        {
            if (tcpAsyCl == null)
                return;
            else {
                if (bReconnectPossibility && !tcpAsyCl.Connected && iplocal != "" && portlocal != 0)
                {
                    tcpAsyCl.Disconnect(true);
                    tcpAsyCl.Dispose();
                    tcpAsyCl = new Socket(IPAddress.Parse(iplocal).AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));
                }

                byte[] data;
                data = CreateWriteHeader(id, unit, startAddress, 1, 1, fctWriteSingleCoil);
                if (OnOff == true) data[10] = 255;
                else data[10] = 0;
                WriteAsyncData(data, id);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Write single coil in slave synchronous.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="OnOff">Specifys if the coil should be switched on or off.</param>
        /// <param name="result">Contains the result of the synchronous write.</param>
        public bool WriteSingleCoils(ushort id, byte unit, ushort startAddress, bool OnOff, ref byte[] result)
        {
            if (tcpSynCl == null)
                return false;
            else
            {
                byte[] data;
                data = CreateWriteHeader(id, unit, startAddress, 1, 1, fctWriteSingleCoil);
                if (OnOff == true) data[10] = 255;
                else data[10] = 0;

                result = WriteSyncData(data, id);
                if (result == null)
                    return false;
                else
                    return true;
            }
        }

        public bool WriteSingleCoils(
            ushort id, byte unit, ushort startAddress, bool OnOff, ref byte[] result, ref string sresult)
        {
            if (tcpSynCl == null)
                return false;
            else
            {
                byte[] data;
                data = CreateWriteHeader(id, unit, startAddress, 1, 1, fctWriteSingleCoil);
                if (OnOff == true) data[10] = 255;
                else data[10] = 0;

                result = WriteSyncData(data, id);
                if (result == null)
                    return false;
                else
                {
                    sresult = Array2String(ref result);
                    return true;
                }
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Write multiple coils in slave asynchronous. The result is given in the response function.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="numBits">Specifys number of bits.</param>
        /// <param name="values">Contains the bit information in byte format.</param>
        public void WriteMultipleCoils(ushort id, byte unit, ushort startAddress, ushort numBits, byte[] values)
        {
            if (tcpAsyCl == null)
                return;
            else
            {
                if (bReconnectPossibility && !tcpAsyCl.Connected && iplocal != "" && portlocal != 0)
                    tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));

                byte numBytes = Convert.ToByte(values.Length);
                byte[] data;
                data = CreateWriteHeader(id, unit, startAddress, numBits, (byte)(numBytes + 2), fctWriteMultipleCoils);
                Array.Copy(values, 0, data, 13, numBytes);
                WriteAsyncData(data, id);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Write multiple coils in slave synchronous.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address from where the data read begins.</param>
        /// <param name="numBits">Specifys number of bits.</param>
        /// <param name="values">Contains the bit information in byte format.</param>
        /// <param name="result">Contains the result of the synchronous write.</param>
        public void WriteMultipleCoils(ushort id, byte unit, ushort startAddress, ushort numBits, byte[] values, ref byte[] result)
        {
            if (tcpSynCl == null)
                return;
            else
            {
                byte numBytes = Convert.ToByte(values.Length);
                byte[] data;
                data = CreateWriteHeader(id, unit, startAddress, numBits, (byte)(numBytes + 2), fctWriteMultipleCoils);
                Array.Copy(values, 0, data, 13, numBytes);
                result = WriteSyncData(data, id);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Write single register in slave asynchronous. The result is given in the response function.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address to where the data is written.</param>
        /// <param name="values">Contains the register information.</param>
        public void WriteSingleRegister(ushort id, byte unit, ushort startAddress, byte[] values)
        {
            if (tcpAsyCl == null)
                return;
            else
            {
                if (bReconnectPossibility && !tcpAsyCl.Connected && iplocal != "" && portlocal != 0)
                    tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));

                byte[] data;
                data = CreateWriteHeader(id, unit, startAddress, 1, 1, fctWriteSingleRegister);
                data[10] = values[0];
                data[11] = values[1];
                WriteAsyncData(data, id);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Write single register in slave synchronous.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address to where the data is written.</param>
        /// <param name="values">Contains the register information.</param>
        /// <param name="result">Contains the result of the synchronous write.</param>
        public bool WriteSingleRegister(ushort id, byte unit, ushort startAddress, byte[] values, ref byte[] result)
        {
            if (tcpSynCl == null)
                return false;
            else
            {
                byte[] data;
                data = CreateWriteHeader(id, unit, startAddress, 1, 1, fctWriteSingleRegister);
                data[10] = values[0];
                data[11] = values[1];

                result = WriteSyncData(data, id);
                if (result == null)
                    return false;
                else
                    return true;
            }
        }

        public bool WriteSingleRegister(
            ushort id, byte unit, ushort startAddress, byte[] values, ref byte[] result, ref string sresult)
        {
            if (tcpSynCl == null)
                return false;
            else
            {
                byte[] data;
                data = CreateWriteHeader(id, unit, startAddress, 1, 1, fctWriteSingleRegister);
                data[10] = values[0];
                data[11] = values[1];

                result = WriteSyncData(data, id);
                if (result == null)
                    return false;
                else
                {
                    sresult = Array2String(ref result);
                    return true;
                }
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Write multiple registers in slave asynchronous. The result is given in the response function.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address to where the data is written.</param>
        /// <param name="values">Contains the register information.</param>
        public void WriteMultipleRegister(ushort id, byte unit, ushort startAddress, byte[] values)
        {
            if (tcpAsyCl == null)
                return;
            else
            {
                if (bReconnectPossibility && !tcpAsyCl.Connected && iplocal != "" && portlocal != 0)
                    tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));

                ushort numBytes = Convert.ToUInt16(values.Length);
                if (numBytes % 2 > 0) numBytes++;
                byte[] data;

                data = CreateWriteHeader(id, unit, startAddress, Convert.ToUInt16(numBytes / 2), Convert.ToUInt16(numBytes + 2), fctWriteMultipleRegister);
                Array.Copy(values, 0, data, 13, values.Length);
                WriteAsyncData(data, id);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Write multiple registers in slave synchronous.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startAddress">Address to where the data is written.</param>
        /// <param name="values">Contains the register information.</param>
        /// <param name="result">Contains the result of the synchronous write.</param>
        public void WriteMultipleRegister(ushort id, byte unit, ushort startAddress, byte[] values, ref byte[] result)
        {
            if (tcpSynCl == null)
                return;
            else
            {
                ushort numBytes = Convert.ToUInt16(values.Length);
                if (numBytes % 2 > 0) numBytes++;
                byte[] data;

                data = CreateWriteHeader(id, unit, startAddress, Convert.ToUInt16(numBytes / 2), Convert.ToUInt16(numBytes + 2), fctWriteMultipleRegister);
                Array.Copy(values, 0, data, 13, values.Length);
                result = WriteSyncData(data, id);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Read/Write multiple registers in slave asynchronous. The result is given in the response function.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startReadAddress">Address from where the data read begins.</param>
        /// <param name="numInputs">Length of data.</param>
        /// <param name="startWriteAddress">Address to where the data is written.</param>
        /// <param name="values">Contains the register information.</param>
        public void ReadWriteMultipleRegister(ushort id, byte unit, ushort startReadAddress, ushort numInputs, ushort startWriteAddress, byte[] values)
        {
            if (tcpAsyCl == null)
                return;
            else
            {
                if (bReconnectPossibility && !tcpAsyCl.Connected && iplocal != "" && portlocal != 0)
                    tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));

                ushort numBytes = Convert.ToUInt16(values.Length);
                if (numBytes % 2 > 0) numBytes++;
                byte[] data;

                data = CreateReadWriteHeader(id, unit, startReadAddress, numInputs, startWriteAddress, Convert.ToUInt16(numBytes / 2));
                Array.Copy(values, 0, data, 17, values.Length);
                WriteAsyncData(data, id);
            }
        }

        // ------------------------------------------------------------------------
        /// <summary>Read/Write multiple registers in slave synchronous. The result is given in the response function.</summary>
        /// <param name="id">Unique id that marks the transaction. In asynchonous mode this id is given to the callback function.</param>
        /// <param name="unit">Unit identifier (previously slave address). In asynchonous mode this unit is given to the callback function.</param>
        /// <param name="startReadAddress">Address from where the data read begins.</param>
        /// <param name="numInputs">Length of data.</param>
        /// <param name="startWriteAddress">Address to where the data is written.</param>
        /// <param name="values">Contains the register information.</param>
        /// <param name="result">Contains the result of the synchronous command.</param>
        public void ReadWriteMultipleRegister(ushort id, byte unit, ushort startReadAddress, ushort numInputs, ushort startWriteAddress, byte[] values, ref byte[] result)
        {
            if (tcpSynCl == null)
                return;
            else
            {
                ushort numBytes = Convert.ToUInt16(values.Length);
                if (numBytes % 2 > 0) numBytes++;
                byte[] data;

                data = CreateReadWriteHeader(id, unit, startReadAddress, numInputs, startWriteAddress, Convert.ToUInt16(numBytes / 2));
                Array.Copy(values, 0, data, 17, values.Length);
                result = WriteSyncData(data, id);
            }
        }

        // ------------------------------------------------------------------------
        // Create modbus header for read action
        private byte[] CreateReadHeader(ushort id, byte unit, ushort startAddress, ushort length, byte function)
        {
            byte[] data = new byte[12];

            byte[] _id = BitConverter.GetBytes((short)id);
            data[0] = _id[1];			    // Slave id high byte
            data[1] = _id[0];				// Slave id low byte
            data[5] = 6;					// Message size
            data[6] = unit;					// Slave address
            data[7] = function;				// Function code
            byte[] _adr = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder((short)startAddress));
            data[8] = _adr[0];				// Start address
            data[9] = _adr[1];				// Start address
            byte[] _length = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder((short)length));
            data[10] = _length[0];			// Number of data to read
            data[11] = _length[1];			// Number of data to read
            return data;
        }

        // ------------------------------------------------------------------------
        // Create modbus header for write action
        private byte[] CreateWriteHeader(ushort id, byte unit, ushort startAddress, ushort numData, ushort numBytes, byte function)
        {
            byte[] data = new byte[numBytes + 11];

            byte[] _id = BitConverter.GetBytes((short)id);
            data[0] = _id[1];				// Slave id high byte
            data[1] = _id[0];				// Slave id low byte
            byte[] _size = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder((short)(5 + numBytes)));
            data[4] = _size[0];				// Complete message size in bytes
            data[5] = _size[1];				// Complete message size in bytes
            data[6] = unit;					// Slave address
            data[7] = function;				// Function code
            byte[] _adr = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder((short)startAddress));
            data[8] = _adr[0];				// Start address
            data[9] = _adr[1];				// Start address
            if (function >= fctWriteMultipleCoils)
            {
                byte[] _cnt = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder((short)numData));
                data[10] = _cnt[0];			// Number of bytes
                data[11] = _cnt[1];			// Number of bytes
                data[12] = (byte)(numBytes - 2);
            }
            return data;
        }

        // ------------------------------------------------------------------------
        // Create modbus header for read/write action
        private byte[] CreateReadWriteHeader(ushort id, byte unit, ushort startReadAddress, ushort numRead, ushort startWriteAddress, ushort numWrite)
        {
            byte[] data = new byte[numWrite * 2 + 17];

            byte[] _id = BitConverter.GetBytes((short)id);
            data[0] = _id[1];						// Slave id high byte
            data[1] = _id[0];						// Slave id low byte
            byte[] _size = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder((short)(11 + numWrite * 2)));
            data[4] = _size[0];						// Complete message size in bytes
            data[5] = _size[1];						// Complete message size in bytes
            data[6] = unit;							// Slave address
            data[7] = fctReadWriteMultipleRegister;	// Function code
            byte[] _adr_read = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder((short)startReadAddress));
            data[8] = _adr_read[0];					// Start read address
            data[9] = _adr_read[1];					// Start read address
            byte[] _cnt_read = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder((short)numRead));
            data[10] = _cnt_read[0];				// Number of bytes to read
            data[11] = _cnt_read[1];				// Number of bytes to read
            byte[] _adr_write = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder((short)startWriteAddress));
            data[12] = _adr_write[0];				// Start write address
            data[13] = _adr_write[1];				// Start write address
            byte[] _cnt_write = BitConverter.GetBytes((short)IPAddress.HostToNetworkOrder((short)numWrite));
            data[14] = _cnt_write[0];				// Number of bytes to write
            data[15] = _cnt_write[1];				// Number of bytes to write
            data[16] = (byte)(numWrite * 2);

            return data;
        }

        // ------------------------------------------------------------------------
        // Write asynchronous data
        private void WriteAsyncData(byte[] write_data, ushort id)
        {
            if ((tcpAsyCl != null) && (tcpAsyCl.Connected))
            {
                try
                {
                    bReady = false; 
                    tcpAsyCl.BeginSend(write_data, 0, write_data.Length, SocketFlags.None, new AsyncCallback(OnSend), tcpAsyCl); //null
                    while (!bReady) Thread.Sleep(1);
                    bReady = false; 
                    tcpAsyCl.BeginReceive(tcpAsyClBuffer, 0, tcpAsyClBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceive), tcpAsyCl);
                    while (!bReady) Thread.Sleep(1);
                    bReady = false;
                }
                catch (Exception ex) //(SystemException)
                {
                    if (bReconnectPossibility)
                    {
                        tcpAsyCl.Disconnect(true);
                        tcpAsyCl.Dispose();
                        tcpAsyCl = new Socket(IPAddress.Parse(iplocal).AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        tcpAsyCl.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));

                        if (!tcpAsyCl.Connected)
                        {
                            CallException(id, write_data[6], write_data[7], excExceptionConnectionLost);
                        }
                        else
                        {
                            bReady = false;
                            tcpAsyCl.BeginSend(write_data, 0, write_data.Length, SocketFlags.None, new AsyncCallback(OnSend), tcpAsyCl); //null
                            while (!bReady) Thread.Sleep(1);
                            bReady = false;
                            tcpAsyCl.BeginReceive(tcpAsyClBuffer, 0, tcpAsyClBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceive), tcpAsyCl);
                            while (!bReady) Thread.Sleep(1);
                            bReady = false;
                        }
                    } else
                    {
                        CallException(id, write_data[6], write_data[7], excExceptionConnectionLost);
                    }
                }
            }
            else
                CallException(id, write_data[6], write_data[7], excExceptionConnectionLost);
        }

        // ------------------------------------------------------------------------
        // Write asynchronous data acknowledge
        private void OnSend(System.IAsyncResult result)
        {
            //bReady = true;
            if (result.IsCompleted == false) CallException(0xFFFF, 0xFF, 0xFF, excSendFailt);

            Socket s = (Socket)result.AsyncState;
            if (s != null) {
                try {
                    int bytes = s.EndSend(result);
                }
                catch (Exception ex)
                {
                    bReady = true;
                    CallException(0xFFFF, 0xFF, 0xFF, excSendFailt);
                }
            }
            bReady = true;
        }

        // ------------------------------------------------------------------------
        // Write asynchronous data response
        private void OnReceive(System.IAsyncResult result)
        {
            if (result.IsCompleted == false) {
                bReady = true;
                CallException(0xFF, 0xFF, 0xFF, excExceptionConnectionLost);
            }

            //commented 2018.10.18
            //Socket s = (Socket)result.AsyncState;
            //if (s != null) {
            //    try {
            //        int bytes = s.EndReceive(result);
            //        if (bytes > 0) {
            //            //s.BeginReceive(tcpAsyClBuffer, 0, tcpAsyClBuffer.Length, SocketFlags.Partial, this.OnReceive, null);
            //        } else {
            //            s.Close();
            //            ushort id3 = 3;
            //            s.Connect(new IPEndPoint(IPAddress.Parse(iplocal), portlocal));
            //            CallException(id3, 0, 0, excExceptionConnectionLost);
            //        }
            //    }
            //    catch (Exception ex) { }
            //}

            ushort id = SwapUInt16(BitConverter.ToUInt16(tcpAsyClBuffer, 0));
            byte unit = tcpAsyClBuffer[6];
            byte function = tcpAsyClBuffer[7];
            byte[] data;

            // Write response data ------------------------------------------------------------
            if ((function >= fctWriteSingleCoil) && (function != fctReadWriteMultipleRegister)) {
                data = new byte[2];
                Array.Copy(tcpAsyClBuffer, 10, data, 0, 2);
            }
            // Read response data ------------------------------------------------------------
            else {
                data = new byte[tcpAsyClBuffer[8]];
                Array.Copy(tcpAsyClBuffer, 9, data, 0, tcpAsyClBuffer[8]);
            }

            // Response data is slave exception ------------------------------------------------------------
            if (function > excExceptionOffset) {
                function -= excExceptionOffset;
                bReady = true;
                CallException(id, unit, function, tcpAsyClBuffer[8]);
            }
            // Response data is regular data ------------------------------------------------------------
            else {
                if (OnResponseData != null) OnResponseData(id, unit, function, data);
                bReady = true;
            }
        }


        private void OnReceiveLifeGuard(System.IAsyncResult result)
        {
            if (result.IsCompleted == false)
            {
                bReady = true;
                CallException(0xFF, 0xFF, 0xFF, excExceptionConnectionLost);
            }

            ushort id = SwapUInt16(BitConverter.ToUInt16(tcpAsyClBuffer, 0));
            byte unit = tcpAsyClBuffer[6];
            byte function = tcpAsyClBuffer[7];
            byte[] data;

            // Write response data ------------------------------------------------------------
            if ((function >= fctWriteSingleCoil) && (function != fctReadWriteMultipleRegister))
            {
                data = new byte[2];
                Array.Copy(tcpAsyClBuffer, 10, data, 0, 2);
            }
            // Read response data ------------------------------------------------------------
            else
            {
                data = new byte[tcpAsyClBuffer[8]];
                Array.Copy(tcpAsyClBuffer, 9, data, 0, tcpAsyClBuffer[8]);
            }

            // Response data is slave exception ------------------------------------------------------------
            if (function > excExceptionOffset)
            {
                function -= excExceptionOffset;
                bReady = true;
                CallException(id, unit, function, tcpAsyClBuffer[8]);
            }
            // Response data is regular data ------------------------------------------------------------
            else
            {
                //if (OnResponseData != null) OnResponseData(id, unit, function, data);
                bReady = true;
            }
        }


        // ------------------------------------------------------------------------
        // Write data and and wait for response
        private byte[] WriteSyncData(byte[] write_data, ushort id)
        {
            if (!tcpSynCl.Connected && iplocal != "") { Dispose(); connect(iplocal, portlocal); }

            if (tcpSynCl.Connected)
            {
                try
                {
                    tcpSynCl.Send(write_data, 0, write_data.Length, SocketFlags.None);
                    Thread.Sleep(5);
                    int result = tcpSynCl.Receive(tcpSynClBuffer, 0, tcpSynClBuffer.Length, SocketFlags.None);
                    Thread.Sleep(5);
                    byte unit = tcpSynClBuffer[6];
                    byte function = tcpSynClBuffer[7];
                    byte[] data;

                    if (result == 0)
                    {
                        CallException(id, unit, write_data[7], excExceptionConnectionLost);
                        return null;
                    }

                    // Response data is slave exception ------------------------------------------------------------
                    if (function > excExceptionOffset)
                    {
                        function -= excExceptionOffset;
                        CallException(id, unit, function, tcpSynClBuffer[8]);
                        return null;
                    }
                    else
                    {
                        // Write response data ------------------------------------------------------------
                        if ((function >= fctWriteSingleCoil) && (function != fctReadWriteMultipleRegister))
                        {
                            data = new byte[2];
                            Array.Copy(tcpSynClBuffer, 10, data, 0, 2);
                        }
                        // Read response data ------------------------------------------------------------
                        else
                        {
                            data = new byte[tcpSynClBuffer[8]];
                            Array.Copy(tcpSynClBuffer, 9, data, 0, tcpSynClBuffer[8]);
                        }
                        return data;
                    }
                }
                catch (SystemException)
                {
                    CallException(id, write_data[6], write_data[7], excExceptionConnectionLost);
                    return null;
                }
            }
            else
            {
                CallException(id, write_data[6], write_data[7], excExceptionConnectionLost);
                return null;
            }
        }
    }
}
