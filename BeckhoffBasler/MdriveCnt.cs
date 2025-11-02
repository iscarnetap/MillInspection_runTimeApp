using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Diagnostics;


namespace SumoNewMachine
{
    public struct CommReply
    {
        public bool result;
        public string reply;
        public string cmd;
        public float[] data;
        public string status;
        public string comment;

    }
    public struct SendParms //====2013
    {
        public string comment;
        public string cmd;
        public Single[] SendParm;
    }



   public class MdriveCnt
    {

        public string[] MdrivePosition = new string[5];
        public bool AllDevices = false;
        public string Command = "";

        public SerialPort sp = new SerialPort();
        public string rs232status;
        public byte[] mb_message = new byte[8];
        public byte[] mb_response = new byte[8];
        //public ListBox lstRobotSnd = new ListBox();
        //public ListBox lstRobotGt = new ListBox();
        public string sMdriveSnd = "";
        public string sMdrivetGt = "";
        public delegate void lst();
        //public int CmdGet = 0;
        public Boolean bReadPort = false;
        //private Single[] Parm=new Single[30];
        static string sTempPort;
        public Boolean WaitResult = false;
        public string DeviceName = "";
        public string CmdName = "";
        //FanucFunctions RF = new FanucFunctions();
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        public BackgroundWorker bwMDrive = new BackgroundWorker();

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
            private string _string1;
            public string StringGet
            {
                set { _string1 = value; }
                get { return _string1; }

            }
            private string _string2;
            public string CmdGet
            {
                set { _string2 = value; }
                get { return _string2; }

            }
            private Single[] _result;
            public Single[] DataGet
            {
                set { _result = value; }
                get { return _result; }

            }
            private Boolean _ok;
            public Boolean OkGet
            {
                set { _ok = value; }
                get { return _ok; }

            }


            private string _name;
            public string DeviceName
            {
                set { _name = value; }
                get { return _name; }
            }

            private string _cmd;
            public string Cmd
            {
                set { _cmd = value; }
                get { return _cmd; }
            }
        }


        public MyEventArgs MyE = new MyEventArgs();
        public void GetFini(CommReply result)
        {         //Do stuff          // Raise Event, which triggers all method subscribed to it!     
            //StopWatch2.Restart();
            long t = MyStatic.Stsw.ElapsedMilliseconds;
            if (t - MyStatic.StswTime < MyStatic.StswDelay)//$
            {
                //SetTextLst("########### delay CAM  ######" +
                //           " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                Thread.Sleep(MyStatic.StswDelay);// (MyStatic.StswDelay);
            }
            MyStatic.StswTime = MyStatic.Stsw.ElapsedMilliseconds;

            MyE.DataGet = result.data;
            MyE.StringGet = result.reply;
            MyE.OkGet = result.result;
            MyE.CmdGet = result.cmd;
            MyE.DeviceName = DeviceName;

            this.GetFiniDone(this, MyE);
        }
        #endregion

        public MdriveCnt()
        {
            bwMDrive.WorkerSupportsCancellation = true;
            bwMDrive.WorkerReportsProgress = true;
            bwMDrive.DoWork += new DoWorkEventHandler(bwMDrive_DoWork);
            bwMDrive.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwMDrive_RunWorkerCompleted);
            //bwMDrive.ProgressChanged += new ProgressChangedEventHandler(bwMDrive_ProgressChanged);
        }//constructor
        #region delegate
        delegate void SetListText(string text, ListBox lst, Form frm);
        private void SetTextLst(string text, ListBox lst, Form frm)
        {
            if ((lst == null) || (frm == null)) { return; }
            try
            {
                if ((lst == null) || (frm == null)) { return; }
                if (lst.InvokeRequired)
                {
                    SetListText d = new SetListText(SetTextLst);
                    frm.Invoke(d, new object[] { text, lst, frm });
                }
                else
                {
                    lst.Items.Add(text);
                }
            }
            catch { }

        }
        delegate void SetTextBox(string text, TextBox txt, Form frm);
        private void SetTxtText(string text, TextBox txt, Form frm)
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
                    txt.Text = text;

                }
            }
            catch { }

        }
        public ListBox lstSend;
        public TextBox txtstate;
        public Form frm;
        public void SetControls(ListBox LstSend, TextBox Txtstate, Form Frm)
        {
            lstSend = LstSend;
            txtstate = Txtstate;
            frm = Frm;
        }
        #endregion
        #region Port INIT Procedures
        public bool Open(string portName, int baudRate, int databits, string parity, string stopBits)
        {
            //Ensure port isn't already opened:
            if (!sp.IsOpen)
            {
                //Assign desired settings to the serial port:
                Parity p = new Parity();
                p = SetPortParity(Parity.None, parity);
                StopBits b = new StopBits();
                b = SetPortStopbits(StopBits.One, stopBits);

                sp.PortName = portName;
                sp.BaudRate = baudRate;
                sp.DataBits = databits;
                sp.Parity = Parity.None;
                sp.StopBits = StopBits.One;
                //These timeouts are default and cannot be editted through the class at this point:
                sp.ReadTimeout = 1000;
                sp.WriteTimeout = 1000;



                try
                {
                    sp.Open();
                }
                catch (Exception err)
                {
                    rs232status = "Error opening " + portName + ": " + err.Message;
                    SetTxtText(rs232status, txtstate, frm);
                    return false;
                }
                rs232status = portName + " opened successfully";
                SetTxtText(rs232status, txtstate, frm);
                return true;
            }
            else
            {
                rs232status = portName + " already opened";
                SetTxtText(rs232status, txtstate, frm);
                return false;
            }
        }


        public bool Close()
        {
            //Ensure port is opened before attempting to close:
            rs232status = "";
            SetTxtText(rs232status, txtstate, frm);
            if (sp.IsOpen)
            {
                try
                {
                    sp.Close();
                }
                catch (Exception err)
                {
                    rs232status = "Error closing " + sp.PortName + ": " + err.Message;
                    SetTxtText(rs232status, txtstate, frm);
                    return false;
                }
                rs232status = sp.PortName + " closed successfully";
                SetTxtText(rs232status, txtstate, frm);
                return true;
            }
            else
            {
                rs232status = sp.PortName + " is not open";
                SetTxtText(rs232status, txtstate, frm);
                return false;
            }
        }

        public Parity SetPortParity(Parity defaultPortParity, string sParity)
        {
            string parity;
            parity = sParity;

            if (parity == "") { parity = defaultPortParity.ToString(); }
            return (Parity)Enum.Parse(typeof(Parity), parity);
        }

        public StopBits SetPortStopbits(StopBits defaultPortStopbits, string sStopbits)
        {
            string stopbits;
            stopbits = sStopbits;

            if (stopbits == "") { stopbits = defaultPortStopbits.ToString(); }
            return (StopBits)Enum.Parse(typeof(StopBits), stopbits);
        }
        public void FlashPort()
        {
            if (sp.IsOpen)
            {
                sp.DiscardInBuffer();
                sp.DiscardOutBuffer();
                sp.ReadExisting();
                //for (int i = 0; i < MyStatic.Parm.Length; i++) MyStatic.Parm[i] = 0;
                sTempPort = "";
            }

        }
        #endregion
        #region Port Write/Read Procedures
        public Boolean SendMdrive(string mess)
        {
            //CmdGet = 0;
            rs232status = "";
            SetTxtText(rs232status, txtstate, frm);
            //if (sp.IsOpen & MyStatic.bPortReading)
            if (sp.IsOpen)
            {
                //Clear in/out buffers:
                sp.DiscardOutBuffer();
                sp.DiscardInBuffer();
                try
                {

                    sp.Write(mess);//cr Environment.NewLine);
                    if (mess !="PR MV" + "\r")
                    {
                      

                        MainHMI.NewMainHMI.ListAdd("<---   " + mess.Trim() +
                   " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                    }

                    bReadPort = true;
                    sMdriveSnd = mess.Trim();
                    return true;

                }
                catch (Exception err)
                {
                    rs232status = "Error in write event: " + err.Message;
                    SetTxtText(rs232status, txtstate, frm);
                    return false;
                }

            }
            return false;
        }
        #endregion
        #region Wait procedures


        public void Delay(int ms)
        {
            DateTime startTime = DateTime.Now;
            DateTime NowTime;

            do
            {
                Application.DoEvents();
                NowTime = DateTime.Now;
            }
            while (ms > (int)NowTime.Subtract(startTime).TotalMilliseconds);
        }
        #endregion
        //private void tmrReadPort_Tick(object sender, EventArgs e)
        public Boolean ReadPort(CancellationToken ct, ref string CmdGet)
        {


            if (ct.IsCancellationRequested)
            {
                MyStatic.bPortReading = false; return false;
            }
            if (MyStatic.bReset)
            {
                MyStatic.bPortReading = false; return false;
            }

            if (sp.IsOpen)
            {
                MyStatic.bPortReading = true;
                try
                {

                    sTempPort = sp.ReadExisting();
                    if (sTempPort.Length > 0)
                    {
                        CmdGet = sTempPort;
                    }

                }
                catch (Exception err)
                {
                    rs232status = "Error in reading data: " + err.Message;
                    SetTxtText(rs232status, txtstate, frm);
                    sTempPort = "";
                    //for (int i = 0; i < MyStatic.Parm.Length; i++) { MyStatic.Parm[i] = 0; }
                    return false;

                }

                return true;
            }


            return false;
        }

        public Boolean MdriveGoToPosition(string mDrivePos1, string mDrivePos2, string mDrivePos3, string mDrivePos4, string mDrivePos5, ref string ErrMessage)
        {



            MdrivePosition[0] = mDrivePos1;
            MdrivePosition[1] = mDrivePos2;
            MdrivePosition[2] = mDrivePos3;
            MdrivePosition[3] = mDrivePos4;
            MdrivePosition[4] = mDrivePos5;


            if (!SendToMdrive("ALL", "MA  " + MdrivePosition[0], ref ErrMessage, 1000)) return false;
            return true;
        }

        //IF NO NAME MOTOR SEND ""
        public Boolean SendToMdrive(string MotorName, string Cmd, ref string Error, long tmout = 1000)//====2013
        {
            string cmd = "";


            //if (Cmd == 2) { cmd = "MR"; CmdName = "Relative"; };
            //if (Cmd == 3) { cmd = "MA"; CmdName = "Absolute"; };
            //if (Cmd == 4) { cmd = "PR P"; CmdName = "CurrentPosition"; };
            //if (Cmd == 5) { cmd = "PR MV"; CmdName = "CurrentVelosity"; };
            //if (Cmd == 6) { cmd = "P=0"; CmdName = "P=0"; };
            //if (Cmd < 1 || Cmd > 7) return false;
            if (Cmd.Substring(0, 2) == "MA") { CmdName = "GoToPositions"; }
            if (Cmd.Substring(0, 2) == "MR") { CmdName = "GoToPositions"; }

            if (Cmd.Substring(0,2) == "HM" || Cmd.Substring(0, 2) == "HI") { CmdName = "Homing"; }
            //if (cmd.Substring(0, 2) == "MR" || cmd.Substring(0, 2) == "MA") { CmdName = "MOVING"; }
            //if (cmd == "PR P") { CmdName = "POSITION"; }
            DeviceName = MotorName;
            if (DeviceName == "ALL")
            {
                DeviceName = "1";
                AllDevices = true;
                Command = CmdName;
            }
            DateTime NowTime = DateTime.Now;
            DateTime startTime = DateTime.Now;
            MyStatic.RobotErrorMess = false;
            SendParms MdParms = new SendParms();
            MdParms.cmd = (Cmd);
            MdParms.comment = "";
            Array.Resize<float>(ref MdParms.SendParm, 1);
            MdParms.SendParm[0] = tmout;
            FlashPort();
            while (bwMDrive.IsBusy) //wait end busy for 2 sec
            {
                NowTime = DateTime.Now;
                if (2 < (((int)NowTime.Subtract(startTime).TotalMilliseconds)) / 1000) break;
            }
            // while (bwMDrive.IsBusy);
            if (!bwMDrive.IsBusy)
            {

                bwMDrive.RunWorkerAsync(MdParms);//====2013
            }
            else
            {
                bwMDrive.CancelAsync();
                MyStatic.bReset = true;
                Thread.Sleep(1000);
                MyStatic.bReset = false;
                Error = "ERROR: Connection Busy" + "Can't run the worker twice! Action:" + cmd;
                // AddToList(Error);
                //dFile.WriteLogFile(Error);
                return false;
            }

            return true;
        }

        public void bwMDrive_DoWork(object sender, DoWorkEventArgs e)
        {
            CommReply MdReply = new CommReply();

            if (Thread.CurrentThread.Name == null) { Thread.CurrentThread.Name = "BWmdrive"; }
            //task for running in background
            if (e.Argument != null)
            {
                e.Cancel = false;

                SendParms MdParms = (SendParms)e.Argument;
                Single[] arg = MdParms.SendParm;
                String cmd = DeviceName + MdParms.cmd;
                //create parameters

                //send robot last parameter e.Argument.length-1 is timeout

                //send to robot

                if (!SendMdrive(cmd + "\r"))//send cmd
                {
                    MdReply.result = false;

                    MdReply.comment = MdParms.comment;//====2013
                    e.Result = MdReply;

                    return;
                }

                sMdriveSnd = "";


                Single tmout = arg[arg.Length - 1];
                MdReply.cmd = cmd;
                if (!WaitMdrive(tmout, ref MdReply)) //wait OK on send
                {
                    MdReply.result = false;
                    MdReply.status = rs232status + " cmd" + cmd.ToString();

                    MdReply.comment = MdParms.comment;//====2013
                    e.Result = MdReply;


                    return;
                }
                else
                {
                    MdReply.result = true;
                    MdReply.status = rs232status;

                    //MdReply. = cmd;

                    MdReply.comment = MdParms.comment;//====2013
                    e.Result = MdReply;

                    return;
                }
            }
        }
        #region ---------Wait Port--------------
        private Boolean WaitMdrive(Single TimeOut, ref CommReply reply)
        {

            //SetTxtText(rs232status, txtstate, frm);

            //if (sp.IsOpen & MyStatic.bPortReading)
            bool timeout_flag = false;
            bool readport_flag = false;

            if (sp.IsOpen)
            {
                bReadPort = true;
                DateTime startTime = DateTime.Now;
                DateTime NowTime = DateTime.Now; ;
                string cmdget = "";
                string cmdget1 = "";

                try
                {
                    Stopwatch stopper = new Stopwatch();
                    stopper.Restart();
                    do
                    {
                        cmdget1 = "";
                        if (!ReadPort(tokenSource.Token, ref cmdget1))
                        {
                            MyStatic.bPortReading = false; readport_flag = true; break;
                        }
                        if (cmdget1 != "") cmdget = cmdget + cmdget1;

                        MyStatic.bPortReading = true;
                        if ((TimeOut > 0) & stopper.ElapsedMilliseconds > TimeOut)
                        { timeout_flag = true; stopper.Stop(); break; }
                        if (MyStatic.bReset) break;
                        Thread.Sleep(1);
                        

                    }
                    while (cmdget.IndexOf(reply.cmd) < 0);// in cmdget ==""));

                    Thread.Sleep(50);
                    cmdget = cmdget + sp.ReadExisting();

                    
                    if (timeout_flag)
                    {
                        // SetTextLst("===TIMEOUT ERROR====" +
                        //  " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                        MainHMI.NewMainHMI.ListAdd("===TIMEOUT ERROR====" +
                       " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");

                        rs232status = "Error in wait GetCmd";
                        SetTxtText(rs232status, txtstate, frm);
                        reply.reply = "TIMEOUT ERROR";
                        return false;
                    }
                    if (MyStatic.bReset)
                    {
                        //    SetTextLst("===RESET====" +
                        //  " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                        MainHMI.NewMainHMI.ListAdd("===RESET====" +
                       " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");



                        rs232status = "Error Mdrive Reset";
                        SetTxtText(rs232status, txtstate, frm);
                        reply.reply = "RESET";
                        return false;

                    }
                    if (readport_flag)
                    {
                        //        SetTextLst("===READ PORT ERROR====" +
                        //    " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);

                        MainHMI.NewMainHMI.ListAdd("===READ PORT ERROR====" +
                     " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");
                        rs232status = "Error Read Port";
                        SetTxtText(rs232status, txtstate, frm);
                        reply.reply = "READ PORT ERROR";
                        return false;

                    }
                    reply.reply = cmdget;
                    //ok read port
                    // reply.reply = cmdget.Substring((reply.cmd+"\r\n").Length,cmdget.Length);
                    //cmdget = cmdget.Replace(" ", "");
                    reply.reply = reply.reply.Replace(">", "");
                    //olgaif (cmdget.IndexOf('?') >= 0) SetTextLst("--->?   " + cmdget +
                    //  " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                    
                    reply.reply = reply.reply.Replace("\r", "");
                    reply.reply = reply.reply.Replace("\n", "");
                    reply.reply = reply.reply.Replace("?", "");
                    //if (reply.reply.Substring(0, 1) == reply.reply.Substring(0, 1))
                        reply.reply= reply.reply.Remove(0, 1);
                   // reply.reply = reply.reply.Substring(((reply.cmd).Length), reply.reply.Length - (reply.cmd).Length);



                    MainHMI.NewMainHMI.ListAdd("--->   " + reply.reply +
                   " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")");

                    return true;
                    // if (cmdget == 1) { return true; } else { return false; }
                }
                catch (Exception err)
                {
                    rs232status = "Error in wait GetCmd" + err.Message; return false;
                }

            }

            return false;
        }
        #endregion
        public void bwMDrive_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        //autocycle for robot
        {

            MyE.Cmd = "";
            //send all params to robot
            while (bwMDrive.IsBusy) { Thread.Sleep(5); };//wait fini bwMDrive
            //try
            //{
            //    frmMain.newFrmMain.toolStripProgressBar1.Value = 0;
            //    frmMain.newFrmMain.toolStripProgressBar1.Visible = false;
            //}
            //catch { }
            CommReply MdReply = new CommReply();
            //SendParms Parms = new SendParms();

            if (e.Result != null) MdReply = (CommReply)e.Result;
            string Error = "Mdrive Communication Error";
            if (e.Result == null || !MdReply.result)
            {
                try
                {
                    Error = MdReply.status;
                    MdReply.cmd = "ERROR";
                    MdReply.result = false;
                    GetFini(MdReply);
                    return;
                }
                catch { }
                return;
            }


            try
            {
                if ((MyStatic.bReset) || (!MyStatic.bPortReading))
                {

                    try
                    {

                    }
                    catch { }

                    return;
                }
                string[] s = MdReply.cmd.Split(' ');
                //s[0] = s[0].Substring(0, s[0].Length - 1);
                string cmd;
                string ErrMessage = "";
                switch (s[0].Trim())
                {
                    case "PR":
                        if (s[1] == "P")
                        {

                            Array.Resize<float>(ref MdReply.data, 1);
                            MyE.Cmd = "Position";

                            //MdReply.data[0] = Single.Parse(MdReply.reply);
                           
                          //  GetFini(MdReply);
                            break;
                        }
                        else if (s[1] == "MV")
                        {
                            Array.Resize<float>(ref MdReply.data, 1);
                            MdReply.data[0] = Single.Parse(MdReply.reply);
                            if (MdReply.reply == "1")
                            {

                                if (!SendToMdrive(DeviceName, "PR MV", ref ErrMessage, 1000))
                                {
                                    MessageBox.Show("Mdrive Send Data Error ERROR" + " " + DeviceName);
                                    return;
                                }
                                return;
                            }
                            else
                            {

                                if (Command == "Homing")
                                {

                                    if (!SendToMdrive(DeviceName, "P=0", ref ErrMessage, 1000))
                                    {
                                        MessageBox.Show("Mdrive Send Data Error ERROR" + " " + DeviceName);
                                        return;
                                    }
                                    return;


                                }

                                if (CmdName == "GoToPositions")
                                {

                                    if (!SendToMdrive(DeviceName, "PR P", ref ErrMessage, 1000))
                                    {
                                        MessageBox.Show("Mdrive Send Data Error ERROR" + " " + DeviceName);
                                        return;
                                    }

                                    //MdReply.reply = "OK";
                                    //MyE.Cmd = CmdName;
                                    //Command = "Position";
                                    //GetFini(MdReply);

                                    // MdReply.data[0] = Single.Parse(MdReply.reply);
                                    return;




                                }
                                if (CmdName == "Homing")
                                {
                                    if (!SendToMdrive(DeviceName, "P=0", ref ErrMessage, 1000))
                                    {
                                        MessageBox.Show("Mdrive Send Data Error ERROR" + " " + DeviceName);
                                        return;
                                    }
                                    return;
                                }

                                if (!SendToMdrive(DeviceName, "PR P", ref ErrMessage, 1000))
                                {
                                    MessageBox.Show("Mdrive Send Data Error ERROR" + " " + DeviceName);
                                    return;
                                }
                                break;



                            }


                        }
                        else
                        {
                            MdReply.result = false;
                            break;
                        }
                        break;
                    case "P=0":

                        if (AllDevices)
                        {
                            int tmp = Convert.ToInt32(DeviceName) + 1;
                            if (tmp > 5)
                            {
                                DeviceName = "All";
                                MdReply.reply = "OK";
                                MyE.Cmd = "Homing";
                                AllDevices = false;
                                Command = "";
                                GetFini(MdReply);
                                break;
                            }
                            if (!SendToMdrive(tmp.ToString(), "PR MV", ref ErrMessage, 1000))
                            {
                                MessageBox.Show("Mdrive Send Data Error ERROR" + " " + DeviceName);
                                return;
                            }
                            MdReply.reply = "OK";
                            // MdReply.data[0] = Single.Parse(MdReply.reply);
                            return;

                        }
                        MdReply.reply = "OK";
                        MyE.Cmd = "Homing";
                        AllDevices = false;
                        Command = "";
                        GetFini(MdReply);
                        break;
                        break;
                    case "MR":

                        MdReply.reply = "OK";



                        if (!SendToMdrive(DeviceName, "PR MV", ref ErrMessage, 1000))
                        {
                            MessageBox.Show("Mdrive Send Data Error ERROR" + " " + DeviceName);
                            return;
                        }
                        return;


                        break;
                    case "MA":
                        MdReply.reply = "OK";


                       
                                if (!SendToMdrive("", "PR MV", ref ErrMessage, 1000))
                                {

                                    MessageBox.Show("Mdrive Send Data Error ERROR" + " " + DeviceName);
                                    return;
                                }
                                return;
                           
                          
                    case "S1=3,1,0":
                        break;
                    case "HM":
                        if (AllDevices)
                        {
                            int tmp = Convert.ToInt32(DeviceName) + 1;
                            if (tmp > 5)
                            {
                                if (!SendToMdrive("1", "PR MV", ref ErrMessage, 1000))
                                {
                                    MessageBox.Show("Mdrive Send Data Error ERROR" + " " + DeviceName);
                                    return;
                                }
                                return;
                            }
                            if (!SendToMdrive(tmp.ToString(), "HM 1", ref ErrMessage, 1000))
                            {
                                MessageBox.Show("Mdrive Send Data Error ERROR" + " " + DeviceName);
                                return;
                            }
                            MdReply.reply = "OK";
                            // MdReply.data[0] = Single.Parse(MdReply.reply);
                            return;

                        }

                        if (s[1] == "1")
                        {
                            if (!SendToMdrive(DeviceName, "PR MV", ref ErrMessage, 1000))
                            {
                                MessageBox.Show("Mdrive Send Data Error ERROR" + " " + DeviceName);
                                return;
                            }
                            MdReply.reply = "OK";
                            // MdReply.data[0] = Single.Parse(MdReply.reply);
                            return;
                        }
                        else
                        {

                            MdReply.result = false;
                            break;
                        }

                    default:
                        if (s[0].Substring(0, 2) == "Q1")
                        {
                            Array.Resize<float>(ref MdReply.data, 1);

                            MdReply.reply = "OK";
                            MyE.Cmd = "Position";
                            AllDevices = false;
                            Command = "";
                            MdReply.data[0] = 1;
                            GetFini(MdReply);
                            return;
                        }
                        else
                        {
                            MdReply.result = false;
                            break;
                        }
                        break;
                }

                if (MdReply.reply == "")
                {
                    MessageBox.Show("Mdrive Reply DATA ERROR" + " " + DeviceName);
                    return;
                }



            }
            catch
            {
                MdReply.result = false;
                MessageBox.Show("Mdrive Reply DATA ERROR" + DeviceName);
                return;
            }


        }

        // end functions

    }
}
