using System;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading.Tasks;


namespace SumoNewMachine
{
    class Plc
    {
        modbus mb = new modbus();
        //public BackgroundWorker bwPlc = new BackgroundWorker();
        public Stopwatch stopwatch = new Stopwatch();
        private DataIniFile dFile = new DataIniFile();
        public int PlcAdd = 0;
        public struct CommReply
        {
            public bool result;
            public int[] data;
            public string status;
            public string comment;//====2013
            public int FunctionCode;
            public string Error;
        }
        public Plc()
        {
            //bwPlc.WorkerSupportsCancellation = true;
            //bwPlc.WorkerReportsProgress = true;
            //bwPlc.DoWork += new DoWorkEventHandler(bwPlc_DoWork);
            //bwPlc.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwPlc_RunWorkerCompleted);
        }
        public void SetControls(ListBox LstSend, Label lblstate, Form Frm, Boolean en)
        {
            mb.SetControls(LstSend, lblstate, Frm, en);
        }
        public Boolean PlcOpenPort(string PortName, int Baudrate, int DataBits, String Parity, string StopBits)
        {
            if (mb.sp.IsOpen)
            {
                return true;
            }
            else
            {
                if (mb.Open(PortName, Baudrate, DataBits, Parity, StopBits))
                {
                    return true;
                }
                else
                {
                    return false;
                }

            }
        }
        public  void PlcClosePort()
        {
            mb.Close();
        }
        public async  Task<bool> WaitReady(int Timeout, int AutoCycle, int bitReady, int reg_off = 4096)
        {
            MyStatic.ReadingIO = false;
            MyStatic.WaitReady = true;
            MyStatic.SetOut = -1;
            String err = "";


            int[] values = new int[4];
            int[] data = new int[7];

            stopwatch.Reset();
            stopwatch.Start();

            data[0] = 2;//wait ready
            data[1] = 0 + reg_off;//start address
            data[2] = (MyStatic.Doffset);//address offset
            data[3] = 6;//reg number to read
            data[4] = Timeout;//
            data[5] = AutoCycle;//1 autocycle with robot continue cycle, 0 manual change
            data[6] = bitReady;
            CommReply reply = new CommReply();
            CommReply send = new CommReply();
            send.data = data;
            string ErrMessage = "";
            //var ctss = new CancellationTokenSource();

            //ctss.CancelAfter(Timeout);
            //await Task.Run(() => RunWorkerAsync(send, ref ErrMessage, ref reply), ctss.Token);
            await Task.Run(() => RunWorkerAsync(send, ref ErrMessage, ref reply));

            return reply.result;

        }
        
        public int ReadBit(Int16 RegAddress, int RegOffset, int bit, int plcadd = 1)
        {
            //on error return -1;
            plcadd = PlcAdd;
            ushort[] value = new ushort[1];
            if (ReadFunction(RegAddress, RegOffset, 1, ref value))
            {
                int ii = (Int32)Math.Pow(2, bit);
                if ((value[0] & ii) > 0)
                { return 1; }
                else
                { return 0; }
            }
            else
                return -1;
        }
        #region Write Function
        public Boolean WriteFunction(int WriteRegister, int Regs, Int16 WriteValue, int reg_off = 4096, int plcadd = 1)
        {


            try
            {
                plcadd = PlcAdd;
                byte IdAddress = Convert.ToByte(plcadd);
                int start = WriteRegister + (MyStatic.Doffset) + reg_off;
                short[] value = new short[1];
                value[0] = WriteValue;

                if (mb.SendFc16(IdAddress, start, (ushort)Regs, value))
                { return true; }
                else
                { return false; }

            }
            catch (Exception err)
            {
                dFile.WriteLogFile("ERROR WRITE FUNCTION:" + err.Message);
                return false;
            }

        }

        private void WriteSingleFunction(Int16 RegAdd, Int16 WriteValue)
        {

            try
            {

                byte PlcAddress = Convert.ToByte(PlcAdd);
                int start = RegAdd + (MyStatic.Doffset);
                short value = 0;
                value = WriteValue;
                mb.SendFc6(PlcAddress, start, value);

            }
            catch (Exception err)
            {
                //DoGUIStatus("Error in write function: " + err.Message);
                dFile.WriteLogFile("ERROR WRITE SINGLE FUNCTION:" + err.Message);
            }



        }
        public bool WriteCoilFunction(Int16 RegAddress, int SetBit, int m_off = 2048, int plcadd = 1)
        {

            try
            {
                byte PlcAddress = Convert.ToByte(PlcAdd);
                //int start = (RegAddress * (0xF + 1) + RegBit);
                int start = RegAddress + m_off;
                int value = 0;
                if (SetBit == 0)
                {
                    value = Convert.ToInt16(0);
                }
                else
                {
                    value = Convert.ToInt32(0xFF00);//set Mbit to 1
                }
                mb.SendFc5(PlcAddress, start, value);
                return true;
            }
            catch (Exception err)
            {
                //DoGUIStatus("Error in write function: " + err.Message);
                dFile.WriteLogFile("ERROR WRITE COIL FUNCTION:" + err.Message);
                return false;

            }


        }
        #endregion
        #region Read Function
        public Boolean ReadFunction(Int16 StartAddress, int offset, Int16 RegNum, ref ushort[] value, int reg_off = 4096, int plcadd = 1)
        {

            try
            {
                byte PlcAddress = Convert.ToByte(PlcAdd);
                int start = Convert.ToInt16(StartAddress) + reg_off;// offset 4096 for delta PLC Dreg

                value = new ushort[RegNum];

                return (mb.SendFc3(PlcAddress, start, offset, (ushort)RegNum, ref value));


            }
            catch (Exception err)
            {
                dFile.WriteLogFile("ERROR READ FUNCTION:" + err.Message);
                return false;
            }

        }
        public Boolean ReadData(Int16 StartAddress, int offset, Int16 RegNum, ref ushort[] value, int reg_off = 4096, int plcadd = 1)
        {

            try
            {
                byte PlcAddress = Convert.ToByte(PlcAdd);
                int start = Convert.ToInt16(StartAddress) + reg_off;// offset 4096 for delta PLC Dreg

                value = new ushort[RegNum];

                return (mb.SendFc3(PlcAddress, start, offset, (ushort)RegNum, ref value));


            }
            catch (Exception err)
            {
                dFile.WriteLogFile("ERROR READ FUNCTION:" + err.Message);
                return false;
            }

        }
        #endregion
        //#region BackgroundWorker

        public void RunWorkerAsync(CommReply send, ref string ErrMessage, ref CommReply reply)
        {

            try
            {
                byte address = (byte)send.data[0];
                int start= send.data[1];
                int offset = send.data[2];
                ushort register= (ushort)send.data[3];
                if (!mb.ReadPlcData( address,  start,  offset, register,  ref reply))
                {
                    MyStatic.SetOut = -1;
                    MyStatic.ReadingIO = false;
                    //string err ="ERROR READ PLC DATA";
                }

                //fini


            }
            catch (OperationCanceledException)
            {

                return;
            }
        }



        //#endregion
        public async Task<bool> PC_command(int auto, short plc_command, short plc_fini, int timeout,int debugtime=1000)
            {

                if (MyStatic.chkDebug)
                {

                Thread.Sleep(debugtime);
                return true;
                    bool ready = await WaitReady(timeout, auto, plc_fini);
                    if (!ready)//int Timeout, int AutoCycle, int bitReady, int reg_off = 4096
                    {
                        MyStatic.ReadingIO = false;
                        MyStatic.WaitReady = false;
                        return false;//timeout 30 sec,auto=0
                    }
                    return true;
                }

        
                if (MyStatic.ReadingIO || MyStatic.WaitReady)
                {
                    MyStatic.ReadingIO = false;
                    MyStatic.WaitReady = false;
                    Thread.Sleep(200);
                }
                //auto=1 automode


                //set Mbit change tray
                WriteCoilFunction(plc_command, 1);
                Thread.Sleep(200);
                if (plc_fini != MyStatic.PlcEndFanucStart)
                {
                    if (ReadBit(MyStatic.Dstatus, (MyStatic.Doffset), plc_fini) != 0)//check not reday
                    {
                    // Debug.WriteLine((PLC.ReadBit(MyStatic.Dstatus, (MyStatic.Doffset), plc_fini).ToString()));
                    MyStatic.ReadingIO = false;
                    MyStatic.WaitReady = false;
                    return false;
                }
                }
                //timeout
                stopwatch.Reset();
                stopwatch.Start();
                bool ready1 = await WaitReady(timeout, auto, plc_fini);
                if (!ready1)
                {
                    MyStatic.ReadingIO = false;
                    MyStatic.WaitReady = false;
                    return false;//timeout 30 sec,auto=0
                }

                return true;
        }
        public async Task<Plc.CommReply> ReadReg(int start, int offset, ushort register)
        {
            Plc.CommReply send = new Plc.CommReply();
            Plc.CommReply repl = new Plc.CommReply();
            Stopwatch stopw = new Stopwatch();
            stopw.Restart();
            while (MyStatic.ReadPlcBusy)
            {
                if (MyStatic.bReset || stopw.ElapsedMilliseconds > 200)
                {
                    repl.Error = "ERROR READ PLC DATA. PLC busy";
                    repl.result = false;
                    return repl;
                }
                await Task.Delay(50);

            }
            byte address = Convert.ToByte(PlcAdd);
            
            
            if (!mb.ReadPlcData(address, start, offset, register, ref repl))
            {
                MyStatic.SetOut = -1;
                MyStatic.ReadingIO = false;
                repl.Error = "ERROR READ PLC DATA";
                repl.result = false;
                return repl;
            }
            repl.result = true;
            return repl;
        }
        public async Task<Plc.CommReply> WaitBusy(bool busy, int timeout = 0)
        {
                Stopwatch stopw1 = new Stopwatch();
                stopw1.Restart();
                Plc.CommReply reply = new Plc.CommReply();
            try
            {
                
                while (MyStatic.ReadPlcBusy)
                {
                    Application.DoEvents();
                    if (MyStatic.bReset || stopw1.ElapsedMilliseconds > timeout)
                    {
                        reply.Error = "ERROR  busy";
                        reply.result = false;
                        return reply;
                    }
                    await Task.Delay(50);

                }
                reply.Error = "";
                reply.result = true;
                return reply;
            }
            catch (Exception ex)
            {
                reply.Error = "ERROR  busy";
                reply.result = false;
                return reply;
            }
        }
        public async Task<Plc.CommReply> PlcCommand(int m_cmd,int m_fini,int m_error,int timeout=0)
        {
            Plc.CommReply reply = new Plc.CommReply();
            int ErrCnt = 0;
            try
            {
               
                if (!WriteCoilFunction((short)m_cmd, 1))
                {
                    reply.result = false;
                    reply.Error = "Error write bit m_cmd=" + m_cmd.ToString();
                    return reply;
                }
                //wait fini
                //D5 - fini D6 - error


                //ErrorLift = " ";
                Stopwatch stopw = new Stopwatch();
                stopw.Restart();
                while (true)
                {
                    await Task.Delay(20);
                    var task = WaitBusy(MyStatic.ReadPlcBusy,200);
                    await task;
                    reply = task.Result;
                    if(!reply.result)
                    {
                        reply.result = false;
                        reply.Error = "Error Busy m_cmd=" + m_cmd.ToString();
                        return reply;
                    }
                    //ReadRegNum(5, PLC2add, 2, Darr) 'read D5,D6
                    if (timeout >= 0 && stopw.ElapsedMilliseconds > timeout)
                    {
                        reply.result = false;
                        reply.Error = "Error timeout m_cmd=" + m_cmd.ToString();
                        return reply;
                    }
                    if (MyStatic.bReset)
                    {
                        reply.result = false;
                        return reply;
                    }

                    int start = 5 + 4096;//start address with delta PLC D0 offset
                    int offset = 0;
                    ushort registers = 2;
                    var task1 = ReadReg(start, offset, registers);
                    await task1;
                    reply = task1.Result;
                    if (!reply.result)
                    {
                        ErrCnt++;
                        if (ErrCnt > 2) return reply;
                    }
                    else
                    {
                        int[] Darr = { 0, 0 };
                        Darr[0] = (int)reply.data[0];
                        Darr[1] = (int)reply.data[1];
                        //'M_ResetConvFin
                        int bit = (Int32)Math.Pow(2, m_fini);
                        if ((Darr[0] & bit) == bit)
                        {
                            reply.result = true;
                            return reply;
                        }
                        //   'M_ResetConvErr
                        int bit1 = (Int32)Math.Pow(2, m_error);
                        if ((Darr[1] & bit) == bit)
                        {
                            reply.result = false;
                            reply.Error = "Error task m_cmd=" + m_cmd.ToString() + "Error:" + m_error.ToString();
                            return reply;
                        }
                    }


                }
            }
            catch (Exception ex)
            {
                reply.result = false;
                reply.Error = "Error task m_cmd=" + ex.Message;
                return reply;
            }
           
           
        }
    }
}
