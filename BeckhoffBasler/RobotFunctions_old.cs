using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace SumoNewMachine
{
    public class RobotFunctions
    {
        public BackgroundWorker bwUserPort = new BackgroundWorker();//wait cmd ready
        public BackgroundWorker bwHostPort = new BackgroundWorker();
        public string bwName = "";
        //public int FunctionCode;
        public string status = "";
        public ListBox lstSend;

        public Boolean Fini;
        public Boolean Start;
        public Boolean Busy;
        public Boolean Stop;
        public Boolean ReadyToStart;
        public Boolean PickFini;
        public Boolean PlaceFini;
        public TextBox text11, text12, text13, text14, text15, text16;

        public Form frm;
        public PictureBox picBox;
        public Boolean bExitcycle = false;
        public string RobotCmd = "";
        public string RobotReport = "";
        public Single[] ParmGet = new Single[30];
        Stopwatch stopwatch = new Stopwatch();
        //ROBOT1--Pick 10,Place 20,Maintanence-30,Inputs-31,Outputs-32,Griper Type-33,
        //ROBOT1--Pick 110,Place 120,Maintanence-120,Inputs-131,Outputs-132,Griper Type-133,
        public int[] CmdRobot2 = { 110, 111, 112, 113, 114, 115, 116 };
        public int[] CmdRobot1 = { 10, 11, 12, 13, 14, 15, 16,17, 35, 45 ,55,65 };
        public string RobotName = "";
        public string RobotProgram = "";
        public struct position
        {
            public Double x;
            public Double y;
            public Double z;
            public Double r;
            public int Rotate;

        }
        public struct SendRobotParms //====2013
        {
            public string comment;
            public Single[] SendParm;
            public bool NotSendMess;
            //public int FunctionCode;
        }
        public struct SendHostParms //====2013
        {
            public string comment;
            public string[] cmd;
            public float timeout;

        }
        public struct CommReply
        {
            public bool result;
            public float[] data;
            public string status;
            public string comment;//====2013
            public int FunctionCode;
        }
        public struct HostReply
        {
            public bool result;
            public string reply;
            public string cmd;
            public string[] data;
            public string status;
            public string comment;//====2013

        }
        public Socket m_socClient; //= new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
        public string ClientStatus;
        public byte[] mb_message = new byte[8];
        public byte[] mb_response = new byte[8];
        public string sRobotSnd = "";
        public string sRobotGt = "";
        public delegate void lst();
        public Boolean bReadPort = false;
        //public string sTempPort;
        public Boolean WaitResult = false;
        public Stopwatch StopWatch1 = new Stopwatch();
        public Stopwatch StopWatch2 = new Stopwatch();

        #region ---------event to Form--------------
        public event EventHandler<MyEventArgs> GetFiniDone;//= delegate { };
        public delegate void EventHandler(object sender, MyEventArgs e);
        public string Robot_buffer = "";
        public class MyEventArgs : System.EventArgs
        {
            private string _string;
            public string StringToSend
            {
                set { _string = value; }
                get { return _string; }

            }
            private Single[] _result;
            public Single[] DataGet
            {
                set { _result = value; }
                get { return _result; }
            }
        }

        public MyEventArgs MyE = new MyEventArgs();

        public void GetFini(Single[] result)
        {   //Do stuff          // Raise Event, which triggers all method subscribed to it!     
            //StopWatch2.Restart();
            long t = MyStatic.Stsw.ElapsedMilliseconds;
            if (t - MyStatic.StswTime < MyStatic.StswDelay)//$
            {
                //SetTextLst("########### delay CAM  ######" +
                //           " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                Thread.Sleep(MyStatic.StswDelay);// (MyStatic.StswDelay);
            }
            MyStatic.StswTime = MyStatic.Stsw.ElapsedMilliseconds;
            MyE.DataGet = result;
            this.GetFiniDone(this, MyE);
        }
        #endregion

        public void SetControls(ListBox LstSend, Form Frm)
        {
            lstSend = LstSend;
            //txtstate = Txtstate;
            frm = Frm;
        }

        public void SetText(int id, TextBox text)
        {
            switch (id)
            {
                case (1): text11 = text; break;
                case (2): text12 = text; break;
                case (3): text13 = text; break;
                case (4): text14 = text; break;
                case (5): text15 = text; break;
                case (6): text16 = text; break;
                default: break;
            }
        }

        public RobotFunctions()
        {
            bwUserPort.WorkerSupportsCancellation = true;
            bwUserPort.WorkerReportsProgress = true;
            bwUserPort.DoWork += new DoWorkEventHandler(bwUserPort_DoWork);
            bwUserPort.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwUserPort_RunWorkerCompleted);
            bwUserPort.ProgressChanged += new ProgressChangedEventHandler(bwUserPort_ProgressChanged);

            bwHostPort.WorkerSupportsCancellation = true;
            bwHostPort.WorkerReportsProgress = true;
            bwHostPort.DoWork += new DoWorkEventHandler(bwHostPort_DoWork);
            bwHostPort.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwHostPort_RunWorkerCompleted);
            bwHostPort.ProgressChanged += new ProgressChangedEventHandler(bwHostPort_ProgressChanged);
        }
        
        #region ------------HOST PORT Procedures------------------
        public Boolean SendToHostPort(SendHostParms SendParams, ref string Error)//====2013
        {
            DateTime NowTime = DateTime.Now;
            DateTime startTime = DateTime.Now;

            MyStatic.RobotErrorMess = false;

            String[] arg = SendParams.cmd;
            //create parameters
            string sparms = arg[0];
            for (int i = 1; i < arg.Length - 1; i++)
            {
                sparms = sparms + "," + arg[i];
            }
            string stx = Encoding.ASCII.GetString(new byte[] { 0x02 });
            string etx = Encoding.ASCII.GetString(new byte[] { 0x03 });
            string cr = Encoding.ASCII.GetString(new byte[] { 13 });

            sparms = stx + sparms + cr + etx;

            //send to robot

            if (arg[0] == "SF" || arg[0] == "SU") sparms = sparms + stx + "OK" + cr + etx;
            HostReply RobotHostReply = new HostReply();
            
            if (!SendData(sparms))//send cmd
            {
                RobotHostReply.result = false;
                RobotHostReply.comment = SendParams.comment;//====2013
                                                            //    SetTextLst("ERROR SEND DATA " + RobotHostReply.comment + "  (" + DateTime.Now.ToString() + ")", lstSend, frm);
                MainHMI.NewMainHMI.ListAdd("ERROR SEND DATA " + RobotHostReply.comment + "  (" + DateTime.Now.ToString() + ")");

                SetTxtText("Error in reading data", text11, frm);
                Error = "Error send data";
                return false;
            }

            if (!StartWaitRobotHost(SendParams, ref Error))
            {
                return false;
            }
            return true;
        }

        public Boolean GetHostCmd(ref string getstring, ref byte[] getbyte, float tmout)
        {
            try
            {
                StopReceive = true;
                //int i = 0;

                Thread.Sleep(50);
                Stopwatch stopper = new Stopwatch();
                stopper.Start();

                while (StopReceive)
                {
                    Thread.Sleep(1);
                    if ((tmout > 0) & (tmout * 1000 < stopper.ElapsedMilliseconds))
                    { return false; }
                    //m_socClient.ReceiveTimeout = 10;
                    byte[] buffer = new byte[1024];
                    int tout = (int)tmout * 1000000;
                    if (tout == 0) tout = 10000;
                    if (m_socClient.Poll(tout, SelectMode.SelectRead))
                    //if (m_socClient.Available > 0)//check  data in port
                    {
                        int iRx = 0;
                        try
                        {
                            iRx = m_socClient.Receive(buffer);
                        }
                        catch (SocketException E) {}
                        catch (Exception E) { }
                        if (iRx > 0)
                        {
                            char[] chars = new char[iRx];
                            Thread.Sleep(1);
                            System.Text.Decoder d = System.Text.Encoding.ASCII.GetDecoder();
                            int charLen = d.GetChars(buffer, 0, iRx, chars, 0);
                            getbyte = buffer;
                            System.String szData = new System.String(chars);
                            string s = "";
                            for (int ii = 0; ii < chars.Length; ii++)
                            {
                                int u = (int)chars[ii];
                                s = s + " " + u.ToString();
                            }
                            //   SetTextLst("--->   " + szData +
                            //    " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);

                            MainHMI.NewMainHMI.ListAdd("--->   " + szData + " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");

                            if ((szData != null) && (szData != ""))
                            {
                                getstring = szData;
                                return true;

                            }//host
                        }
                    }
                    //return false;
                }
                return false;
            }
            catch (SocketException se) { return false; }
        }

        public Boolean StartWaitRobotHost(SendHostParms SendParams, ref string Error)//====2013
        {
            //start backgroundworker bwUserPort
            //frmMain.newMdiChiledRobot.ControlsEnable(false);
            DateTime NowTime = DateTime.Now;
            DateTime startTime = DateTime.Now;
            MyStatic.RobotErrorMess = false;

            while (bwHostPort.IsBusy) //wait end busy for 2 sec
            {
                NowTime = DateTime.Now;
                if (0.3 < (((int)NowTime.Subtract(startTime).TotalMilliseconds)) / 1000) break;
                Thread.Sleep(2);
            }
            // while (bwUserPort.IsBusy);
            if (!bwHostPort.IsBusy)
            {
                bwHostPort.RunWorkerAsync(SendParams);//====2013
            }
            else
            {
                bwHostPort.CancelAsync();
                MyStatic.bReset = true;
                Thread.Sleep(1000);
                MyStatic.bReset = false;
                Error = "ERROR: Connection Busy" + "Can't run the worker twice! Action:" + SendParams.comment;
                //AddToList(Error);
                //  SetTextLst("!!!   " + Error +
                // " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                MainHMI.NewMainHMI.ListAdd("!!!   " + Error + " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                return false;
            }
            return true;
        }

        public void bwHostPort_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            try
            {
                //SetProgress(e.ProgressPercentage, progressbar2, frm);
                // SetTxtText(e.ProgressPercentage.ToString(), text12, frm);
            }
            catch { }
        }

        public void bwHostPort_DoWork(object sender, DoWorkEventArgs e)
        {
            DateTime NowTime = DateTime.Now;
            DateTime startTime = DateTime.Now;
            HostReply RobotHostReply = new HostReply();

            //int ii = 0;
            if (Thread.CurrentThread.Name == null) { Thread.CurrentThread.Name = bwName; }
            //task for running in background
            if (e.Argument.GetType().IsValueType)
            {
                e.Cancel = false;
                SendHostParms RobotParms = (SendHostParms)e.Argument;
                string[] arg = RobotParms.cmd;

                Single tmout = RobotParms.timeout;
                Boolean timeout_flag = false;
                //bPortReading = true;

                do
                {
                    // if (MyStatic.bReset) return;
                    Thread.Sleep(1);
                    bwHostPort.ReportProgress((int)NowTime.Subtract(startTime).TotalMilliseconds);
                    NowTime = DateTime.Now;
                    if ((tmout > 0) & (tmout < (((int)NowTime.Subtract(startTime).TotalMilliseconds)) / 1000))
                    { timeout_flag = true; break; }
                    string getstring = "";
                    byte[] getbyte = new byte[255];
                    if (!GetHostCmd(ref getstring, ref getbyte, tmout))
                    {
                        //bPortReading = false;
                        RobotHostReply.reply = "";
                        RobotHostReply.result = false;
                        RobotHostReply.status = ClientStatus + " " + arg[0];
                        //RobotHostReply.FunctionCode = RobotParms.FunctionCode;//====2013
                        RobotHostReply.comment = RobotParms.comment;//====2013
                        e.Result = RobotHostReply;
                        return;
                    }
                    if ((getstring != null) & (getstring != ""))
                    {
                        string sTempPort1 = getstring;
                        //string[] arrparm = Regex.Split(sTempPort1, @"\w+"); //Split(',');//fined variable name
                        //string[] arrparm = sTempPort1.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        string[] arrparm = Regex.Split(sTempPort1, @"\W+");
                        if (arg[0].Substring(0, 2) == "IW") arg[0] = arg[0].Substring(0, 2);
                        switch (arg[0])
                        {
                            case ("IW"):
                                RobotHostReply.cmd = arg[0];
                                if ((arrparm[1] == "OK") || (arrparm[1] == "NG")) { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("SP"):
                                RobotHostReply.cmd = arg[0];
                                if ((arrparm[1] == "OK") || (arrparm[1] == "NG")) { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("FD"):
                                RobotHostReply.cmd = arg[0];
                                if ((arrparm[1] == "OK") || (arrparm[1] == "NG")) { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("RS,SEL"):
                                RobotHostReply.cmd = arg[0];
                                if ((arrparm[1] == "OK") || (arrparm[1] == "NG")) { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("SO"):
                                RobotHostReply.cmd = arg[0];
                                if ((arrparm[1] == "OK") || (arrparm[1] == "NG")) { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("BR"):
                                RobotHostReply.cmd = arg[0];
                                if ((arrparm[1] == "OK") || (arrparm[1] == "NG")) { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("RS,PRG"):
                                RobotHostReply.cmd = arg[0];
                                if ((arrparm[1] == "OK") || (arrparm[1] == "NG")) { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("RS,ERR"):
                                RobotHostReply.cmd = arg[0];
                                if ((arrparm[1] == "OK") || (arrparm[1] == "NG")) { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("SL,SINTER"):
                                RobotHostReply.cmd = arg[0];
                                if (arrparm[1] == "OK" || (arrparm[1] == "NG")) { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("RN"):
                                RobotHostReply.cmd = arg[0];
                                if (arrparm[1] == "OK") { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("EC,MODE CYCLE"):
                                RobotHostReply.cmd = arg[0];
                                //if ((arrparm[1] == "OK") || (arrparm[1] == "NG")) { RobotHostReply.result = true; }
                                if (arrparm[1] == "OK") { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("DO,PRINT IP1, HERE.X,HERE.Y,HERE.Z,HERE.C,CR"): //print currpos
                                RobotHostReply.cmd = arg[0];
                                if (arrparm[1] == "OK") { RobotHostReply.result = true; }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("SF"):
                                RobotHostReply.cmd = arg[0];//print currpos
                                if (arrparm[1] == "FL")
                                //if ((arrparm[1] == "FL")|| (arrparm[2] == "FL"))
                                {
                                    string st = "";
                                    string s = "";
                                    string[] arrparm1 = new string[arrparm.Length - 1];
                                    char[] myChar;
                                    Array.Resize<string>(ref arrparm, 16);
                                    // if (arrparm[1] == "FL")
                                    myChar = sTempPort1.ToCharArray();
                                    //else
                                    //{
                                    //    char[] myChar1 = new char[sTempPort1.Length - 1];
                                    //    //Array.Resize<char>(ref myChar, sTempPort1.Length-1);
                                    //    myChar = sTempPort1.ToCharArray();
                                    //    for (int i = 0; i < arrparm.Length; i++)
                                    //        myChar1[i] = myChar[i +5];
                                    //    myChar = myChar1;
                                    //}   
                                    //int i;
                                    for (int i = 0; i < myChar.Length; i++)
                                    {
                                        st = st + getbyte[i] + "\r";
                                        //                 For i = 1 To Len(data_from_robot)
                                        if ((i >= 42 * 4 + 1) & (i <= 46 * 4))// Then '169-184
                                        {
                                            s = s + myChar[i];
                                            Thread.Sleep(1);
                                            // if(MainHMI.NewMainHMI.chkDebugPrint.Checked) Debug.Print(i.ToString() + " " +
                                            //    "Char=" + getbyte[i].ToString() + " " +
                                            //    "Asc=" + myChar[i] +
                                            //    "HEX=");
                                        }
                                    }
                                    for (int j = 0; j < 4; j++)
                                    {
                                        byte[] bt = new byte[4];
                                        for (int i = 0; i < 4; i++) bt[3 - i] = getbyte[144 + i + j * 4];
                                        float myFloat = System.BitConverter.ToSingle(bt, 0);
                                        arrparm[2 + j] = myFloat.ToString();
                                        // if(MainHMI.NewMainHMI.chkDebugPrint.Checked) Debug.Print("myfloat:" + myFloat.ToString());
                                    }
                                    if (arrparm.Length < 16 || myChar.Length < 16)
                                    {
                                        MessageBox.Show("ERROR GET STATUS DATA FROM ROBOT");
                                        break;
                                    }
                                    for (int j = 0; j < 4; j++)
                                    {
                                        arrparm[10 + j] = ((int)myChar[4 + j]).ToString();
                                        // arrparm[10] PWR
                                        // arrparm[11] Estop
                                        // arrparm[12] Motion
                                        // arrparm[13] System
                                        // arrparm[14] Error
                                    }
                                    arrparm[14] = "0";//no robot current errors
                                    for (int j = 8; j < 27; j++)
                                    {
                                        if (myChar[j] != '?')
                                        {
                                            arrparm[14] = "1";//robot current error
                                            break;
                                        }
                                    }
                                    arrparm[15] = ((int)myChar[45]).ToString();
                                    RobotHostReply.result = true; RobotHostReply.data = arrparm;
                                }
                                else { RobotHostReply.result = false; }
                                break;
                            case ("SU"):
                                RobotHostReply.cmd = arg[0];//print currpos
                                if (arrparm[1] == "FL") { RobotHostReply.result = true; RobotHostReply.data = arrparm; }
                                else { RobotHostReply.result = false; }
                                break;
                            default:
                                RobotHostReply.result = false;
                                break;
                        }
                        RobotHostReply.status = ClientStatus + " HOST" + RobotHostReply.cmd;
                        RobotHostReply.comment = RobotParms.comment;//====2013
                        e.Result = RobotHostReply;
                        //GC.Collect();//$$$
                        return;
                    }
                    else
                    {
                        RobotHostReply.result = false;
                        RobotHostReply.status = ClientStatus + " HOST" + RobotHostReply.cmd;
                        //RobotUserReply.FunctionCode = RobotParms.FunctionCode;//====2013
                        RobotHostReply.comment = RobotParms.comment;//====2013
                        e.Result = RobotHostReply;
                        //GC.Collect();//$$$
                        return;
                    }
                }
                while (true);

                if (timeout_flag)
                {
                    RobotHostReply.result = false;
                    RobotHostReply.status = ClientStatus + " cmd" + RobotHostReply.cmd;
                    RobotHostReply.comment = RobotParms.comment;//====2013
                    e.Result = RobotHostReply;
                    //GC.Collect();//$$$
                    return;
                }
            }
        }

        public void bwHostPort_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)//robot user port
                                                                                               //autocycle for robot
        {
            // if (MyStatic.bReset) return;
            DateTime NowTime = DateTime.Now;
            DateTime startTime = DateTime.Now;
            //GC.Collect();//$$$
            while (bwHostPort.IsBusy) //wait end busy for 2 sec
            {
                NowTime = DateTime.Now;
                if (0.3 < (((int)NowTime.Subtract(startTime).TotalMilliseconds)) / 1000) break;
                Thread.Sleep(2);
            }
            //bPortReading = false;
            if (e.Result == null) { MessageBox.Show("TIMEOUT READ Host " + RobotName); return; }
            HostReply RobotReply = new HostReply();

            RobotReply = (HostReply)e.Result;
            string Error = "";
            //int Function = RobotReply.FunctionCode;

            if ((RobotReply.result == false) & (RobotReply.cmd != "SF") & (RobotReply.cmd != "SU") & (RobotReply.cmd != "RN"))
            {
                try
                {
                    Error = RobotReply.status;
                    //SetTxtText("Error cmd", text1, frm);
                    MessageBox.Show("RESULT HOST ERROR " + RobotName);
                    return;
                }
                catch { }
                return;
            }

            SendHostParms ParmsHost = new SendHostParms();
            string ErrMessage = "";
            string cmd = "";
            // if (MyStatic.bReset) return;
            switch (RobotReply.cmd)
            {
                case "SP": //stop
                    cmd = "RS,PRG";
                    ParmsHost.comment = RobotReply.comment;
                    break;
                case "RS,PRG": //stop
                    cmd = "RS,ERR";
                    ParmsHost.comment = RobotReply.comment;
                    break;
                case "RS,ERR"://reset prg
                    cmd = "RS,SEL";
                    if (RobotReply.comment == "TP") { ParmsHost.comment = RobotReply.comment; cmd = "BR"; }
                    else if (RobotReply.comment == "AUTO") { ParmsHost.comment = RobotReply.comment; cmd = "SO"; }
                    else if (RobotReply.comment == "RunRobotReset") { ParmsHost.comment = RobotReply.comment; cmd = "SL," + RobotProgram; }
                    break;
                case "BR":
                    return;
                case "RS,SEL":
                    cmd = "EC,MODE CYCLE";
                    break;
                case "EC,MODE CYCLE":
                    cmd = "SO";
                    // return;
                    break;
                    //return;//end reset robot
                case "FD":
                    return;
                    //start robot
                case "SO": //servo on  
                    //cmd = "SL,SINTROB1.TXT";
                    cmd = "SL," + RobotProgram;
                    if (RobotReply.comment == "AUTO") { ParmsHost.comment = RobotReply.comment; }

                    MainHMI.NewMainHMI.SetPanelsEnable(3);

                    break;
                case "SL,SINTER": //select program
                    cmd = "RN";//run program
                    ParmsHost.comment = RobotReply.comment;
                    break;
                case "SL,MM2": //select program
                    cmd = "RN";//run program
                    break;
                case "RN"://run prg
                    if (RobotReply.comment == "AUTO" || RobotReply.comment == "RunRobotReset")
                    {
                        if (RobotReply.result == true)
                        {
                            float[] p = { 0 };
                            if (RobotReply.comment == "AUTO")
                                p[0] = 1002;//sf 
                            else
                                p[0] = 1012;
                            GetFini(p);//send event 
                            return;
                        }
                        else
                        {
                            if (RobotReply.comment == "RunRobotReset")
                            {
                                float[] p13 = { 0 };
                                p13[0] = 1013;
                                GetFini(p13);//send event 
                                return;
                            }

                            if (MyStatic.sReplay_count > 3)
                            {
                                MessageBox.Show("RESULT HOST ERROR 12 " + RobotName);
                                return;
                            }
                            ClearPort(0.5f);
                            Thread.Sleep(2000);
                            MyStatic.sReplay_count++;
                            Array.Resize<String>(ref ParmsHost.cmd, 1);

                            ParmsHost.cmd[0] = "RS,PRG";
                            ParmsHost.comment = RobotReply.comment; // "AUTO";
                            ParmsHost.timeout = 0.5f;//timeout

                            if (!SendToHostPort(ParmsHost, ref ErrMessage)) MessageBox.Show("RESULT HOST ERROR " + RobotName);
                            return;
                        }
                    }
                    float[] p1 = { 0 };
                    p1[0] = 1003;//sf 
                    GetFini(p1);//send event
                    return;
                case "DO,PRINT IP1, HERE.X,HERE.Y,HERE.Z,HERE.C,CR, HERE.X,CR":
                    return;
                case "SF":
                    string st = "";
                    if (RobotReply.data == null)
                    {
                        MyStatic.sReplay_count++;
                        Thread.Sleep(100);
                        StopWatch1.Restart();
                        //string getstring = "";
                        //SendHostParms ParmsHost = new RobotFunctions.SendHostParms();
                        if (MyStatic.sReplay_count > 3)
                        {
                            MessageBox.Show("RESULT HOST ERROR 1 " + RobotName);
                            return;
                        }
                        ClearPort(0.5f);
                        //if (m_socClient.Available>0) GetUserCmd(ref  getstring, 0.5f);//clear buffer
                        Array.Resize<String>(ref ParmsHost.cmd, 1);
                        ParmsHost.cmd[0] = "SF";
                        ParmsHost.timeout = 5;//  0.5f;//timeout
                        string ErrMessage1 = "";
                        if (!SendToHostPort(ParmsHost, ref ErrMessage1))
                            MessageBox.Show("RESULT HOST ERROR " + RobotName); return;
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        st = st + RobotReply.data[i] + "\r";
                        //                 For i = 1 To Len(data_from_robot)
                    }
                    float[] pos = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                    pos[0] = 1000;//sf 
                    pos[1] = Single.Parse(RobotReply.data[2]);
                    pos[2] = Single.Parse(RobotReply.data[3]);
                    pos[3] = Single.Parse(RobotReply.data[4]);
                    pos[4] = Single.Parse(RobotReply.data[5]);

                    pos[10] = Single.Parse(RobotReply.data[10]);
                    pos[11] = Single.Parse(RobotReply.data[11]);
                    pos[12] = Single.Parse(RobotReply.data[12]);
                    pos[13] = Single.Parse(RobotReply.data[13]);
                    pos[14] = Single.Parse(RobotReply.data[14]);
                    pos[15] = Single.Parse(RobotReply.data[15]);
                    GetFini(pos);//send event
                                 //MessageBox.Show(st);
                    cmd = "OK";
                    return;
                    //break;
                case "SU":
                    st = "";
                    if (RobotReply.data == null)
                    {
                        MyStatic.sReplay_count++;
                        Thread.Sleep(100);
                        StopWatch1.Restart();

                        if (MyStatic.sReplay_count > 3)
                        {
                            MessageBox.Show("RESULT HOST ERROR 11 " + RobotName);
                            return;
                        }
                        ClearPort(0.5f);
                        //if (m_socClient.Available>0) GetUserCmd(ref  getstring, 0.5f);//clear buffer
                        Array.Resize<String>(ref ParmsHost.cmd, 1);
                        ParmsHost.cmd[0] = "SU";
                        ParmsHost.timeout = 0.5f;//timeout
                        string ErrMessage1 = "";
                        if (!SendToHostPort(ParmsHost, ref ErrMessage1))
                            MessageBox.Show("RESULT HOST ERROR " + RobotName); return;
                    }

                    st = st + "1. " + RobotReply.data[2] + ": " + RobotReply.data[3] + " (" + RobotReply.data[4] + ") " + RobotReply.data[5] + "\r";
                    //st = st + RobotReply.data[4] + "\r";
                    //st = st + RobotReply.data[5] + "\r";
                    st = st + "2. " + RobotReply.data[6] + ": " + RobotReply.data[7] + "\r";
                    st = st + "3. " + RobotReply.data[9] + ": " + RobotReply.data[10] + "\r";
                    //st = st + "4." + RobotReply.data[11] + ": " + RobotReply.data[12] + "\r";
                    st = st + "4. " + RobotReply.data[13] + ": " + RobotReply.data[14] + "\r";
                    st = st + "5. " + RobotReply.data[15] + ": " + RobotReply.data[16] + "\r";
                    float[] st1 = { 0, 0, 0, 0 };
                    if (RobotReply.data[3] == "external")
                        st1[2] = (float)MyStatic.auto;
                    else if (RobotReply.data[3] == "teaching")
                        st1[2] = (float)MyStatic.teaching;
                    else st1[2] = 0f;
                    if (RobotReply.data[15] == "running") //[16]
                        st1[3] = (float)MyStatic.run;
                    else if (RobotReply.data[15] == "stop") //[16]
                        st1[3] = (float)MyStatic.stop;
                    else st1[3] = 0f;
                    if (MyStatic.Robot1mode.mess || MyStatic.Robot2mode.mess) MessageBox.Show(st);

                    st1[0] = 1001;//su 
                    st1[1] = 0;
                    //GC.Collect();//$$$
                    GetFini(st1);//send event
                    cmd = "OK";

                    MainHMI.NewMainHMI.SetPanelsEnable(2);

                    return;
                //break;
                default: return;
            }
            //SendHostParms ParmsHost = new SendHostParms();
            Array.Resize<String>(ref ParmsHost.cmd, 1);
            ParmsHost.cmd[0] = cmd;
            ParmsHost.timeout = 5;//  0.5f;//timeout

            if (SendToHostPort(ParmsHost, ref ErrMessage)) { return; };
            if (ErrMessage != "")
            {
                MessageBox.Show(ErrMessage);
                return;
            }
        }
        #endregion
        #region -----------------USER PORT Procedures----------------
        public Boolean SendToUserPort(SendRobotParms SendParams, ref string Error)//====2013
        {
            StopWatch1.Restart();
            MyStatic.RobotErrorMess = false;
            //GC.Collect();//$$$
            Single[] arg = SendParams.SendParm;
            //create parameters
            string sparms = ((int)arg[0]).ToString();
            for (int i = 1; i < arg.Length - 1; i++)
            {
                sparms = sparms + "," + String.Format("{0:0.00}", arg[i]);
            }
            sparms = sparms + "\r";
            //send robot last parameter e.Argument.length-1 is timeout
            //send to robot
            if (SendParams.NotSendMess == null || !SendParams.NotSendMess)
            {
                if ((arg[0] != 1) || (arg[0] != 101))//cmd start from 10 ,1 onle for read port
                {
                    CommReply RobotUserReply = new CommReply();
                    if (!MyStatic.chkDebug)//flow chart
                    {
                        if (!SendData(sparms))//send cmd
                        {
                            RobotUserReply.result = false;
                            //RobotUserReply.FunctionCode = SendParams.FunctionCode;//====2013
                            RobotUserReply.comment = SendParams.comment;//====2013
                                                                        // SetTextLst("ERROR SEND DATA " + RobotUserReply.comment + "  (" + DateTime.Now.ToString() + ")", lstSend, frm);
                            MainHMI.NewMainHMI.ListAdd("ERROR SEND DATA " + RobotUserReply.comment + "  (" + DateTime.Now.ToString() + ")");

                            SetTxtText("Error in reading data", text11, frm);
                            //picBox.BackColor = Color.Red;
                            Error = "ERROR SEND DATA ";
                            return false;
                        }
                    }
                    else
                    {
                        string s = "";
                        for (int ii = 0; ii < arg.Length; ii++)
                        {
                            int u = (int)arg[ii];
                            s = s + " " + u.ToString();
                        }
                        //    SetTextLst("<---   " + s +
                        // " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                        MainHMI.NewMainHMI.ListAdd("<---   " + s + " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                    }
                }
            }

            int cmd = (int)arg[0];
            Single tmout = arg[arg.Length - 1];
            //wait robot

            if (!StartWaitRobotUser(SendParams, ref Error))
            {
                return false;
            }
            //}
            return true;
        }

        public Boolean StartWaitRobotUser(SendRobotParms SendParams, ref string Error)//====2013
        {
            DateTime NowTime = DateTime.Now;
            DateTime startTime = DateTime.Now;
            MyStatic.RobotErrorMess = false;
            Stopwatch stopw = new Stopwatch();
            stopw.Start();
            while (bwUserPort.IsBusy) //wait end busy for 2 sec
            {
                if (300 < stopw.ElapsedMilliseconds) break;
                Thread.Sleep(2);
            }
            if (!bwUserPort.IsBusy)
            {
                bwUserPort.RunWorkerAsync(SendParams);//====2013
            }
            else
            {
                bwUserPort.CancelAsync();
                MyStatic.bReset = true;
                Thread.Sleep(1000);
                MyStatic.bReset = false;
                Error = "ERROR: Connection Busy" + "Can't run the worker twice! Action:" + SendParams.comment;
                // SetTextLst("!!!   " + Error +
                //     " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                MainHMI.NewMainHMI.ListAdd("!!!   " + Error +
                       " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                //AddToList(Error);
                //picBox.BackColor = Color.Red;
                return false;
            }
            //GC.Collect();//$$$
            return true;
        }

        public void bwUserPort_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            try
            {
                // SetProgress(e.ProgressPercentage, progressbar2, frm);
            }
            catch { }
        }

        public Boolean InArray(int[] arr, int cmd)
        {
            int first = Array.IndexOf<int>(arr, cmd);
            if (first >= 0) return true; else return false;
        }

        public void bwUserPort_DoWork(object sender, DoWorkEventArgs e)
        {
            Busy = true;
            //GC.Collect();//$$$
            CommReply RobotUserReply = new CommReply();

            float[] myparm = { 0 };

            if (Thread.CurrentThread.Name == null) { Thread.CurrentThread.Name = bwName; }
            //task for running in background

            if (e.Argument.GetType().IsValueType)
            {
                e.Cancel = false;
                SendRobotParms RobotParms = (SendRobotParms)e.Argument;
                Single[] arg = RobotParms.SendParm;
                int cmdwait = (int)arg[0];
                Single tmout = arg[arg.Length - 1];
                // if (tmout == 0) MessageBox.Show("ERROR TIMEOUT");
                Boolean timeout_flag = false;
                bPortReading = true;

                if (InArray(CmdRobot1, cmdwait) || InArray(CmdRobot2, cmdwait))//wait reply
                {
                    do
                    {
                        if (MyStatic.bReset) return;
                        Thread.Sleep(1);
                        string getstring = "";
                        if (MyStatic.Speed <= 0) MyStatic.Speed = 5;
                        short timeK = (short)(100 / MyStatic.Speed);

                        if (MyStatic.chkDebug)//flow chart
                        {
                            //flow chart
                        }
                        else
                        {
                            if ((tmout > 0) & (tmout * 1000 < StopWatch1.ElapsedMilliseconds))
                            {
                                timeout_flag = true;
                                bPortReading = false;
                                break;
                            }

                            if (Robot_buffer == "")
                            {
                                GetUserCmd(ref getstring, ref timeout_flag, tmout);
                                if ((getstring == "") && (cmdwait != 1) && (RobotCmd != ""))
                                {
                                    getstring = RobotCmd;
                                    // SetTextLst("---> *** timeout " + getstring + " ****" +
                                    //    " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                                    MainHMI.NewMainHMI.ListAdd("---> *** timeout " + getstring + " ****" +
                                        " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                                }
                                else if ((getstring == "") && (cmdwait != 1) && (RobotCmd == ""))
                                {
                                    if (timeout_flag)  break;

                                    bPortReading = false;
                                    RobotUserReply.result = false;
                                    RobotUserReply.status = ClientStatus + " cmd" + cmdwait.ToString();
                                    Array.Resize<Single>(ref RobotUserReply.data, 2);
                                    RobotUserReply.data[0] = 0;
                                    RobotUserReply.data[1] = cmdwait;
                                    //RobotUserReply.FunctionCode = RobotParms.FunctionCode;//====2013
                                    RobotUserReply.comment = RobotParms.comment;//====2013
                                    e.Result = RobotUserReply;
                                    return;
                                }
                            }
                            else
                            {
                                getstring = Robot_buffer;
                                Robot_buffer = "";
                                // SetTextLst("--->>> ***** " + getstring + " *****" +
                                //" // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                                MainHMI.NewMainHMI.ListAdd("--->>> ***** " + getstring + " *****" +  " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                            }
                        }
                        if ((getstring != null) & (getstring != "") & (cmdwait == 1))
                        {
                            getstring = "CMD01 1 " + getstring;
                        }
                        if ((getstring != null) & (getstring != ""))
                        {
                            string sTempPort1 = getstring;
                            Robot_buffer = "";
                            string[] arrparm1 = sTempPort1.Split(new char[] { '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            if (arrparm1.Length >= 2)
                            {
                                for (int i = 1; i < arrparm1.Length; i++)
                                    Robot_buffer = Robot_buffer + arrparm1[i];
                            }
                            if (Robot_buffer != "")
                            {
                                string[] arrparm2 = Robot_buffer.Split(new char[] { '\r' }, StringSplitOptions.RemoveEmptyEntries);
                                //if ((arrparm2[0] != "CMD") & (arrparm2[0] != "REQ")) Robot_buffer = "";
                            }
                            //else Robot_buffer = ""; 
                            if (arrparm1[0] != "")
                            {
                                string[] arrparm = arrparm1[0].Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                                int cmd = 0;
                                if ((arrparm[0] != "CMD") & (arrparm[0] != "REQ"))
                                {
                                    SetTxtText("Error in reading data", text11, frm);
                                    return;
                                }
                                if (arrparm.Length > 1)
                                {
                                    Array.Resize<float>(ref myparm, arrparm.Length);
                                    for (int i = 1; i < arrparm.Length; i++)
                                    {
                                        if (!Single.TryParse(arrparm[i], out myparm[i]))
                                        {
                                            SetTxtText("Error in reading data", text11, frm);
                                        }
                                    }
                                    cmd = (int)myparm[1];
                                }
                                if ((cmdwait == cmd) & (arrparm[0] == "CMD") & (myparm[2] <= 1)) { break; };
                                if ((cmdwait == cmd) & (arrparm[0] == "CMD") & (myparm[2] == 100)) { break; };
                                if ((cmdwait == cmd) & (arrparm[0] == "REQ") & (myparm[2] > 1))
                                {
                                    RobotCmd = "CMD " + cmdwait + " 1" + '\r';
                                    break;
                                    // GetFini(myparm);  
                                }
                                else
                                {
                                    RobotCmd = "";
                                }
                            }
                            //else if (arrparm1.Length == 2)
                            //{
                            //}
                            //else if (arrparm1.Length == 3)
                            //{
                            //}
                            //else
                            //{
                            //}
                            //falgs for vision
                        }
                    }
                    while (true);
                    //GC.Collect();//$$$
                    if (timeout_flag)
                    {
                        bPortReading = false;
                        RobotUserReply.result = false;
                        RobotUserReply.status = ClientStatus + " cmd" + cmdwait.ToString();
                        //RobotUserReply.FunctionCode = RobotParms.FunctionCode;//====2013
                        RobotUserReply.comment = RobotParms.comment;//====2013
                        e.Result = RobotUserReply;
                        return;
                    }
                  
                    SetTxtText(StopWatch1.ElapsedMilliseconds.ToString(), text12, frm);///////////////////////////////////////
                    //SetTxtText(StopWatch1.ElapsedMilliseconds.ToString(), text12, frm)
                    bPortReading = false;

                    RobotUserReply.result = true;
                    RobotUserReply.status = ClientStatus;
                    RobotUserReply.data = myparm;
                    RobotUserReply.data[0] = 0;// cmdwait;
                                               //RobotUserReply.FunctionCode = RobotParms.FunctionCode;//====2013
                    RobotUserReply.comment = RobotParms.comment;//====2013
                    if (RobotUserReply.data[2] == 1)
                    {
                        //picBox.BackColor = Color.Yellow;
                    }
                    else
                    {
                      //  picBox.BackColor = Color.Red;
                    }
                    e.Result = RobotUserReply;
                }
            }
        }

        public Boolean GetUserCmd(ref string getstring, ref bool timeout_flag, float tmout)
        {
            try
            {
                //DateTime NowTime = DateTime.Now;
                //DateTime startTime = DateTime.Now;
                while (!MyStatic.bReset)
                {
                    Thread.Sleep(2);
                    //NowTime = DateTime.Now;
                    if ((tmout > 0) & (tmout * 1000 < StopWatch1.ElapsedMilliseconds))
                    {
                        timeout_flag = true;
                        bPortReading = false;
                        return false;
                    }
                    //SetTxtText(StopWatch1.ElapsedMilliseconds.ToString(), text12, frm);//////////////////////////////////
                    //m_socClient.ReceiveTimeout = 1;
                    byte[] buffer = new byte[1024];
                    //if (m_socClient.Available > 0)//check  data in port
                    int tout = (int)tmout * 1000;
                    if (tout == 0) tout = 1000;
                    try
                    {
                        if (m_socClient.Poll(tout, SelectMode.SelectRead))//check  data in port
                        {
                            int iRx = 0;
                            try
                            {
                                iRx = m_socClient.Receive(buffer);
                            }
                            catch (SocketException E)
                            {
                                return false;
                            }
                            catch (Exception Ex)
                            {
                                return false;
                            }
                            if (iRx > 0)
                            {
                                char[] chars = new char[iRx];
                                Thread.Sleep(1);
                                System.Text.Decoder d = System.Text.Encoding.UTF8.GetDecoder();
                                int charLen = d.GetChars(buffer, 0, iRx, chars, 0);
                                System.String szData = new System.String(chars);
                                string s = "";
                                for (int ii = 0; ii < chars.Length; ii++)
                                {
                                    int u = (int)chars[ii];
                                    s = s + " " + u.ToString();
                                }
                                //     SetTextLst("--->   " + szData +
                                //   " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                                MainHMI.NewMainHMI.ListAdd("--->   " + szData + " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                                //int cmd=0;
                                getstring = szData;
                                szData = "";//enother worker;
                                            //GC.Collect();//$$$
                                return true;
                            }
                        }
                    }
                    catch (Exception Ex)
                    {
                        return false;
                    }
                    //return false;
                }
                return false;
            }
            catch (SocketException se) { ClientStatus = se.ToString(); return false; }
        }

        public void ClearPort(float tmout)
        {
            try
            {
                Stopwatch sw = new Stopwatch();
                while (!MyStatic.bReset)
                {
                    Thread.Sleep(2);
                    if ((tmout > 0) & (tmout * 1000 < sw.ElapsedMilliseconds))
                    { return; }
                    byte[] buffer = new byte[2048];
                    if (m_socClient.Available > 0)//check  data in port
                    {
                        int iRx = 0;
                        try
                        {
                            iRx = m_socClient.Receive(buffer);
                        }
                        catch (SocketException E) {}
                    }
                    else return;
                    //return false;
                }
                return;
            }
            catch (SocketException se) { ClientStatus = se.ToString(); return; }
        }

        //RobotFunctions.position outpos = new RobotFunctions.position();
        //SendRobotParms Parms = new SendRobotParms();
        public void bwUserPort_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)//robot user port
                                                                                               //autocycle for robot
        {
            Busy = false;
            if (MyStatic.bReset) return;
            if (e.Result == null) return;

            // while (bwUserPort.IsBusy) { Thread.Sleep(5); };//wait fini bwUserPort
            DateTime NowTime = DateTime.Now;
            Stopwatch stopper = new Stopwatch();
            stopper.Restart();
            while (bwUserPort.IsBusy) //wait end busy for 2 sec
            {
                NowTime = DateTime.Now;
                if (300 < stopper.ElapsedMilliseconds) break;
                Thread.Sleep(2);
            }
            bPortReading = false;
            CommReply RobotReply = new CommReply();
            RobotReply = (CommReply)e.Result;
            // if(MainHMI.NewMainHMI.chkDebugPrint.Checked) Debug.Print("RobotBworkComlite " + RobotReply.data[0].ToString() + " " + RobotReply.data[1].ToString() + " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
            string Error = "";
            int Function = RobotReply.FunctionCode;
            Single[] result = RobotReply.data;

            if (RobotReply.result == false)
            {
                try
                {
                    //picBox.BackColor = Color.Red;
                    Error = RobotReply.status;
                    SetTxtText("Error cmd", text11, frm);
                    //MessageBox.Show("TIMEOUT READ ROBOT "+RobotName);
                    Error = "";
                    Array.Resize<Single>(ref result, 3);
                    if (result[0] != null && result[1] != null) ;
                    result[0] = 0;
                    result[2] = result[1];
                    result[1] = 0;
                    GetFini(result);
                    //GC.Collect();//$$$
                    return;
                }
                catch { }
                return;
            }

            //outpos.x = 0; outpos.y = 0; outpos.z = 0; outpos.r = 0; outpos.Rotate = 0;
            if (RobotReply.data == null || (RobotReply.data.Length < 2))
            {
                picBox.BackColor = Color.Red;
                MessageBox.Show("Robot Reply DATA ERROR " + RobotName);
                return;
            }

            if (InArray(CmdRobot1, (int)result[1])) // || InArray(CmdRobot2, (int)result[0]))
            {
                if ((result[2] == 1) || (result[2] == 2) || (result[2] == 3) || (result[2] == 100) || (result.Length > 3 && result[3] == 1)) { }
                else
                {
                    //MessageBox.Show("cmd" + result[0].ToString() + " ERROR " + result[2].ToString()+" " + RobotName);
                    //     SetTextLst("----- cmd" + result[0].ToString() + " ERROR " + result[2].ToString() + " " + RobotName +
                    //    " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                    MainHMI.NewMainHMI.ListAdd("----- cmd" + result[0].ToString() + " ERROR " + result[2].ToString() + " " + RobotName +
                                   " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                    return;
                }
            }
            //string ErrMessage = "";
            //MainHMI.NewMainHMI.txtCycle.Text = StopWatch.ElapsedMilliseconds.ToString();
            //  text13.Text = StopWatch1.ElapsedMilliseconds.ToString();

            SetTxtText(StopWatch1.ElapsedMilliseconds.ToString(), text13, frm);
            //if (MyStatic.bLast) return ;
            //create event to form
            // if(MainHMI.NewMainHMI.chkDebugPrint.Checked) Debug.Print(bwName + "[1]" + result[1].ToString() + "[2]" + result[2].ToString() + " // " + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n");   
            GetFini(result);
            //GC.Collect();//$$$
            return;
        }
        #endregion

        public Boolean bPortReading = false;
        private String response = String.Empty;
        
        #region ------------Socket INIT Procedures-------------------------
        public Boolean ConnectClient(string port, string address, ref string error)
        {
            try
            {
                string err = "";
                m_socClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPGlobalProperties ipg = IPGlobalProperties.GetIPGlobalProperties();
                IPEndPoint[] endpoint = ipg.GetActiveTcpListeners();
                int myPort = System.Convert.ToInt16(port, 10);
                System.Net.IPAddress remoteIPAddress = System.Net.IPAddress.Parse(address);

                Ping p1 = new Ping();
                PingReply PR = p1.Send(address);
                if (!PR.Status.ToString().Equals("Success"))
                {
                    error = "ERROR CONNECTION " + address + "\r";
                    //MessageBox.Show("ERROR CONNECTION " + address);
                    return false;
                }

                if (!Connected)
                {
                    if (!ConnectPort(port, address, ref err))
                    {
                        //MessageBox.Show("ERROR CONNECTION " + err);
                        error = error + "ERROR CONNECTION " + err + "/r";
                        return false;
                    }
                    bPortReading = true;
                    return true;
                    //if (!bwTCP.IsBusy) { bwTCP.RunWorkerAsync(); }
                }
                return true; ;
            }
            catch
            {
                return false;
            }
        }

        public Boolean ConnectPort(string Port, string address, ref string Error)
        {
            //this procedure is connecting to tcp/ip port
            //
            try
            {
                //create a new client socket ...
                // m_socClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                String szIPSelected = address;
                String szPort = Port;
                int alPort = System.Convert.ToInt16(szPort, 10);

                System.Net.IPAddress remoteIPAddress = System.Net.IPAddress.Parse(szIPSelected);
                //fined port
                //m_socClient.Bind(new System.Net.IPEndPoint(remoteIPAddress, 0));
                //alPort=((System.Net.IPEndPoint)m_socClient.LocalEndPoint).Port;
                System.Net.IPEndPoint remoteEndPoint = new System.Net.IPEndPoint(remoteIPAddress, alPort);
                //m_socClient.Connect(remoteEndPoint);
                var asynResalt = m_socClient.BeginConnect(remoteEndPoint, null, null);
                int timeout = 1000;
                bool success = asynResalt.AsyncWaitHandle.WaitOne(timeout, true);
                if (success)
                {
                    m_socClient.EndConnect(asynResalt);
                    Error = "";
                    return true;
                }
                else
                {
                    m_socClient.Close();
                    throw new SocketException(10060);//timeout
                }
            }
            catch (SocketException se)
            {
                Error = se.Message;
                return false;
            }
        }

        public Boolean Connected = false;

        public void ClientConnected()
        {
            if (m_socClient.Connected)
                Connected = true;
            else
                Connected = false;
        }

        public void ClosePort()
        {
            StopReceive = false;
            try
            { m_socClient.Close(); }
            catch { };
        }

        public void CloseSocket()
        {
            if (m_socClient != null)
            {
                try
                {
                    //m_socClient.Shutdown(SocketShutdown.Both);
                    MyStatic.bReset = true;
                    StopReceive = false;
                    // m_socClient.Disconnect(true);
                    m_socClient.Close();
                    m_socClient.Dispose();// = null;
                }
                catch (SocketException e)
                { MessageBox.Show(e.ToString()); }
            }
        }

        public Boolean SendData(string data)
        {
            //try
            //{
                Object objData = data;
                byte[] byData = System.Text.Encoding.ASCII.GetBytes(objData.ToString());
                if (m_socClient == null)
                {
                    MessageBox.Show("NO CONNECTION WITH ROBOTS!");
                    return false;
                }
            try
            {
                m_socClient.Send(byData);

                string s = "";
                for (int ii = 0; ii < byData.Length; ii++)
                {
                    int u = (int)byData[ii];
                    s = s + " " + u.ToString();
                }
                // if(MainHMI.NewMainHMI.chkDebugPrint.Checked) Debug.Print("<-"+RobotName+" "+data);
                //  SetTextLst("<---   " + data +
                // " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                MainHMI.NewMainHMI.ListAdd("<---   " + data + " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                //GC.Collect();//$$$
                return true;
            }
            catch (SocketException se)
            {
                //MessageBox.Show(se.Message);
                ClientStatus = se.ToString();
                return false;
            }
            catch (Exception ex)
            {
                //MessageBox.Show(se.Message);
                ClientStatus = ex.ToString();
                return false;
            }
        }

        public bool StopReceive;
        //public string GetString;

        #endregion

        #region ---------Delegate Procedures--------------

        delegate void SetTextBox(string text, TextBox txt, Form frm);
        public void SetTxtText(string text, TextBox txt, Form frm)
        {
            if ((txt == null) || (frm == null)) { return; }
            try
            {
                if (txt.InvokeRequired)
                {
                    SetTextBox d = new SetTextBox(SetTxtText);
                    frm.Invoke(d, new object[] { text, txt, frm });
                }
                else
                {
                    txt.Visible = false;
                    txt.Text = text;
                    txt.Visible = true;
                }
            }
            catch { }

        }

        delegate void SetListText(string text, ListBox lst, Form frm, string sender = "");
        public void SetTextLst(string text, ListBox lst, Form frm, string sender = "")
        {
            if ((lst == null) || (frm == null)) { return; }
            try
            {
                if (lst.InvokeRequired)
                {
                    SetListText d = new SetListText(SetTextLst);
                    frm.Invoke(d, new object[] { text, lst, frm, sender });
                    //lst.Visible = true;
                }
                else
                {
                    lst.Visible = false;
                    if (sender == "")
                        lst.Items.Add(RobotName + ":" + text);
                    else
                        lst.Items.Add(sender + ":" + text);
                    if (lst.Items.Count > 400) lst.Items.Clear();
                    lst.Visible = true;

                    if (lst.Items.Count > 350)
                    {
                        for (int i = 0; i < 20; i++) lst.Items.RemoveAt(0);
                    }

                    lst.SetSelected(lst.Items.Count - 1, true);
                }
                //GC.Collect();//$$$
            }
            catch { }
        }

        delegate void SetProgressBar(int val, ProgressBar progressbar, Form frm);
        private void SetProgress(int val, ProgressBar progressbar, Form frm)
        {
            if ((progressbar == null) || (frm == null)) { return; }
            try
            {
                if (progressbar.InvokeRequired)
                {
                    SetProgressBar d = new SetProgressBar(SetProgress);
                    frm.Invoke(d, new object[] { val, progressbar, frm });
                }
                else
                {
                    progressbar.Value = val;
                }
                //GC.Collect();//$$$
            }
            catch { }
        }
        #endregion

        public Boolean RunCmd(SendRobotParms Parms, ref string ErrorMessage)
        //send cmd to PC robot is master
        {
            if (Parms.SendParm.Length != 11)
            {
                //MessageBox.Show("ERROR RUN PARAMETERS ");
                //  SetTextLst("----- ERROR RUN PARAMETERS--------" + RobotName +
                //    " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                MainHMI.NewMainHMI.ListAdd("----- ERROR RUN PARAMETERS--------" + RobotName + " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                return false;
            }
            Parms.SendParm[9] = 0;
            for (int i = 0; i < 9; i++) Parms.SendParm[9] = Parms.SendParm[9] + Parms.SendParm[i];

            if (!MyStatic.bStartCycle)
                Parms.SendParm[10] = 60;

            DateTime NowTime = DateTime.Now;
            //DateTime startTime = DateTime.Now;
            Stopwatch stopper = new Stopwatch();
            stopper.Start();
            while (bwUserPort.IsBusy) //wait end busy for 2 sec
            {
                NowTime = DateTime.Now;
                if (1000 < stopper.ElapsedMilliseconds)
                {
                    //MessageBox.Show("ERROR RUN PROGRAM RunCmd" + Parms.SendParm[0].ToString()+" robot:"+ RobotName);
                    //     SetTextLst("----- ERROR RUN PROGRAM RunCmd" + Parms.SendParm[0].ToString() + " robot:" + RobotName +
                    //   " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);

                    MainHMI.NewMainHMI.ListAdd("----- ERROR RUN PROGRAM RunCmd" + Parms.SendParm[0].ToString() + " robot:" + RobotName +
                                  " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                    return false;
                }
                Thread.Sleep(2);
            }

            //send message
            //picBox.BackColor = Color.Lime;
            if (!SendToUserPort(Parms, ref ErrorMessage)) { return false; };
            //GC.Collect();//$$$
            return true;
        }
    }
}
