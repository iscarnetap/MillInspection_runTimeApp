using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;

namespace SumoNewMachine
{

   public class MotorCnt
    {
        SerialPort sp = new SerialPort();
        bool approvedFlag;
        public string Response;

        public MotorCnt()
        {
            sp.BaudRate = 9600;
            sp.DataBits = 8;
            sp.Parity = Parity.None;
            sp.StopBits = StopBits.One;
            sp.ReadTimeout = 1000;
            //sp.DataReceived += new SerialDataReceivedEventHandler(sp_DataReceived);
        }

        public bool PortScan(string cmdToPort, int ScanTime)
        {
            try
            {
                Response = "";
                approvedFlag = false;

                Stopwatch ScanTimeOut = new Stopwatch();

                if (cleanBuffer() == false) { return false; }
                if (SendCmdToPort(cmdToPort) == false) { return false; }

                //ScanTimeOut.Reset();
                //ScanTimeOut.Start();
                //Thread.Sleep(100);

                Response = "";
                //Response = sp.ReadExisting();
                Response = sp.ReadTo("\r\n");

                MainHMI.NewMainHMI.ListAdd(Response);
                Response = Response.Replace("\r", "");
                Response = Response.Replace("\n", "");
                Response = Response.Replace("?", "");
                Response = Response.Replace(">", "");
                if (Response.Contains(cmdToPort))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            //while (ScanTimeOut.ElapsedMilliseconds < ScanTime)
            //{
            //    //Application.DoEvents();
            //    //if (approvedFlag == true)
            //    //{
            //    //    string last_characters = Response.Substring(Response.Length - 2);
            //    //    if (last_characters == "\n?" || last_characters == "\n>")
            //    //    {
            //    //        ScanTimeOut.Stop();
            //    //        ScanTimeOut.Reset();
            //    //        return true;
            //    //    }
            //    //    else
            //    //    {
            //    //        return false;
            //    //    }
            //    //}
            //    Response = "";
            //    Response=sp.ReadExisting();
            //    Thread.Sleep(100);
            //    //Response=sp.ReadExisting();
            // Response = Response + System.Text.Encoding.ASCII.GetString(response);
            MainHMI.NewMainHMI.ListAdd(Response);
                //if (Response[Response.Length - 1] == Convert.ToChar(">") || Response[Response.Length - 1] == Convert.ToChar("?"))
                //{
                //    ScanTimeOut.Stop();
                //    ScanTimeOut.Reset();
                //    return true;
                //}
            //}
            //ScanTimeOut.Stop();
            //ScanTimeOut.Reset();
            //return false;
        }

        //------------------set_port_name--------------
        public bool SetPortName(string PortName)
        {
            try
            {
                sp.PortName = PortName;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

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
                    //rs232status = "Error opening " + portName + ": " + err.Message;
                    //SetTxtText(rs232status, txtstate, frm);
                    return false;
                }
                //rs232status = portName + " opened successfully";
                //SetTxtText(rs232status, txtstate, frm);
                return true;
            }
            else
            {
                //rs232status = portName + " already opened";
                //SetTxtText(rs232status, txtstate, frm);
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
        //-----------------open port------------------
        public bool OpenMotorport()
        {
            try
            {
                if (!sp.IsOpen)
                {
                    sp.Open();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        //---------------------------CLEAN BUFFER-------------------------------------
        public bool cleanBuffer()
        {
            try
            {
                if (sp.BytesToRead > 0)
                {
                    sp.ReadExisting();
                    return (true);
                }
                else
                {
                    return (true);
                }
            }
            catch (Exception se)
            {
                return (false);
            }
        }

        //----------------------------close port-----------------------------
        public bool CloseMotorPort()
        {
            try
            {
                if (sp.IsOpen)
                {
                    sp.Close();
                }
                return true;
            }

            catch (Exception)
            {
                return false;
            }
        }

        //-----------------------------------send cmd to port---------------------------------------------

        public bool SendCmdToPort(string cmdToPort)
        {
            if (OpenMotorport() == false) { return false; }
            if (cleanBuffer() == false) { return false; }
            try
            {
                sp.Write(cmdToPort+"\r");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        //----------------------------------read data from port--------------------------------

        //public string GetID()
        //{
        //    string string_response = System.Text.Encoding.ASCII.GetString(Response);
        //    return string_response;
        //}


        //--------------------------------------------------------------port read event---------------------------------------
        void sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] response = new byte[sp.BytesToRead];
            sp.Read(response, 0, response.Length);
            //Response=sp.ReadExisting();
            Response = Response + System.Text.Encoding.ASCII.GetString(response);
            MainHMI.NewMainHMI.ListAdd(Response);
            if (Response.Length < 2) return;
            if (Response[Response.Length - 1] == Convert.ToChar(">") || Response[Response.Length - 1] == Convert.ToChar("?"))
            {
                approvedFlag = true;
            }
        }
    }
}
