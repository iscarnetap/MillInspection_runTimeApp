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





namespace Inspection
{


    class CommFunctions
    {
                
       
       
        
        delegate void SetButtonCallback(Boolean en, Button btn, Form frm);
        
        public string status = "";
        public ListBox lstSend;

        //public TextBox txtstate, textwait;
        public TextBox text11, text12,text13,text14,text15,text16;
        
        public Form frm;
        
        public Boolean bExitcycle=false;
        public string RobotCmd = "";
        public string RobotReport = "";
        public Single[] ParmGet = new Single[30];
        Stopwatch stopwatch = new Stopwatch();
        
        public int[] CmdRobot1 = { 112,120,121,122,123,124,125,210,211,212,213,214,215,216,217,218 };
        public string RobotName="" ;
        public string RobotProgram = "";
        public Boolean bPortReading = false;
        public DataIniFile dFile = new DataIniFile();
       
        public struct SendRobotParms //====2013
        {
            public string comment;
            public Single[] SendParm;
            public bool NotSendMess;
            public string cmd;
            //public int FunctionCode;
        }
        public struct SendHostParms //====2013
        {
            public string comment;
            public string[] cmd;
            public float timeout;
            
        }
        public struct SendPlcParms //====2013
        {
            public string comment;
            public Single[] SendParm;
            public int FunctionCode;
        }
        public struct CommReply
        {
            public bool result;
            public float[] data;
            public string status;
            public string comment;//====2013
            public int FunctionCode;
            public string Error;
        }
        public struct HostReply
        {
            public bool result;
            public string reply;
            public string cmd;
            public string[] data;
            public string status;
            public string comment;//====2013
            public string error;
            public byte[] BYTE;
        }
        public  Socket n_socClient; //= new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

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
        public struct position
        {
            public Double x;
            public Double y;
            public Double z;
            public Double r;
            public Double corrX;
            public Double corrY;
            public Double corrZ;

            public int Rotate;
            public int EndOfTray;
            public string Error;
        }

        public  string Robot_buffer="";
        SendRobotParms Parms0 = new SendRobotParms();
        
        public void SetControls(ListBox LstSend,  Form Frm)
        {
            lstSend = LstSend;
            //txtstate = Txtstate;
            frm = Frm;
           
        }
        
        

        #region ---------Background Worker--------------

        public  CommFunctions()
        {
           
        }
        #endregion

        #region ------------HOST PORT Procedures------------------

        //printers
      

        public Boolean SendToUserPort2(string Send, ref string Error, ref CommReply RobotUserReply,bool waitrep=true)//====2013
        {
            try
            {
                StopWatch1.Restart();
                //open connection
                MyStatic.RobotErrorMess = false;
                string send1 = Send.Replace("ESC", "\x1b");
                string sparms = send1 + "\r";
                
                if (!SendData(sparms))//send cmd
                {
                    Error = "ERROR SEND DATA " + RobotUserReply.comment;
                    RobotUserReply.comment = "ERROR SEND DATA ";
                    return false;
                }

                //wait reply
                if (waitrep)
                {
                    if (!StartWaitRobotUser2(ref Error, ref RobotUserReply))
                    {
                        return false;
                    }
                }
                
                //close connection
                return true;
            }
            catch
            {

                return false;
            }
        }

        public Boolean StartWaitRobotUser2(ref string Error, ref CommReply RobotUserReply)//====2019
        {

            try
            {
                DateTime NowTime = DateTime.Now;
                DateTime startTime = DateTime.Now;
                MyStatic.RobotErrorMess = false;
                Stopwatch stopw = new Stopwatch();
                int tmout = 5;
                

                do
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                    if (MyStatic.bReset) return false;
                    Thread.Sleep(1);
                    string getstring = "";
                    if (MyStatic.Speed <= 0) MyStatic.Speed = 5;
                    short timeK = (short)(100 / MyStatic.Speed);

                    if ((tmout > 0) & (tmout * 1000 < StopWatch1.ElapsedMilliseconds))
                    {
                        bPortReading = false;
                        break;
                    }


                    GetUserCmd(ref getstring, tmout);

                    if (getstring != "")
                    {
                        RobotUserReply.comment = getstring;
                        return true;
                    }

                    else
                    {
                        return false;
                    }

                }
                while (true);

                return true;
            }
            catch
            {
                return false;
            }
        }


        public Boolean SendToHostPort1(SendHostParms SendParams, ref string Error, ref HostReply RobotHostReply)//====2013
        {

            try
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
                //string stx = Encoding.ASCII.GetString(new byte[] { 0x02 });
                //string etx = Encoding.ASCII.GetString(new byte[] { 0x03 });
                //string cr = Encoding.ASCII.GetString(new byte[] { 13 });

                //sparms = stx + sparms + cr + etx;

                //send to robot


                //HostReply RobotHostReply = new HostReply();

                if (!SendData(sparms))//send cmd
                {
                    RobotHostReply.result = false;
                    RobotHostReply.comment = SendParams.comment;//====2013
                    SetTextLst("ERROR SEND DATA " + RobotHostReply.comment + "  (" + DateTime.Now.ToString() + ")", lstSend, frm);
                    SetTxtText("Error in reading data", text11, frm);
                    Error = "ERROR SEND DATA " + RobotHostReply.comment;
                    return false;
                }

                if (SendParams.comment == "file")
                {
                    if (!StartWaitHostFile(SendParams, ref Error, ref RobotHostReply))
                    {
                        return false;
                    }
                    RobotHostReply.result = true;
                    return true;

                }
                else
                {

                    if (!StartWaitRobotHost1(SendParams, ref Error, ref RobotHostReply))
                    {
                        return false;
                    }

                }


                return true;
            }
            catch
            {
                return false;
            }
        }
        public bool StartWaitRobotHost1(SendHostParms SendParams, ref string Error,ref HostReply RobotHostReply)
        {
            try
            {
                DateTime NowTime = DateTime.Now;
                DateTime startTime = DateTime.Now;
                
                SendHostParms RobotParms = SendParams;// (SendHostParms)e.Argument;
                string[] arg = RobotParms.cmd;

                Single tmout = RobotParms.timeout;
                Boolean timeout_flag = false;
                //bPortReading = true;


                do
                {
                    Application.DoEvents();
                    Thread.Sleep(50);
                    
                    NowTime = DateTime.Now;
                    if ((tmout > 0) & (tmout < (((int)NowTime.Subtract(startTime).TotalMilliseconds)) / 1000))
                    { timeout_flag = true; break; }
                    string getstring = "";
                    byte[] getbyte = new byte[255];
                    if (!GetHostCmd(ref getstring, ref getbyte, tmout))
                    {
                        
                        RobotHostReply.reply = "";
                        RobotHostReply.result = false;
                        RobotHostReply.status = ClientStatus + " " + arg[0];
                       
                        RobotHostReply.comment = RobotParms.comment;//====2013

                        return false;
                    }


                    if ((getstring != null) & (getstring != ""))
                    {
                        string sTempPort1 = getstring;
                        
                            RobotHostReply.comment = sTempPort1;
                        RobotHostReply.result = true;
                        return true;



                    }
                    else
                    {
                        RobotHostReply.result = false;
                        RobotHostReply.status = ClientStatus + " HOST" + RobotHostReply.cmd;
                        
                        RobotHostReply.comment = RobotParms.comment;//====2013
                        
                        return false;
                    }
                }
                while (true);


                if (timeout_flag)
                {

                    RobotHostReply.result = false;
                    RobotHostReply.status = ClientStatus + " cmd" + RobotHostReply.cmd;

                    RobotHostReply.comment = RobotParms.comment;//====2013
                    
                    return false;

                }
                return true;
            }
            catch
            {
                
                return false;
            }



        }
        public bool StartWaitHostFile(SendHostParms SendParams, ref string Error, ref HostReply RobotHostReply)
        {
            try
            {
                DateTime NowTime = DateTime.Now;
                DateTime startTime = DateTime.Now;
                
                SendHostParms RobotParms = SendParams;// (SendHostParms)e.Argument;
                string[] arg = RobotParms.cmd;

                Single tmout = RobotParms.timeout;
                Boolean timeout_flag = false;
                

               
                    if (MyStatic.bReset) return false;
                    Thread.Sleep(50);

                    NowTime = DateTime.Now;
                    if ((tmout > 0) & (tmout < (((int)NowTime.Subtract(startTime).TotalMilliseconds)) / 1000))
                    { timeout_flag = true;
                    

                        RobotHostReply.result = false;
                        RobotHostReply.status = ClientStatus + " cmd" + RobotHostReply.cmd;

                        RobotHostReply.comment = RobotParms.comment;//====2013
                                                                    //e.Result = RobotHostReply;
                        MessageBox.Show("Timeout");
                        return false;

                    
                    }
                    string getstring = "";
                    byte[] getbyte = new byte[255];
                
                if (!GetHostFile(ref getstring, ref getbyte, tmout))
                {
                    
                    RobotHostReply.reply = "";
                    RobotHostReply.result = false;
                    RobotHostReply.status = ClientStatus + " " + arg[0];
                    
                    RobotHostReply.comment = RobotParms.comment;//====2013

                    return false;
                }
                else
                {



                   
                    RobotHostReply.comment= "READ FILE " + getstring;
                    RobotHostReply.BYTE = getbyte;
                    return true;
                }
               

                return true;
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
                return false;
            }



        }


        public Boolean GetHostCmd(ref string getstring, ref byte[] getbyte, float tmout)
        {

            try
            {
                StopReceive = true;
                
                Stopwatch stopper = new Stopwatch();
                stopper.Restart();

                while (StopReceive)
                {
                    Application.DoEvents();
                    Thread.Sleep(5);
                    if (MyStatic.bReset) break;
                    if ((tmout > 0) & (tmout * 1000 < stopper.ElapsedMilliseconds))
                    { 
                        return false; 
                    }
                    
                    byte[] buffer = new byte[512];
                    int tout = (int)tmout * 1000000;
                    if (tout == 0) tout = 10000;
                   
                    if (n_socClient.Poll(tout, SelectMode.SelectRead))
                   
                    {

                        int iRx = 0;
                        int end = 0;

                        try
                        {
                            iRx = n_socClient.Receive(buffer);
                        }
                        catch (SocketException E)
                        {
                              Debug.Print("51 "+E.Message);//return;
                        }
                        
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
                            SetTextLst("--->   " + szData +
                            " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);

                            if ((szData != null) && (szData != ""))
                            {
                                getstring = szData;
                                return true;

                            }//host

                        }
                    }
                    
                }
                return false;
            }
            catch (SocketException se) {  Debug.Print("52"); ClientStatus = se.ToString(); return false; }
        }
        public Boolean GetHostFile(ref string getstring, ref byte[] getbyte, float tmout)
        {
            
            try
            {
                Stopwatch stopper = new Stopwatch();
                stopper.Start();
                string stemp = "";
                byte[] buffer = new byte[1];
                int tout = (int)tmout * 1000000;
                while (true)
                {
                    Application.DoEvents();
                    Thread.Sleep(5);
                    if (MyStatic.bReset) break;
                    if ((tmout > 0) & (tmout * 1000 < stopper.ElapsedMilliseconds))
                    { return false;
                    }
                
                        
                    if (n_socClient.Poll(tout, SelectMode.SelectRead))
                    
                    {

                        int iRx = 0;
                        iRx = n_socClient.Receive(buffer);
                        if (iRx > 0)
                        {
                            char[] chars = new char[iRx];
                            Thread.Sleep(1);
                            System.Text.Decoder d = System.Text.Encoding.ASCII.GetDecoder();
                            int charLen = d.GetChars(buffer, 0, iRx, chars, 0);
                            getbyte = buffer;
                            System.String szData = new System.String(chars);
                            stemp = stemp + szData;
                           
                            if (stemp.IndexOf("\r\n") > 0)
                            {
                                break;
                            }
                            
                        }

                    }
                }
                        
                stopper.Start();
                int intNS = 0;
                byte[] data = new byte[5000000];
                NetworkStream NS = new NetworkStream(n_socClient);
                int allbytes = 0;
                
                while (NS.CanRead && !MyStatic.bReset)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                    intNS = NS.Read(data, allbytes, data.Length - allbytes);
                    allbytes = allbytes + intNS;
                    Thread.Sleep(10);
                    int inbytes = n_socClient.Available;
                    if (inbytes <= 0 || intNS==0) break;
                    
                }
                Array.Resize<byte>(ref getbyte, allbytes);
                Array.Resize<byte>(ref data, allbytes);
                getbyte = data;
                getstring = getbyte.Length.ToString();
                NS.Close();
                return true;

              
            }
            catch (SocketException se)
            { Debug.Print("52"); ClientStatus = se.ToString();
                
                return false;
            }
            return true;
        }


        #region -----------------USER PORT Procedures----------------

        public Boolean SendToUserPort1(SendRobotParms SendParams, ref string Error, ref CommReply RobotUserReply)//====2013
        {
            try
            {
                StopWatch1.Restart();

                MyStatic.RobotErrorMess = false;

                Single[] arg = SendParams.SendParm;
                arg[9] = 0;
                for (int i = 0; i < 9; i++) arg[9] = arg[9] + arg[i];
                //create parameters
                string sparms = ((int)arg[0]).ToString();
                for (int i = 1; i < arg.Length - 1; i++)
                {
                    sparms = sparms + "," + String.Format("{0:0.00}", arg[i]);
                }
                sparms = sparms + "\r";
                //send robot last parameter e.Argument.length-1 is timeout

                //send to robot
                //CommReply RobotUserReply = new CommReply();
                if (SendParams.NotSendMess == null || !SendParams.NotSendMess)
                {
                    if ((arg[0] != 1) || (arg[0] != 101))//cmd start from 10 ,1 onle for read port
                    {


                        if (!MyStatic.chkDebug)
                        {
                            if (!SendData(sparms))//send cmd
                            {
                                RobotUserReply.result = false;
                                //RobotUserReply.FunctionCode = SendParams.FunctionCode;//====2013
                                RobotUserReply.comment = SendParams.comment;//====2013
                                SetTextLst("ERROR SEND DATA " + RobotUserReply.comment + "  (" + DateTime.Now.ToString() + ")", lstSend, frm);
                                SetTxtText("Error in reading data", text11, frm);
                                Error = "ERROR SEND DATA " + RobotUserReply.comment;
                                return false;
                            }
                        }
                        else
                        {
                            SetTextLst("<---   " + sparms +
                    " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                        }
                    }
                }

                int cmd = (int)arg[0];
                Single tmout = arg[arg.Length - 1];
                //wait robot

                if (!StartWaitRobotUser1(SendParams, ref Error, ref RobotUserReply))
                {
                    return false;
                }
                //}

                return true;
            }
            catch
            {
                
                return false;
            }
        }
        public Boolean StartWaitRobotUser1(SendRobotParms SendParams, ref string Error, ref CommReply RobotUserReply)//====2019
        {

            try
            {
                DateTime NowTime = DateTime.Now;
                DateTime startTime = DateTime.Now;
                MyStatic.RobotErrorMess = false;
                Stopwatch stopw = new Stopwatch();
                //CommReply RobotUserReply = new CommReply();

                float[] myparm = { 0 };
                stopw.Start();
                if (SendParams.GetType().IsValueType)
                {

                    SendRobotParms RobotParms = SendParams;
                    Single[] arg = RobotParms.SendParm;
                    int cmdwait = (int)arg[0];
                    Single tmout = arg[arg.Length - 1];
                    // if (tmout == 0) MessageBox.Show("ERROR TIMEOUT");
                    Boolean timeout_flag = false;
                    bPortReading = true;


                    if (InArray(CmdRobot1, cmdwait))//wait reply
                    {
                        do
                        {
                            Application.DoEvents();
                            Thread.Sleep(10);
                            if (MyStatic.bReset) return false;
                            Thread.Sleep(1);
                            string getstring = "";
                            if (MyStatic.Speed <= 0) MyStatic.Speed = 5;
                            short timeK = (short)(100 / MyStatic.Speed);




                            if ((tmout > 0) & (tmout * 1000 < StopWatch1.ElapsedMilliseconds))
                            {
                                timeout_flag = true;
                                bPortReading = false;
                                break;
                            }

                            if (Robot_buffer == "")
                            {
                                GetUserCmd(ref getstring, tmout);
                                if ((getstring == "") && (cmdwait != 1) && (RobotCmd != ""))
                                {

                                    getstring = RobotCmd;
                                    SetTextLst("---> *** timeout " + getstring + " ****" +
                                        " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                                    RobotUserReply.result = false;
                                    return false;

                                }

                                else if ((getstring == "") && (cmdwait != 1) && (RobotCmd == ""))
                                {
                                    bPortReading = false;
                                    RobotUserReply.result = false;
                                    RobotUserReply.status = ClientStatus + " cmd" + cmdwait.ToString();
                                    Array.Resize<Single>(ref RobotUserReply.data, 2);
                                    RobotUserReply.data[0] = 0;
                                    RobotUserReply.data[1] = cmdwait;
                                    //RobotUserReply.FunctionCode = RobotParms.FunctionCode;//====2013
                                    RobotUserReply.comment = RobotParms.comment;//====2013
                                                                                //e.Result = RobotUserReply;
                                    return false;
                                }
                            }
                            else
                            {
                                getstring = Robot_buffer;
                                Robot_buffer = "";
                                SetTextLst("--->>> ***** " + getstring + " *****" +
                           " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                            }

                            //}

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
                                        return false;

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

                            }
                        }
                        while (true);

                        if (timeout_flag)
                        {
                            bPortReading = false;
                            RobotUserReply.result = false;
                            RobotUserReply.status = ClientStatus + " cmd" + cmdwait.ToString();
                            //RobotUserReply.FunctionCode = RobotParms.FunctionCode;//====2013
                            RobotUserReply.comment = RobotParms.comment;//====2013
                                                                        //e.Result = RobotUserReply;

                            return false;

                        }

                        //SetTxtText(StopWatch1.ElapsedMilliseconds.ToString(), text12, frm);///////////////////////////////////////
                        bPortReading = false;

                        RobotUserReply.result = true;
                        RobotUserReply.status = ClientStatus;
                        RobotUserReply.data = myparm;
                        RobotUserReply.data[0] = 0;// cmdwait;
                                                   //RobotUserReply.FunctionCode = RobotParms.FunctionCode;//====2013
                        RobotUserReply.comment = RobotParms.comment;//====2013
                                                                    //e.Result = RobotUserReply;
                    }

                }

                return true;
            }
            catch
            {
               
                return false;
            }
        }
        
       
        
        public Boolean InArray(int[] arr, int cmd)
        {            
            int first=  Array.IndexOf<int>(arr, cmd);
            if (first >= 0) return true; else return false;
        }
        //public bool bPortReading = false;
        //public static MyStatic.rowActionDebug rowActDebug = new MyStatic.rowActionDebug();
        
        public Boolean GetUserCmd(ref string getstring, float tmout)
        {
           
            try
            {
                
                               
                    while (!MyStatic.bReset)
                    {
                    Application.DoEvents();
                    Thread.Sleep(10);
                        if (MyStatic.bReset) break;
                        
                        if ((tmout > 0) & (tmout * 1000 < StopWatch1.ElapsedMilliseconds))
                        {  return false; }
                        
                        byte[] buffer = new byte[1024];
                        //if (m_socClient.Available > 0)//check  data in port
                        int tout = (int)tmout * 1000000;
                        if (tout == 0) tout = 10000; 
                        if (n_socClient.Poll(tout,SelectMode.SelectRead))//check  data in port
                        {
                            
                            int iRx = 0;
                            
                            try
                            {
                                iRx = n_socClient.Receive(buffer);
                            }
                            catch (SocketException E)
                            {
                                  Debug.Print("56 " +E.Message); return false;
                            }
                            if (iRx>0)
                            {
                            char[] chars = new char[iRx];
                            Thread.Sleep(1);
                            System.Text.Decoder d = System.Text.Encoding.UTF8.GetDecoder();
                            int charLen = d.GetChars(buffer, 0, iRx, chars, 0);
                            System.String szData = new System.String(chars);
                            string s="";
                            for (int ii = 0; ii < chars.Length; ii++)
                            {int u = (int)chars[ii];
                                s=s+" "+u.ToString();
                            
                            }
                            SetTextLst("--->   " + szData +
                            " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                            
                                //int cmd=0;
                                getstring = szData;
                                szData = "";//enother worker;
                                return true;

                           
                        }
                    }
                        //return false;
                }
                    return false;
            }
            catch (SocketException se) {  Debug.Print("57"); ClientStatus = se.ToString(); return false; }
        }
        public void ClearPort(float tmout)
        {

            try
            {

                 Stopwatch sw = new Stopwatch();
                
                while (!MyStatic.bReset)
                {
                    Thread.Sleep(2);
                    Application.DoEvents();
                    if ((tmout > 0) & (tmout * 1000 < sw.ElapsedMilliseconds))
                    { return ; }
                   
                    byte[] buffer = new byte[2048];
                    if (n_socClient!=null && n_socClient.Available > 0)//check  data in port
                    {

                        int iRx = 0;

                        try
                        {
                            iRx = n_socClient.Receive(buffer);
                        }
                        catch (SocketException E)
                        {
                              Debug.Print("58 "+E.Message);//return;
                        }
                    }
                    else return;
                    //return false;
                }
                return ;
            }
            catch (SocketException se) {  Debug.Print("59"); ClientStatus = se.ToString(); return; }
        }

        #endregion

        /// <summary>
        /// ////////////////////////////////////////////////////////////////

        /// ////////////////////////////////////////////////////////////////


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
                    txt.Text = text;
                }
            }
            catch { }

        }
        delegate void SetListText(string text, ListBox lst, Form frm);
        public void SetTextLst(string text, ListBox lst, Form frm)
        {
            //if (!Enable) { return; }
            if ((lst == null) || (frm == null)) { return; }

            try
            {
                if (lst.InvokeRequired)
                {
                    SetListText d = new SetListText(SetTextLst);
                    frm.Invoke(d, new object[] { text, lst, frm });
                }
                else
                {
                    lst.Items.Add(text);
                    if (lst.Items.Count > 500) { lst.Items.Clear(); }
                }
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
            }
            catch {   Debug.Print("63"); }


        }
        #endregion
               
        
        private  String response = String.Empty;

        #endregion
        #region ------------Socket INIT Procedures-------------------------
        public void GetPort(ref Socket mini_socClient) //= new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);)
        {
            n_socClient = mini_socClient;
        }
        public Boolean ConnectClient(string port, string address, ref string error)
        {
            try
            {
                string err = "";
                if (n_socClient == null)
                {
                                        
                    
                    n_socClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    
                    IPGlobalProperties ipg = IPGlobalProperties.GetIPGlobalProperties();
                    IPEndPoint[] endpoint = ipg.GetActiveTcpListeners();
                    int myPort = System.Convert.ToInt16(port, 10);
                    System.Net.IPAddress remoteIPAddress = System.Net.IPAddress.Parse(address);
                    
                    Ping p1 = new Ping();
                    PingReply PR = p1.Send(address);
                    if (!PR.Status.ToString().Equals("Success"))
                    {
                        error = "ERROR CONNECTION 12" + address + "\r";
                        
                        return false;
                    }
                }

                if (!ClientConnected())
                {
                    if (!ConnectPort(port, address, ref err))
                    {
                       
                        error = error + "ERROR CONNECTION 13" + err + "\r";
                        return false;
                    }
                    bPortReading = true;
                    return true;
                    
                }

                return true;
            }
            catch (Exception ex)
            {
                error=ex.Message;
                return false;
            }
        }
        public Boolean ConnectPort1(string Port, string address, ref string Error)
        {

            try {
                String szIPSelected = address;
                String szPort = Port;
                int alPort = System.Convert.ToInt16(szPort, 10);

                System.Net.IPAddress remoteIPAddress = System.Net.IPAddress.Parse(szIPSelected);
                System.Net.IPEndPoint remoteEndPoint = new System.Net.IPEndPoint(remoteIPAddress, alPort);
                n_socClient.Connect(remoteIPAddress,alPort);
                

                return true;
                }
            catch (SocketException se)
            {
                
                Error = se.Message;
                n_socClient.Disconnect(false);
                n_socClient.Close();
                n_socClient.Dispose();
                n_socClient = null;
                return false;
            }
        }
        
        public Boolean ConnectPort(string Port, string address, ref string Error)
        {

            try
            {
                //create a new client socket ...
                // m_socClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                String szIPSelected = address;
                String szPort = Port;
                int alPort = System.Convert.ToInt16(szPort, 10);

                System.Net.IPAddress remoteIPAddress = System.Net.IPAddress.Parse(szIPSelected);
                System.Net.IPEndPoint remoteEndPoint = new System.Net.IPEndPoint(remoteIPAddress, alPort);
                n_socClient.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 2000);
                n_socClient.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2000);
                n_socClient.ReceiveTimeout = 2000;
                n_socClient.SendTimeout = 2000;

                n_socClient.NoDelay = true;
                
                IAsyncResult asynResalt = n_socClient.BeginConnect(remoteEndPoint,null, null);
               
                int timeout = 5000;
                
                bool success = asynResalt.AsyncWaitHandle.WaitOne(timeout, true);
                if (success)
                {
                    n_socClient.EndConnect(asynResalt);
                   
                    Error = "";
                    asynResalt.AsyncWaitHandle.Close();
                    ClearPort(500);
                    return true;
                }
                else
                {
                    
                    asynResalt.AsyncWaitHandle.Close();
                    
                    throw new SocketException(10060);//timeout
                    
                }
                



            }
            catch (SocketException se)
            {
                Debug.Print("64");
                Error = se.Message;
                
                n_socClient.Dispose();
                n_socClient = null;
                return false;
            }
        }
        private static void ConnectCallback(IAsyncResult ar)
        { 
         try { 
             // Retrieve the socket from the state object. 
             Socket client = (Socket) ar.AsyncState; 
 
             // Complete the connection. 
             client.EndConnect(ar); 
              
         } 
            catch (Exception e) { 
             Console.WriteLine(e.ToString()); 
         }
            
     } 

        public Boolean Connected = false;
        public bool ClientConnected()
        {
            if (n_socClient.Connected)
                return true;
            else
                return false;
        }
        public void ClosePort()
        {
            StopReceive = false;
            try
            {
                
                ClearPort(500);
                StopReceive = false;
                
               if(n_socClient!=null) { n_socClient.Disconnect(false);n_socClient.Close();n_socClient = null;}
               
            }
            catch {
                
                n_socClient.Close();
                n_socClient = null;
                Debug.Print("65"); };

        }
        public void CloseSocket()
        {
            if (n_socClient != null)
            {
                try
                {
                    
                    StopReceive = false;
                    n_socClient.Close();
                    
                }
                catch (SocketException e)
                { Debug.Print("66");
                dFile.WriteLogFile(e.ToString());
                    MessageBox.Show(e.ToString(), "ERROR", MessageBoxButtons.OK,
                      MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                }
            }


        }
        public Boolean SendData(string data)
        {
            try
            {
                Object objData = data;
                byte[] byData = System.Text.Encoding.ASCII.GetBytes(objData.ToString());
                if (n_socClient == null || !n_socClient.Connected)
                {
                    
                    SetTextLst("--> NOT CONNECTED  " + data +
                " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                    return false;
                }

                n_socClient.Send(byData);
                
                string s = "";
                for (int ii = 0; ii < byData.Length; ii++)
                {
                    int u = (int)byData[ii];
                    s = s + " " + u.ToString();

                }
                
                SetTextLst("<---   " + data +
                " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);

                return true;
            }
            catch (SocketException se)
            {
                
                Debug.Print("67");
                ClientStatus = se.ToString();
                return false;
            }
        }
        public bool SendToUserPort2(string address, int port, string Send, ref string Error, ref CommReply RobotUserReply,bool waitrep=true)
        {
            // init
            TcpClient client = new TcpClient(address, port);
            Byte[] data = Encoding.UTF8.GetBytes(Send);
            NetworkStream stream = client.GetStream();
            try
            {
                // send message
                stream.Write(data, 0, data.Length);
                // get reply
                Byte[] readingData = new Byte[256];
                String responseData = String.Empty;
                StringBuilder completeMessage = new StringBuilder();
                int numberOfBytesRead = 0;
                do
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                    numberOfBytesRead = stream.Read(readingData, 0, readingData.Length);
                    completeMessage.AppendFormat("{0}", Encoding.UTF8.GetString(readingData, 0, numberOfBytesRead));
                }
                while (stream.DataAvailable);
                RobotUserReply.comment = completeMessage.ToString();
                RobotUserReply.result=false;
                return true;
            }
            catch(Exception ex)
            {
                Error=ex.Message;
                return false;
            }
            finally
            {
                stream.Close();
                client.Close();
            }
        }


            
        public bool StopReceive;
               
        #endregion
        
     }  //
}
