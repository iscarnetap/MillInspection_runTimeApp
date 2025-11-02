using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace SumoNewMachine
{
    public class MainCycle
    {
        Stopwatch PickVisionTimeOut = new Stopwatch();
        Stopwatch ChangeConveyor1TimeOut = new Stopwatch();
        Stopwatch ChangeConveyor2TimeOut = new Stopwatch();

        public bool TMP = false;
        public BackgroundWorker bwMainCycle = new BackgroundWorker();

        private long ReadConveyorStatusTimeOut = 15000;
        private long ChangeConveyorTimeOut = 120000;


        public struct MainCycleParameters
        {
            public float[] data;
            public string comment;//====2013
            public int status;
            public int CameraNum;
        }
        #region ---------event to Form--------------
        public event EventHandler<MyEventArgs> GetFiniDone;//= delegate { };
        public delegate void EventHandler(object sender, MyEventArgs e);

        public class MyEventArgs : System.EventArgs
        {
            private Single[] _result;
            public Single[] DataGet
            {
                set { _result = value; }
                get { return _result; }
            }
        }


        public MyEventArgs MyE = new MyEventArgs();

        public void GetFini(Single[] result)
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

            MyE.DataGet = result;
            this.GetFiniDone(this, MyE);
        }
        #endregion


        public MainCycle()
        {
            bwMainCycle.WorkerSupportsCancellation = true;
            bwMainCycle.DoWork += new DoWorkEventHandler(bwMainCycle_DoWork);
            bwMainCycle.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwMainCycle_RunWorkerCompleted);
            //ToshibaFunc.RobotName = "Toshiba_MainCycle";
            //ToshibaFunc.bwName = "Toshiba_MainCycle";

            // Array.Resize<Single>(ref Parms0.SendParm, 11);
            // for (int i = 0; i < 11; i++) Parms0.SendParm[i] = 0;//zero array
            //ToshibaFunc.GetFiniDone += ToshibaFunc_FiniDone;

            //  CameraPick = new Cameras();
        }

        public Boolean StartCycle(ref string Error)//====2013
        {
            DateTime NowTime = DateTime.Now;
            DateTime startTime = DateTime.Now;
            Stopwatch stopw = new Stopwatch();
            stopw.Start();
            while (bwMainCycle.IsBusy) //wait end busy for 2 sec
            {
                if (300 < stopw.ElapsedMilliseconds) break;
                Thread.Sleep(2);
            }

            if (!bwMainCycle.IsBusy)
            {
                bwMainCycle.RunWorkerAsync();//====2013
            }
            else
            {
                bwMainCycle.CancelAsync();
                MyStatic.bReset = true;
                Thread.Sleep(1000);
                MyStatic.bReset = false;
                Error = "ERROR: Connection Busy" + "Can't run the worker twice! Action:";
                //SetTextLst("!!!   " + Error +
                // " // (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", lstSend, frm);
                //AddToList(Error);
                    //inv.set(MainHMI.dataGridViewRobot1, Enabled, true); 
                //MainHMI.NewMainHMI.SetPanelsEnable(true);
                return false;
            }
            //GC.Collect();//$$$
            return true;
        }

        public void bwMainCycle_DoWork(object sender, DoWorkEventArgs e)
        {
            string Error="";
            Thread.CurrentThread.Name = "MainCycle";
            //  MyStatic.bReset = false;
            while (MyStatic.bStartCycle)
            {
                Thread.Sleep(1);
                inv.settxt(MainHMI.NewMainHMI.lblCycleStatus,"Running.....");
                if (MyStatic.MainCmdFini[(int)MyStatic.Actions.Error]!=0)
                {
                    MyStatic.MainCmdFini[(int)MyStatic.Actions.Error] = 0;
                    MyStatic.bStartCycle = false;
                    break;
                }
                if (MyStatic.CycleStep[(int)MyStatic.Actions.CamWaterMarkVision] == 1)
                {
                    //frmVision.mFormVisionDefInstance.Cam5.Busy = false;
                    if (!frmVision.mFormVisionDefInstance.Cam1.Busy && !frmVision.mFormVisionDefInstance.Cam2.Busy &&
                        !frmVision.mFormVisionDefInstance.Cam3.Busy && !frmVision.mFormVisionDefInstance.Cam4.Busy &&
                        !frmVision.mFormVisionDefInstance.Cam5.Busy && !frmVision.mFormVisionDefInstance.Cam6.Busy)
                    {
                        MainCycleParameters GetParams = new MainCycleParameters();
                        float[] X = new float[4];
                        GetParams.data = X;
                        GetParams.data[0] = 6;
                        GetParams.status = 1; //timeout
                        GetParams.CameraNum = 6;
                        GetParams.comment = "Water Mark Camera";
                        e.Result = GetParams;
                        return;
                    }
                }
                if (MyStatic.CycleStep[(int)MyStatic.Actions.CellVision] == 1)
                {
                    //frmVision.mFormVisionDefInstance.Cam5.Busy = false;
                    if (!frmVision.mFormVisionDefInstance.Cam1.Busy && !frmVision.mFormVisionDefInstance.Cam2.Busy &&
                        !frmVision.mFormVisionDefInstance.Cam3.Busy && !frmVision.mFormVisionDefInstance.Cam4.Busy &&
                        !frmVision.mFormVisionDefInstance.Cam5.Busy && !frmVision.mFormVisionDefInstance.Cam6.Busy)
                    {
                        MyStatic.CycleStep[(int)MyStatic.Actions.CellVision] = 0;
                        MainCycleParameters GetParams = new MainCycleParameters();
                        float[] X = new float[4];
                        GetParams.data = X;
                        GetParams.data[0] = 5;
                        GetParams.status = 1; //timeout
                        GetParams.CameraNum = 5;
                        GetParams.comment = "Cell Vision";
                        e.Result = GetParams;
                        return;
                    }
                }
                if (MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] == 1)
                {
                    //frmVision.mFormVisionDefInstance.Cam5.Busy = false;
                    if (!frmVision.mFormVisionDefInstance.Cam1.Busy && !frmVision.mFormVisionDefInstance.Cam2.Busy &&
                        !frmVision.mFormVisionDefInstance.Cam3.Busy && !frmVision.mFormVisionDefInstance.Cam4.Busy &&
                        !frmVision.mFormVisionDefInstance.Cam5.Busy && !frmVision.mFormVisionDefInstance.Cam6.Busy)
                    {
                        if (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] != 0)
                        {
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 0;
                        }
                        else
                        {
                            PickVisionTimeOut.Reset();
                            PickVisionTimeOut.Start();

                            MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 2;
                            MainCycleParameters GetParams = new MainCycleParameters();
                            float[] X = new float[4];
                            GetParams.data = X;
                            if (MainHMI.NewMainHMI.WorkMode.PlasticToPlastic)
                            {
                                GetParams.data[0] = 2;
                            }
                            else if (MainHMI.NewMainHMI.WorkMode.GraffitToPlastic)
                            {
                                GetParams.data[0] = 1;
                            }
                            GetParams.status = 1; //timeout
                            GetParams.CameraNum = 1;
                            GetParams.comment = "Camera Pick";
                            e.Result = GetParams;
                            return;
                        }
                    }
                }
                if (MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] == 2)
                {
                    if(PickVisionTimeOut.ElapsedMilliseconds > 2000000)
                    {
                        MainHMI.NewMainHMI.ResetAllFlags();
                        MyStatic.bStartCycle = false;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 0;
                        MessageBox.Show("Vision Time Out", "ERROR", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                        return;
                    }
                    if (MyStatic.bStopCycle)
                    {
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 0;
                        if (MainHMI.NewMainHMI.WorkMode.PlasticToPlastic)
                        {
                            bool tmp = false;

                            for (int i = 2; i < MyStatic.CycleStep.Length - 1; i++)
                            {
                                if (MyStatic.CycleStep[i] > 0)
                                {
                                    tmp = true;
                                    break;
                                }
                            }
                            if (!tmp)
                            {
                                MainHMI.NewMainHMI.ResetAllFlags(); MyStatic.bStartCycle = false;
                                string ErrMessage1 = "";
                                if (!MainHMI.NewMainHMI.RunCmd12(ref ErrMessage1, MyStatic.Speed))
                                {
                                    MyStatic.bStartCycle = false;
                                    MessageBox.Show(ErrMessage1, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                                    return;
                                }
                                return;
                            }
                            else
                            {
                                MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                                MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 0;
                                MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] = (int)MyStatic.CmdFini.CamPickVision;
                            }
                            //MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] = (int)MyStatic.CmdFini.CamPickVision;
                            //if(MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace]==0)
                            //{
                            //    MyStatic.bStartCycle = false; MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 0;
                            //}
                            //else
                            //{
                            //    MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 0;
                            //}
                        }
                        else if(MainHMI.NewMainHMI.radioButtonFlipStation.Checked)
                        {
                            bool tmp = false;
                            for (int i = 2; i < MyStatic.CycleStep.Length - 1; i++)
                            {
                                if (MyStatic.CycleStep[i] > 0)
                                {
                                    tmp = true;
                                    break;
                                }
                            }
                            if (!tmp)
                            {
                                MainHMI.NewMainHMI.ResetAllFlags(); MyStatic.bStartCycle = false;
                                string ErrMessage1 = "";
                                if (!MainHMI.NewMainHMI.RunCmd12(ref ErrMessage1, MyStatic.Speed))
                                {
                                    MyStatic.bStartCycle = false;
                                    MessageBox.Show(ErrMessage1, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                                    return;
                                }
                                return;
                            }
                            else
                            {
                                MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                                MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 0;
                                MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] = (int)MyStatic.CmdFini.CamPickVision;
                            }
                        }
                        else if(MainHMI.NewMainHMI.radioButtonWaterMark.Checked)
                        {
                            bool tmp = false;
                            for(int i=2;i< MyStatic.CycleStep.Length-1;i++)
                            {
                                if(MyStatic.CycleStep[i] > 0)
                                {
                                    tmp = true;
                                    break;
                                }
                            }
                            if(!tmp)
                            {
                                MainHMI.NewMainHMI.ResetAllFlags(); MyStatic.bStartCycle = false;
                                string ErrMessage1 = "";
                                if (!MainHMI.NewMainHMI.RunCmd12(ref ErrMessage1, MyStatic.Speed))
                                {
                                    MyStatic.bStartCycle = false;
                                    MessageBox.Show(ErrMessage1, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                                    return;
                                }
                                return;
                            }
                           else
                            {
                                MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                                MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 0;
                                MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] = (int)MyStatic.CmdFini.CamPickVision;
                            }
                        }
                        else
                        {
                            MyStatic.bStartCycle = false; MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 0;
                            MainHMI.NewMainHMI.ResetAllFlags();
                            return;
                        }
                    }
                    //Was Next
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] == (int)MyStatic.CmdErr.CamPickVision)
                    {
                    Thread.Sleep(2);
                        MyStatic.CycleTime.Reset();
                        MyStatic.CycleTime.Start();
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                    }
                    // bla bla
                }

                if (MainHMI.NewMainHMI.radioButtonFlipStation.Checked)
                {
                    MainLoopWithFlipStation();
                }
                else if (MainHMI.NewMainHMI.radioButtonWaterMark.Checked && MainHMI.NewMainHMI.WorkMode.PlasticToPlastic)
                {
                    MainLoopWaterMark();
                }
                else if (MainHMI.NewMainHMI.radioButtonWaterMarkInFlip.Checked && MainHMI.NewMainHMI.WorkMode.PlasticToPlastic)
                {
                    MainLoopWaterMarkInFlipStation();
                }
                else
                {
                    MainLoopPickPlace();
                }
            }
        }

        public void SetError(string ErrorText,string ErrorCaption)
        {
                //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.YellowLightChannel, MyStatic.DigitalOutput.YellowLightBit, 0);
                //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.GreenLightChannel, MyStatic.DigitalOutput.GreenLightBit, 0);
                //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.RedLightChannel, MyStatic.DigitalOutput.RedLightBit, 1);
            //int nValue;
            string s = "";
            bool bDeltaErr = false;
            if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.YellowLightBit, 0, ref s)) {
                bDeltaErr = true;
                MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } else {
                if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.GreenLightBit, 0, ref s)) {
                    bDeltaErr = true;
                    MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else {
                    if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.RedLightBit, 1, ref s)) {
                        bDeltaErr = true;
                        MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            MyStatic.bStartCycle = false;
            MainHMI.NewMainHMI.ResetAllFlags();

            if (bDeltaErr) MainHMI.NewMainHMI.AddToLogFile(s + "   " + "DELTA Error");
            MainHMI.NewMainHMI.AddToLogFile(ErrorText + "   " + ErrorCaption);

            MessageBox.Show(ErrorText, ErrorCaption, MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            
                //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.RedLightChannel, MyStatic.DigitalOutput.RedLightBit, 0);
            if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.RedLightBit, 0, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            inv.click(MainHMI.NewMainHMI.btnStop);
            inv.click(MainHMI.NewMainHMI.btnStopBuzzer);
            inv.click(MainHMI.NewMainHMI.btnRobotHome);
        }


        public void SetDeltaError(string ErrorText)
        {
            string s = "";
            MyStatic.bStartCycle = false;
            MainHMI.NewMainHMI.ResetAllFlags();
            MainHMI.NewMainHMI.AddToLogFile(ErrorText + "   " + "DELTA Error");
            MessageBox.Show(ErrorText, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            inv.click(MainHMI.NewMainHMI.btnStop);
            inv.click(MainHMI.NewMainHMI.btnRobotHome);
        }


        public void MainLoopWaterMarkInFlipStation()
        {
            #region Robot Pick
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick])
            {
                case 1:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] == (int)MyStatic.CmdFini.CamPickVision)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] = 0;
                        string ErrMessage = "";
                        if (!MainHMI.NewMainHMI.RunCmd10(ref ErrMessage, MyStatic.Speed))
                        {
                            SetError(ErrMessage, "RobotError");
                            return;
                        }
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 2;
                    }
                    break;
                case 2:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] == (int)MyStatic.CmdFini.RobotPick)
                    {

                        MainHMI.NewMainHMI.PartIndexPlasticPick = MainHMI.NewMainHMI.PartIndexNew;

                        if (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] != 0)
                        {
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = 0;
                            return;
                        }
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToFlipStation] = 1;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] == (int)MyStatic.CmdErr.RobotPick)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
            }
            #endregion
            #region Robot Place to Flip Station
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToFlipStation])
            {
                case 1:
                    string ErrMessage = "";
                    if (!MainHMI.NewMainHMI.RunCmd45(ref ErrMessage, 180, 90, MyStatic.Speed)) //0,270
                    {
                        SetError(ErrMessage, "RobotError");
                        return;
                    }
                    MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToFlipStation] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToFlipStation] == (int)MyStatic.CmdFini.RobotPlaceToFlipStation)
                    {
                        /*
                        string ErrMessage16 = "";
                        if (!MainHMI.NewMainHMI.RunCmd16(ref ErrMessage16, true, 270)) //send Parameters To Robot 
                        {
                            MessageBox.Show(ErrMessage16);
                            return;
                        }
                        */
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToFlipStation] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToFlipStation] = 0;
                        // MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation] = 1;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamWaterMarkVision] = 1;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToFlipStation] == (int)MyStatic.CmdErr.RobotPlaceToFlipStation)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion
            #region Motor Rotate To Position
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.MotorRotate])
            {
                case 1:
                    string ErrMessage1 = "";
                    if (!MainHMI.NewMainHMI.RunCmd16(ref ErrMessage1, true,MainHMI.NewMainHMI.FlipStationValue))
                    {
                        SetError(ErrMessage1, "RobotError");
                        MyStatic.CycleStep[(int)MyStatic.Actions.MotorRotate] = 0;
                        return;
                    }
                    MyStatic.CycleStep[(int)MyStatic.Actions.MotorRotate] = 2;
                    break;
                case 2://rotate fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.MotorRotate] == (int)MyStatic.CmdFini.MotorRotate)
                    {
                        if (MainHMI.NewMainHMI.FlipStationValue == 0 || MainHMI.NewMainHMI.FlipStationValue == 180)
                        {
                            MyStatic.CycleStep[(int)MyStatic.Actions.MotorRotate] = 0;
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.MotorRotate] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation] = 1;
                            //MainHMI.NewMainHMI.FlipStationValue = 270; // -90;
                            //if (!MainHMI.NewMainHMI.SetAngleToMdrive()) { SetError("Motor Not Get The Position", "Motor Error"); return; }
                        }
                        else
                        {
                            MyStatic.CycleStep[(int)MyStatic.Actions.MotorRotate] = 0;
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.MotorRotate] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.CamWaterMarkVision] = 1;
                        }
                    }
                    break;
                default:
                    break;
            }
            #endregion
            #region Robot Pick From Flip Station
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation])
            {
                case 1:
                    string ErrMessage = "";
                    bool back;
                    if (MainHMI.NewMainHMI.FlipStationValue == 0)
                        back = true;
                    else
                        back = false;
                    if (!MainHMI.NewMainHMI.RunCmd55(ref ErrMessage, 180, back, MyStatic.Speed)) //180
                    {
                        SetError(ErrMessage, "RobotError");
                        return;
                    }
                    MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPickFromFlipStation] == (int)MyStatic.CmdFini.RobotPickFromFlipStation)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPickFromFlipStation] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation] = 0;
                        if (MainHMI.NewMainHMI.WaterMarkInFlipStationResult)
                        {
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 1;
                            MainHMI.NewMainHMI.WaterMarkInFlipStationResult = false;
                        }
                        else
                        {
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToReject] = 1;
                            MainHMI.NewMainHMI.WaterMarkInFlipStationResult = false;
                        }
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPickFromFlipStation] == (int)MyStatic.CmdErr.RobotPickFromFlipStation)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion
            #region Robot Place 
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace])
            {
                case 1:
                    int indtemp = MainHMI.NewMainHMI.PartIndex;
                    if (MainHMI.NewMainHMI.PartIndexPlasticPick >= 0)
                        MainHMI.NewMainHMI.PartIndex = MainHMI.NewMainHMI.PartIndexPlasticPick;

                    string ErrMessage = "";
                    if (!MainHMI.NewMainHMI.RunCmd11(ref ErrMessage, MyStatic.Speed))
                    {
                        MainHMI.NewMainHMI.PartIndex = indtemp;
                        SetError(ErrMessage, "RobotError");
                        return;
                    }
                    MainHMI.NewMainHMI.PartIndex = indtemp;
                    MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] == (int)MyStatic.CmdFini.RobotPlace)
                    {
                        if (MyStatic.bStopCycle)
                        {
                            string ErrMessage1 = "";
                            if (!MainHMI.NewMainHMI.RunCmd12(ref ErrMessage1, MyStatic.Speed))
                            {
                                SetError(ErrMessage1, "RobotError");
                                return;
                            }
                            MyStatic.bStartCycle = false;
                            return;
                        }
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                        MyStatic.InsertCount++;
                        if (MyStatic.InsertCount > 99999) MyStatic.InsertCount = 0;
                        inv.settxt(MainHMI.NewMainHMI.lblInsertsCount, MyStatic.InsertCount.ToString());
                        //inv.settxt(MainHMI.NewMainHMI.lblCycleTime, MyStatic.CycleTime.ElapsedMilliseconds.ToString());
                        inv.settxt(MainHMI.NewMainHMI.lblCycleTime, (MyStatic.CycleTime.ElapsedMilliseconds / 1000f).ToString("0.00"));
                        MyStatic.CycleTime.Restart();
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] == (int)MyStatic.CmdErr.RobotPlace)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion
            #region Robot Place To Reject 
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToReject])
            {
                case 1:

                    if (MainHMI.NewMainHMI.WorkMode.PlasticToPlastic)
                    {
                        if ((MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] == (int)MyStatic.CmdFini.CamPickVision) || MyStatic.bStopCycle)
                        {
                            string ErrMessage = "";
                            if (!MainHMI.NewMainHMI.RunCmd65(ref ErrMessage, MyStatic.Speed))
                            {
                                SetError(ErrMessage, "RobotError");
                                return;
                            }
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToReject] = 2;
                            break;
                        }
                    }
                    else
                    {
                        string ErrMessage = "";
                        if (!MainHMI.NewMainHMI.RunCmd11(ref ErrMessage, MyStatic.Speed))
                        {
                            SetError(ErrMessage, "RobotError");
                            return;
                        }
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToReject] = 2;
                        break;
                    }
                    //MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] = (int)MyStatic.CmdFini.RobotPlace;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToReject] == (int)MyStatic.CmdFini.RobotPlaceToReject)
                    {
                        if (MyStatic.bStopCycle)
                        {
                            string ErrMessage1 = "";
                            if (!MainHMI.NewMainHMI.RunCmd12(ref ErrMessage1, MyStatic.Speed))
                            {
                                SetError(ErrMessage1, "RobotError");
                                return;
                            }
                            MyStatic.bStartCycle = false; return;
                        }
                        if (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] != 0)
                        {
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToReject] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToReject] = 0;
                            return;
                        }
                        else
                        {
                            MyStatic.InsertCount++;
                            if (MyStatic.InsertCount > 99999) MyStatic.InsertCount = 0;
                            inv.settxt(MainHMI.NewMainHMI.lblInsertsCount, MyStatic.InsertCount.ToString());
                            inv.settxt(MainHMI.NewMainHMI.lblCycleTime, (MyStatic.CycleTime.ElapsedMilliseconds / 1000f).ToString("0.00"));
                            MyStatic.CycleTime.Restart();
                            MyStatic.RejectedInsetCount++;
                            inv.settxt(MainHMI.NewMainHMI.lblRejectInsertsCount, MyStatic.RejectedInsetCount.ToString());
                            if (MyStatic.RejectedInsetCount > MainHMI.NewMainHMI.RejectTray.Length)
                            {
                                SetError("Please Empty Rejected Tray", "Rejected Tray Is Full");
                                return;
                            }
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToReject] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToReject] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                            break;
                        }
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] == (int)MyStatic.CmdErr.RobotPlace)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion

            int nValue;
            byte InputState;
            string s = "";

            #region Change Conveyor 1
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1])
            {
                case 1:
                    //Output To plc To Change Conveyor 1
                    //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray1Channel, MyStatic.DigitalOutput.ChangeTray1Bit, 1);
                    nValue = 1;
                    if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray1Bit, nValue, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Thread.Sleep(1000);
                    //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray1Channel, MyStatic.DigitalOutput.ChangeTray1Bit, 0);
                    nValue = 0;
                    if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray1Bit, nValue, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ChangeConveyor1TimeOut.Reset();
                    ChangeConveyor1TimeOut.Start();
                    MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (ChangeConveyor1TimeOut.ElapsedMilliseconds > ChangeConveyorTimeOut)
                    {
                        ChangeConveyor1TimeOut.Reset();
                        SetError("Error To Change Conveyor 1", "Conveyor 1 Error By Time Out");
                        return;
                    }
                    //check Conveyor Error
                    InputState = 1;
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor1ErrorChannel, MyStatic.DigitalInputs.Conveyor1ErrorBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor1ErrorBit, ref InputState, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (InputState == 1) //0
                    {
                        ChangeConveyor1TimeOut.Reset();
                        SetError("Error To Change Conveyor 1", "Conveyor 1 Error");
                        return;
                    }
                    //------------------------------------------------------------------
                    //change conveyor ready
                    InputState = 0; //1
                    //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor1ReadyChannel, MyStatic.DigitalInputs.Conveyor1ReadyBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor1ReadyBit, ref InputState, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (InputState == 1)
                    {
                        ChangeConveyor1TimeOut.Reset();
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                    }
                    break;
                default:
                    break;
                    //hfawhefoaweioaw
            }
            #endregion
            #region Change Conveyor 2
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2])
            {
                case 1:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotHome] == (int)MyStatic.CmdFini.RobotHome)
                    {

                        //if (MainHMI.NewMainHMI.WorkMode.PlasticToPlastic)
                        //    inv.click(MainHMI.NewMainHMI.btnSavePlasticTrayStatus);
                        
                        int sssss = MainHMI.NewMainHMI.PartIndex;
                        MainHMI.NewMainHMI.PartIndexNew = 0;

                        bool bDeltaErr = false;
                            //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray2Channel, MyStatic.DigitalOutput.ChangeTray2Bit, 1);
                        if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray2Bit, 1, ref s))
                        {
                            SetDeltaError(s);
                            //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            bDeltaErr = true;
                        }
                        else
                        {
                            Thread.Sleep(1000);
                                //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray2Channel, MyStatic.DigitalOutput.ChangeTray2Bit, 0);
                            if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray2Bit, 0, ref s))
                            {
                                SetDeltaError(s);
                                //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                bDeltaErr = true;
                            }
                        }

                        ChangeConveyor2TimeOut.Reset();

                        if (bDeltaErr) return;

                        ChangeConveyor2TimeOut.Start();
                        //output to change conveyor 2
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] = 2;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotHome] == (int)MyStatic.CmdErr.RobotHome)
                    {
                    }
                    break;
                case 2://toshiba R1 place fini
                    if (ChangeConveyor2TimeOut.ElapsedMilliseconds > ChangeConveyorTimeOut)
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetError("Error To Change Conveyor 2", "Conveyor 2 Error By Time Out");
                        return;
                    }
                    //check Conveyor Error
                    InputState = 1;
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor2ErrorChannel, MyStatic.DigitalInputs.Conveyor2ErrorBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor2ErrorBit, ref InputState, ref s))
                    {
                        ChangeConveyor2TimeOut.Reset();
                        // MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        SetDeltaError(s);
                        return;
                    }
                    if (InputState == 1) //0
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetError("Error To Change Conveyor 2", "Conveyor 2 Error");
                        return;
                    }
                    //------------------------------------------------------------------
                    //change conveyor ready
                    InputState = 0;
                    //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor2ReadyChannel, MyStatic.DigitalInputs.Conveyor2ReadyBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor2ReadyBit, ref InputState, ref s))
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (InputState == 1)
                    {
                        ChangeConveyor2TimeOut.Reset();
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] = 0;
                        MainHMI.NewMainHMI.PartIndexNew = MainHMI.NewMainHMI.PartIndex;
                        MainHMI.NewMainHMI.FirstTime = true;
                        MainHMI.NewMainHMI.FirstTimeIfNotFount = true;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                    }
                    break;
                default:
                    break;
                    //hfawhefoaweioaw
            }
            #endregion
        }

        public void MainLoopWithFlipStation()
        {
            #region Robot Pick
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick])
            {
                case 1:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] == (int)MyStatic.CmdFini.CamPickVision)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] = 0;
                        string ErrMessage = "";
                        if (!MainHMI.NewMainHMI.RunCmd10(ref ErrMessage, MyStatic.Speed))
                        {
                            SetError(ErrMessage,"RobotError");
                            return;
                        }
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 2;
                    }
                    break;
                case 2:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] == (int)MyStatic.CmdFini.RobotPick)
                    {
                        if (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] != 0)
                        {
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = 0;
                            return;
                        }
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToFlipStation] = 1;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] == (int)MyStatic.CmdErr.RobotPick)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
            }
            #endregion
            #region Robot PLace to Flip Station
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToFlipStation])
            {
                case 1:
                    string ErrMessage = "";
                    if (!MainHMI.NewMainHMI.RunCmd45(ref ErrMessage, 0, 180, MyStatic.Speed))
                    {
                        SetError(ErrMessage, "RobotError");
                        return;
                    }
                    MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToFlipStation] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToFlipStation] == (int)MyStatic.CmdFini.RobotPlaceToFlipStation)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToFlipStation] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToFlipStation] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation] = 1;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToFlipStation] == (int)MyStatic.CmdErr.RobotPlaceToFlipStation)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion
            #region Robot Pick From Flip Station
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation])
            {
                case 1:
                    string ErrMessage = "";
                    if (!MainHMI.NewMainHMI.RunCmd55(ref ErrMessage, 0, true, MyStatic.Speed))
                    {
                        SetError(ErrMessage, "RobotError");
                        return;
                    }
                    MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPickFromFlipStation] == (int)MyStatic.CmdFini.RobotPickFromFlipStation)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPickFromFlipStation] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 1;
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPickFromFlipStation] == (int)MyStatic.CmdErr.RobotPickFromFlipStation)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion
            #region Robot Place 
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace])
            {
                case 1:
                    string ErrMessage = "";
                    if (!MainHMI.NewMainHMI.RunCmd11(ref ErrMessage, MyStatic.Speed))
                    {
                        SetError(ErrMessage, "RobotError");
                        return;
                    }
                    MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] == (int)MyStatic.CmdFini.RobotPlace)
                    {
                        if (MyStatic.bStopCycle) //go home and stop
                        {
                            string ErrMessage1 = "";
                            if (!MainHMI.NewMainHMI.RunCmd12(ref ErrMessage1, MyStatic.Speed))
                            {
                                SetError(ErrMessage1, "RobotError");
                                return;
                            }
                            if (MainHMI.NewMainHMI.NumOfNotPickedInserts >= MainHMI.NewMainHMI.numNotPicked.Value) //4
                            {
                                MainHMI.NewMainHMI.NumOfNotPickedInserts = 0;
                                SetError("Robot Can't Take " + (MainHMI.NewMainHMI.numNotPicked.Value+1).ToString() + " Inserts In ROI", "Pick Error"); //5
                            }
                            MyStatic.bStartCycle = false; return;
                        }
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                        MyStatic.InsertCount++;
                        if (MyStatic.InsertCount > 99999) MyStatic.InsertCount = 0;
                        inv.settxt(MainHMI.NewMainHMI.lblInsertsCount, MyStatic.InsertCount.ToString());
                        inv.settxt(MainHMI.NewMainHMI.lblCycleTime, (MyStatic.CycleTime.ElapsedMilliseconds / 1000f).ToString("0.00"));
                        MyStatic.CycleTime.Restart();
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] == (int)MyStatic.CmdErr.RobotPlace)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion

            int nValue;
            byte InputState;
            string s = "";

            #region Change Conveyor 1
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1])
            {
                case 1:
                    //Output To plc To Change Conveyor 1
                        //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray1Channel, MyStatic.DigitalOutput.ChangeTray1Bit, 1);
                    nValue = 1;
                    if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray1Bit, nValue, ref s))
                    {
                        ChangeConveyor1TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    Thread.Sleep(1000);
                        //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray1Channel, MyStatic.DigitalOutput.ChangeTray1Bit, 0);
                    nValue = 0;
                    if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray1Bit, nValue, ref s)) {
                        ChangeConveyor1TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    ChangeConveyor1TimeOut.Reset();
                    ChangeConveyor1TimeOut.Start();
                    MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] = 2;
                    break;
                case 2://toshiba R1 place fini

                    if (ChangeConveyor1TimeOut.ElapsedMilliseconds > ReadConveyorStatusTimeOut)
                    {
                        InputState = 0;
                        s = "";
                        if (!MainHMI.NewMainHMI.MBmasterReadM(234, ref InputState, ref s) || (InputState == 1))
                        {
                            SetError(s, "Tray Stack is empty");
                                //MessageBox.Show(s, "Tray Stack is empty", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                //MainHMI.btnStop_Click(null, null);
                            MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] = 0;
                                //MyStatic.bStartCycle = false;
                            break;
                        }
                    }


                    if (ChangeConveyor1TimeOut.ElapsedMilliseconds > ChangeConveyorTimeOut)
                    {
                        ChangeConveyor1TimeOut.Reset();
                        SetError("Error To Change Conveyor 1", "Conveyor 1 Error By Time Out");
                        return;
                    }
                    //check Conveyor Error
                    InputState = 1;
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor1ErrorChannel, MyStatic.DigitalInputs.Conveyor1ErrorBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor1ErrorBit, ref InputState, ref s))
                    {
                        ChangeConveyor1TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (InputState == 1) //0
                    {
                        ChangeConveyor1TimeOut.Reset();
                        SetError("Error To Change Conveyor 1", "Conveyor 1 Error");
                        return;
                    }
                    //------------------------------------------------------------------
                    //change conveyor ready
                    InputState = 0; //1
                    //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor1ReadyChannel, MyStatic.DigitalInputs.Conveyor1ReadyBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor1ReadyBit, ref InputState, ref s)) {
                        ChangeConveyor1TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (InputState == 1)
                    {
                        ChangeConveyor1TimeOut.Reset();
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                    }
                    break;
                default:
                    break;
                    //hfawhefoaweioaw
            }
            #endregion
            #region Change Conveyor 2
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2])
            {
                case 1:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotHome] == (int)MyStatic.CmdFini.RobotHome)
                    {
                        //if (MainHMI.NewMainHMI.WorkMode.PlasticToPlastic)
                        //    inv.click(MainHMI.NewMainHMI.btnSavePlasticTrayStatus);

                        //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray2Channel, MyStatic.DigitalOutput.ChangeTray2Bit, 1);
                        if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray2Bit, 1, ref s)) {
                            ChangeConveyor2TimeOut.Reset();
                            SetDeltaError(s);
                            //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        Thread.Sleep(1000);
                        //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray2Channel, MyStatic.DigitalOutput.ChangeTray2Bit, 0);
                        if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray2Bit, 0, ref s)) {
                            ChangeConveyor2TimeOut.Reset();
                            SetDeltaError(s);
                            //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        ChangeConveyor2TimeOut.Reset();
                        ChangeConveyor2TimeOut.Start();
                        //output to change conveyor 2
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] = 2;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotHome] == (int)MyStatic.CmdErr.RobotHome)
                    {
                    }
                    break;
                case 2://toshiba R1 place fini

                    if (ChangeConveyor2TimeOut.ElapsedMilliseconds > ReadConveyorStatusTimeOut)
                    {
                        InputState = 0;
                        s = "";
                        if (!MainHMI.NewMainHMI.MBmasterReadM(244, ref InputState, ref s) || (InputState == 1))
                        {
                            SetError(s, "Tray Stack is empty");
                                //MessageBox.Show(s, "Tray Stack is empty", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                //MainHMI.btnStop_Click(null, null);
                            MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] = 0;
                                //MyStatic.bStartCycle = false;
                            break;
                        }
                    }

                    if (ChangeConveyor2TimeOut.ElapsedMilliseconds > ChangeConveyorTimeOut)
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetError("Error To Change Conveyor 2", "Conveyor 2 Error By Time Out");
                        return;
                    }
                    //check Conveyor Error
                    InputState = 1;
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor2ErrorChannel, MyStatic.DigitalInputs.Conveyor2ErrorBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor2ErrorBit, ref InputState, ref s))
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (InputState == 1) //0
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetError("Error To Change Conveyor 2", "Conveyor 2 Error");
                        return;
                    }
                    //------------------------------------------------------------------
                    //change conveyor ready
                    InputState = 0;
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor2ReadyChannel, MyStatic.DigitalInputs.Conveyor2ReadyBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor2ReadyBit, ref InputState, ref s))
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (InputState == 1)
                    {
                        ChangeConveyor2TimeOut.Reset();
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                    }
                    break;
                default:
                    break;
                    //hfawhefoaweioaw
            }
            #endregion
        }

        public void MainLoopWaterMark()
        {
            #region Robot Pick
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick])
            {
                case 1:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] == (int)MyStatic.CmdFini.CamPickVision)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] = 0;
                        string ErrMessage = "";
                        MyStatic.WasRotated180 = false;
                        if (!MainHMI.NewMainHMI.RunCmd10(ref ErrMessage, MyStatic.Speed))
                        {
                            SetError(ErrMessage, "RobotError");
                            return;
                        }
                        //MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = (int)MyStatic.CmdFini.RobotPick;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 2;
                    }
                    break;
                case 2:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] == (int)MyStatic.CmdFini.RobotPick)
                    {
                        MainHMI.NewMainHMI.PartIndexPlasticPick = MainHMI.NewMainHMI.PartIndexNew;

                        //if (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] != 0)
                        //{
                        //    MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                        //    MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = 0;
                        //    return;
                        //}
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotWaterMarkPoint] = 1;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] == (int)MyStatic.CmdErr.RobotPick)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
            }
            #endregion
            #region Robot Water Mark Point 
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotWaterMarkPoint])
            {
                case 1:
                    string ErrMessage = "";
                    if (!MainHMI.NewMainHMI.RunCmd35(ref ErrMessage, MyStatic.Speed))
                    {
                        SetError(ErrMessage, "RobotError");
                        return;
                    }
                    //MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotWaterMarkPoint] = (int)MyStatic.CmdFini.RobotWaterMarkPoint;
                    MyStatic.CycleStep[(int)MyStatic.Actions.RobotWaterMarkPoint] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotWaterMarkPoint] == (int)MyStatic.CmdFini.RobotWaterMarkPoint)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotWaterMarkPoint] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotWaterMarkPoint] = 0;
                        //MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 1;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamWaterMarkVision] = 1;
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotWaterMarkPoint] == (int)MyStatic.CmdErr.RobotWaterMarkPoint)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion
            #region Robot Water Mark Rotate 180
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.WaterMarkRotate180InRobot])
            {
                case 1:
                    string ErrMessage = "";
                    if (!MainHMI.NewMainHMI.RunCmd35(ref ErrMessage, MyStatic.Speed,true))
                    {
                        SetError(ErrMessage, "RobotError");
                        return;
                    }
                    //MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotWaterMarkPoint] = (int)MyStatic.CmdFini.RobotWaterMarkPoint;
                    MyStatic.CycleStep[(int)MyStatic.Actions.WaterMarkRotate180InRobot] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotWaterMarkPoint] == (int)MyStatic.CmdFini.RobotWaterMarkPoint)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotWaterMarkPoint] = 0;
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.WaterMarkRotate180InRobot] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.WaterMarkRotate180InRobot] = 0;
                        //MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 1;
                        //MyStatic.WasRotated180 = true;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamWaterMarkVision] = 1;
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotWaterMarkPoint] == (int)MyStatic.CmdErr.RobotWaterMarkPoint)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion
            #region Robot Place to Flip Station
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToFlipStation])
            {
                case 1:
                    string ErrMessage = "";
                    if (!MainHMI.NewMainHMI.RunCmd45(ref ErrMessage, 0, 180, MyStatic.Speed))
                    {
                        SetError(ErrMessage, "RobotError");
                        return;
                    }
                    MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToFlipStation] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToFlipStation] == (int)MyStatic.CmdFini.RobotPlaceToFlipStation)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToFlipStation] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToFlipStation] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation] = 1;
                        //MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToFlipStation] == (int)MyStatic.CmdErr.RobotPlaceToFlipStation)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion
            #region Robot Pick From Flip Station
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation])
            {
                case 1:
                    string ErrMessage = "";
                    if (!MainHMI.NewMainHMI.RunCmd55(ref ErrMessage, 0, false, MyStatic.Speed))
                    {
                        SetError(ErrMessage, "RobotError");
                        return;
                    }
                    MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPickFromFlipStation] == (int)MyStatic.CmdFini.RobotPickFromFlipStation)
                    {

                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPickFromFlipStation] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPickFromFlipStation] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 1;
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPickFromFlipStation] == (int)MyStatic.CmdErr.RobotPickFromFlipStation)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion
            #region Robot Place 
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace])
            {
                case 1:
                    if (MainHMI.NewMainHMI.WorkMode.PlasticToPlastic)
                    {
                        if ((MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] == (int)MyStatic.CmdFini.CamPickVision) || MyStatic.bStopCycle)
                        {
                            string ErrMessage = "";

                            int indtemp = MainHMI.NewMainHMI.PartIndex;
                            if (MainHMI.NewMainHMI.PartIndexPlasticPick >= 0)
                                MainHMI.NewMainHMI.PartIndex = MainHMI.NewMainHMI.PartIndexPlasticPick;

                            if (!MainHMI.NewMainHMI.RunCmd11(ref ErrMessage, MyStatic.Speed))
                            {
                                MainHMI.NewMainHMI.PartIndex = indtemp;
                                SetError(ErrMessage, "RobotError");
                                return;
                            }
                            MainHMI.NewMainHMI.PartIndex = indtemp;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 2;
                            break;
                        }
                    }
                    else
                    {
                        string ErrMessage = "";
                        if (!MainHMI.NewMainHMI.RunCmd11(ref ErrMessage, MyStatic.Speed))
                        {
                            SetError(ErrMessage, "RobotError");
                            return;
                        }
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 2;
                        break;
                    }
                    //MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] = (int)MyStatic.CmdFini.RobotPlace;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] == (int)MyStatic.CmdFini.RobotPlace)
                    {
                        if (MyStatic.bStopCycle)
                        {
                            string ErrMessage1 = "";
                            if (!MainHMI.NewMainHMI.RunCmd12(ref ErrMessage1, MyStatic.Speed))
                            {
                                SetError(ErrMessage1, "RobotError");
                                return;
                            }
                            MyStatic.bStartCycle = false; return;
                        }
                        if (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] != 0)
                        {
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 0;
                            return;
                        }
                        else
                        {
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                            MyStatic.InsertCount++;
                            if (MyStatic.InsertCount > 99999) MyStatic.InsertCount = 0;
                            inv.settxt(MainHMI.NewMainHMI.lblInsertsCount, MyStatic.InsertCount.ToString());
                            inv.settxt(MainHMI.NewMainHMI.lblCycleTime,(MyStatic.CycleTime.ElapsedMilliseconds/1000f).ToString("0.00"));
                            MyStatic.CycleTime.Restart();
                            break;
                        }
                        break;
                    }
                    if(MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] == (int)MyStatic.CmdErr.RobotPlace)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion
            #region Robot Place To Reject 
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToReject])
            {
                case 1:
                    if (MainHMI.NewMainHMI.WorkMode.PlasticToPlastic)
                    {
                        if ((MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] == (int)MyStatic.CmdFini.CamPickVision) || MyStatic.bStopCycle)
                        {
                            string ErrMessage = "";
                            if (!MainHMI.NewMainHMI.RunCmd65(ref ErrMessage, MyStatic.Speed))
                            {
                                SetError(ErrMessage, "RobotError");
                                return;
                            }
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToReject] = 2;
                            break;
                        }
                    }
                    else
                    {
                        string ErrMessage = "";
                        if (!MainHMI.NewMainHMI.RunCmd11(ref ErrMessage, MyStatic.Speed))
                        {
                            SetError(ErrMessage, "RobotError");
                            return;
                        }
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToReject] = 2;
                        break;
                    }
                    //MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] = (int)MyStatic.CmdFini.RobotPlace;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToReject] == (int)MyStatic.CmdFini.RobotPlaceToReject)
                    {
                        if (MyStatic.bStopCycle)
                        {
                            string ErrMessage1 = "";
                            if (!MainHMI.NewMainHMI.RunCmd12(ref ErrMessage1, MyStatic.Speed))
                            {
                                SetError(ErrMessage1, "RobotError");
                                return;
                            }
                            MyStatic.bStartCycle = false; return;
                        }
                        if (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] != 0)
                        {
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToReject] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToReject] = 0;
                            return;
                        }
                        else
                        {
                            MyStatic.InsertCount++;
                            if (MyStatic.InsertCount > 99999) MyStatic.InsertCount = 0;
                            inv.settxt(MainHMI.NewMainHMI.lblInsertsCount, MyStatic.InsertCount.ToString());
                            inv.settxt(MainHMI.NewMainHMI.lblCycleTime, (MyStatic.CycleTime.ElapsedMilliseconds / 1000f).ToString("0.00"));
                            MyStatic.CycleTime.Restart();
                            MyStatic.RejectedInsetCount++;
                            inv.settxt(MainHMI.NewMainHMI.lblRejectInsertsCount, MyStatic.RejectedInsetCount.ToString());
                            if (MyStatic.RejectedInsetCount > MainHMI.NewMainHMI.RejectTray.Length)
                            {
                                SetError("Please Empty Rejected Tray", "Rejected Tray Is Full");
                                return;
                            }
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlaceToReject] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlaceToReject] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                            break;
                        }
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] == (int)MyStatic.CmdErr.RobotPlace)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion

            int nValue;
            byte InputState;
            string s = "";

            #region Change Conveyor 1
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1])
            {
                case 1:
                    //Output To plc To Change Conveyor 1
                        //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray1Channel, MyStatic.DigitalOutput.ChangeTray1Bit, 1);
                    nValue = 1;
                    if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray1Bit, nValue, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Thread.Sleep(1000);
                        //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray1Channel, MyStatic.DigitalOutput.ChangeTray1Bit, 0);
                    nValue = 0;
                    if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray1Bit, nValue, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ChangeConveyor1TimeOut.Reset();
                    ChangeConveyor1TimeOut.Start();
                    MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (ChangeConveyor1TimeOut.ElapsedMilliseconds > ChangeConveyorTimeOut)
                    {
                        ChangeConveyor1TimeOut.Reset();
                        SetError("Error To Change Conveyor 1", "Conveyor 1 Error By Time Out");
                        return;
                    }
                    //check Conveyor Error
                    InputState = 1;
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor1ErrorChannel, MyStatic.DigitalInputs.Conveyor1ErrorBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor1ErrorBit, ref InputState, ref s))
                    {
                        ChangeConveyor1TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (InputState == 1) //0
                    {
                        ChangeConveyor1TimeOut.Reset();
                        SetError("Error To Change Conveyor 1", "Conveyor 1 Error");
                        return;
                    }
                    //------------------------------------------------------------------
                    //change conveyor ready
                    InputState = 0;
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor1ReadyChannel, MyStatic.DigitalInputs.Conveyor1ReadyBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor1ReadyBit, ref InputState, ref s)) {
                        ChangeConveyor1TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (InputState == 1)
                    {
                        ChangeConveyor1TimeOut.Reset();
                        //check inputs is conveyor ready
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                    }
                    break;
                default:
                    break;
                    //hfawhefoaweioaw
            }
            #endregion
            #region Change Conveyor 2
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2])
            {
                case 1:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotHome] == (int)MyStatic.CmdFini.RobotHome)
                    {
                        //if (MainHMI.NewMainHMI.WorkMode.PlasticToPlastic)
                        //    inv.click(MainHMI.NewMainHMI.btnSavePlasticTrayStatus);

                            //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray2Channel, MyStatic.DigitalOutput.ChangeTray2Bit, 1);
                        if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray2Bit, 1, ref s)) {
                            ChangeConveyor2TimeOut.Reset();
                            SetDeltaError(s);
                            //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        Thread.Sleep(1000);
                            //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray2Channel, MyStatic.DigitalOutput.ChangeTray2Bit, 0);
                        if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray2Bit, 0, ref s)) {
                            ChangeConveyor2TimeOut.Reset();
                            SetDeltaError(s);
                            //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        ChangeConveyor2TimeOut.Reset();
                        ChangeConveyor2TimeOut.Start();
                        //output to change conveyor 2
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] = 2;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotHome] == (int)MyStatic.CmdErr.RobotHome)
                    {
                    }
                    //Output To plc To Change Conveyor 1
                    break;
                case 2://toshiba R1 place fini
                    if (ChangeConveyor2TimeOut.ElapsedMilliseconds > ChangeConveyorTimeOut)
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetError("Error To Change Conveyor 2", "Conveyor 2 Error By Time Out");
                        return;
                    }
                    //check Conveyor Error
                    InputState = 1;
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor2ErrorChannel, MyStatic.DigitalInputs.Conveyor2ErrorBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor2ErrorBit, ref InputState, ref s)) {
                        ChangeConveyor2TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (InputState == 1) //0
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetError("Error To Change Conveyor 2", "Conveyor 2 Error");
                        return;
                    }
                    //------------------------------------------------------------------
                    //change conveyor ready
                    InputState = 0;
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor2ReadyChannel, MyStatic.DigitalInputs.Conveyor2ReadyBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor2ReadyBit, ref InputState, ref s))
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (InputState == 1)
                    {
                        ChangeConveyor2TimeOut.Reset();
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] = 0;
                        MainHMI.NewMainHMI.PartIndexNew = MainHMI.NewMainHMI.PartIndex;
                        MainHMI.NewMainHMI.FirstTime = true;
                        MainHMI.NewMainHMI.FirstTimeIfNotFount = true;
                        ///first
                        //MyStatic.CycleStep[(int)MyStatic.Actions.RobotWaterMarkPoint] = 2;
                        //MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotWaterMarkPoint] = (int)MyStatic.CmdFini.RobotWaterMarkPoint;
                        //RefreshTrayPlace(0);
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                        //MyStatic.CycleStep[(int)MyStatic.Actions.RobotWaterMarkPoint] = 2;
                        //MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotWaterMarkPoint] = (int)MyStatic.CmdFini.RobotWaterMarkPoint;
                    }
                    break;
                default:
                    break;
                    //hfawhefoaweioaw
            }
            #endregion
        }

        public void MainLoopPickPlace()
        {
            #region Robot Pick
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick])
            {
                case 1:
                    if (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] != 0)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                        return;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] == (int)MyStatic.CmdFini.CamPickVision)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.CamPickVision] = 0;
                        string ErrMessage = "";
                        if (!MainHMI.NewMainHMI.RunCmd10(ref ErrMessage, MyStatic.Speed))
                        {
                            SetError(ErrMessage, "RobotError");
                            return;
                        }
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 2;
                    }
                    break;
                case 2:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] == (int)MyStatic.CmdFini.RobotPick)
                    {
                        if(MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] != 0)
                        {
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = 0;
                            return;
                        }
                        if (MainHMI.NewMainHMI.checkBoxCellVision.Checked)
                        {
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 3;
                            MyStatic.CycleStep[(int)MyStatic.Actions.CellVision] = 1;
                        }
                        else
                        {
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 1;
                        }
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] == (int)MyStatic.CmdErr.RobotPick)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                case 3:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.CellVision] == (int)MyStatic.CmdFini.CellVision)
                    {
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.CellVision] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 0;
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPick] = 0;

                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 1;
                    }
                    break;
            }
            #endregion
            #region Robot Place 
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace])
            {
                case 1:
                    string ErrMessage = "";
                    if (!MainHMI.NewMainHMI.RunCmd11(ref ErrMessage, MyStatic.Speed))
                    {
                        SetError(ErrMessage, "RobotError");
                        return;
                    }
                    MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 2;
                    break;
                case 2://toshiba R1 place fini
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] == (int)MyStatic.CmdFini.RobotPlace)
                    {
                        if (MyStatic.bStopCycle) //go home and stop
                        {
                            string ErrMessage1 = "";
                            if (!MainHMI.NewMainHMI.RunCmd12(ref ErrMessage1, MyStatic.Speed))
                            {
                                SetError(ErrMessage1, "RobotError");
                                return;
                            }
                            if (MainHMI.NewMainHMI.NumOfNotPickedInserts >= MainHMI.NewMainHMI.numNotPicked.Value) //4
                            {
                                MainHMI.NewMainHMI.NumOfNotPickedInserts = 0;
                                SetError("Robot Can't Take " + (MainHMI.NewMainHMI.numNotPicked.Value+1).ToString() + " Inserts In ROI", "Pick Error"); //5
                            }
                            MyStatic.bStartCycle = false; return;
                        }
                        if (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1]!=0)
                        {
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 0;
                            return;
                        }
                        if (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] != 0)
                        {
                            MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                            MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] = 0;
                            MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 0;
                            return;
                        }
                        //Thread.Sleep(100);
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                        MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPlace] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                        MyStatic.InsertCount++;
                        if (MyStatic.InsertCount > 99999) MyStatic.InsertCount = 0;
                        inv.settxt(MainHMI.NewMainHMI.lblInsertsCount, MyStatic.InsertCount.ToString());
                        inv.settxt(MainHMI.NewMainHMI.lblCycleTime, (MyStatic.CycleTime.ElapsedMilliseconds/1000f).ToString("0.00"));
                        MyStatic.CycleTime.Restart();
                        //MyStatic.CycleTime.Start();
                        break;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotPlace] == (int)MyStatic.CmdErr.RobotPlace)
                    {
                        MyStatic.bStartCycle = false;
                    }
                    break;
                default:
                    break;
            }
            #endregion

            int nValue;
            byte InputState;
            string s = "";

            #region Change Conveyor 1
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1])
            {
                case 1:
                    //Output To plc To Change Conveyor 1
                        //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray1Channel, MyStatic.DigitalOutput.ChangeTray1Bit, 1);
                    if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray1Bit, 1, ref s)) {
                        ChangeConveyor1TimeOut.Reset();
                        SetDeltaError(s);
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    Thread.Sleep(1000);
                            //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray1Channel, MyStatic.DigitalOutput.ChangeTray1Bit, 0);
                        //if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray1Bit, nValue, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ChangeConveyor1TimeOut.Reset();
                    ChangeConveyor1TimeOut.Start();
                    MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] = 2;
                    break;

                case 2://toshiba R1 place fini
                    /*
                    bool bExit = false;
                    while (ChangeConveyor1TimeOut.ElapsedMilliseconds < 50000 && !bExit) {
                        InputState = 0;
                        if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor1ErrorBit, ref InputState, ref s))
                            bExit = true;
                        else {
                            if (InputState == 1)
                                bExit = true;
                            else {
                                if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor1ReadyBit, ref InputState, ref s))
                                    bExit = true;
                                else
                                    if (InputState == 1)
                                    bExit = true;
                            }
                        }
                        Thread.Sleep(200);
                    }
                    */
                    Thread.Sleep(100);

                    if (ChangeConveyor1TimeOut.ElapsedMilliseconds > ReadConveyorStatusTimeOut)
                    {
                        InputState = 0;
                        s = "";
                        if (!MainHMI.NewMainHMI.MBmasterReadM(234, ref InputState, ref s) || (InputState == 1))
                        {
                            SetError(s, "Tray Stack is empty");
                            //MessageBox.Show(s, "Tray Stack is empty", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            
                            MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] = 0;

                            /*
                            MyStatic.bStartCycle = false;
                            inv.click(MainHMI.NewMainHMI.btnStop);
                            inv.click(MainHMI.NewMainHMI.btnRobotHome);
                            */
                            // MainHMI.btnStop_Click(null, null);
                            // this.Invoke(new Action(() => { MainHMI.NewMainHMI.btnStop.PerformClick();}));
                            /*
                            MainHMI.NewMainHMI.SetPanelsEnable(1);
                            s = "";
                            //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.GreenLightChannel, MyStatic.DigitalOutput.GreenLightBit, 0);
                            //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.YellowLightChannel, MyStatic.DigitalOutput.YellowLightBit, 1);
                            nValue = 0;
                            if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.GreenLightBit, nValue, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            nValue = 1;
                            if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.YellowLightBit, nValue, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            MyStatic.bStopCycle = true;
                            */
                            break;
                        }
                    }

                    if (ChangeConveyor1TimeOut.ElapsedMilliseconds > ChangeConveyorTimeOut)
                    {
                        ChangeConveyor1TimeOut.Reset();
                        SetError("Error To Change Conveyor 1", "Conveyor 1 Error By Time Out");
                        return;
                    }
                    //check Conveyor Error
                    InputState = 1;
                    s = "";
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor1ErrorChannel, MyStatic.DigitalInputs.Conveyor1ErrorBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor1ErrorBit, ref InputState, ref s)) {
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ChangeConveyor1TimeOut.Reset();
                        SetError("Error To Change Conveyor 1 - DELTA Error (1) : " + s, "Conveyor 1 Error");
                        return;
                    }
                    if (InputState == 1) {
                        ChangeConveyor1TimeOut.Reset();
                        SetError("Error To Change Conveyor 1", "Conveyor 1 Error");
                        return;
                    }

                    Thread.Sleep(100);

                    //------------------------------------------------------------------
                    //check conveyor ready
                    InputState = 0;
                    s = "";
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor1ReadyChannel, MyStatic.DigitalInputs.Conveyor1ReadyBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor1ReadyBit, ref InputState, ref s)) {
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ChangeConveyor1TimeOut.Reset();
                        SetError("Error To Change Conveyor 1 - DELTA Error (2) : " + s, "Conveyor 1 Error");
                        return;
                    }
                    if (InputState == 1) {
                        //Thread.Sleep(1000);
                        ChangeConveyor1TimeOut.Reset();
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor1] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                    }
                    break;
                default:
                    break;
                    //hfawhefoaweioaw
            }
            #endregion
            #region Change Conveyor 2
            switch (MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2])
            {
                case 1:
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotHome] == (int)MyStatic.CmdFini.RobotHome)
                    {
                        //if (MainHMI.NewMainHMI.WorkMode.PlasticToPlastic)
                        //    inv.click(MainHMI.NewMainHMI.btnSavePlasticTrayStatus);

                        MyStatic.CycleStep[(int)MyStatic.Actions.CamPickVision] = 1;
                            //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray2Channel, MyStatic.DigitalOutput.ChangeTray2Bit, 1);
                        if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray2Bit, 1, ref s))
                        {
                            //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            ChangeConveyor2TimeOut.Reset();
                            SetError("Error To Change Conveyor 2 - DELTA Error", "Conveyor 2 Error");
                            return;
                        }
                        Thread.Sleep(1000);
                            //MainHMI.NewMainHMI.IOCardOutput(MyStatic.DigitalOutput.ChangeTray2Channel, MyStatic.DigitalOutput.ChangeTray2Bit, 0);
                        //nValue = 0;
                        //if (!MainHMI.NewMainHMI.MBmasterWriteM(MyStatic.DigitalOutput.ChangeTray2Bit, nValue, ref s)) MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ChangeConveyor2TimeOut.Reset();
                        ChangeConveyor2TimeOut.Start();
                        //output to change conveyor 2
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] = 2;
                    }
                    if (MyStatic.MainCmdFini[(int)MyStatic.Actions.RobotHome] == (int)MyStatic.CmdErr.RobotHome)
                    {
                    }
                    break;

                case 2://toshiba R1 place fini
                    /*
                    bool bExit = false;
                    while (ChangeConveyor2TimeOut.ElapsedMilliseconds < 50000 && !bExit) {
                        InputState = 0;
                        if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor2ErrorBit, ref InputState, ref s))
                            bExit = true;
                        else {
                            if (InputState == 1)
                                bExit = true;
                            else {
                                if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor2ReadyBit, ref InputState, ref s))
                                    bExit = true;
                                else
                                    if (InputState == 1)
                                    bExit = true;
                            }
                        }
                        Thread.Sleep(200);
                    }
                    */

                    if (ChangeConveyor2TimeOut.ElapsedMilliseconds > ReadConveyorStatusTimeOut) {
                        InputState = 0;
                        s = "";
                        if (!MainHMI.NewMainHMI.MBmasterReadM(244, ref InputState, ref s) || (InputState == 1))
                        {
                            ChangeConveyor2TimeOut.Reset();
                            SetError(s, "Tray Stack is empty");
                            //MessageBox.Show(s, "Tray Stack is empty", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            //MainHMI.btnStop_Click(null, null);
                            MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] = 0;
                            //MyStatic.bStartCycle = false;
                            break;
                        }
                    }
                    Thread.Sleep(100);

                    if (ChangeConveyor2TimeOut.ElapsedMilliseconds > ChangeConveyorTimeOut)
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetError("Error To Change Conveyor 2", "Conveyor 2 Error By Time Out");
                        return;
                    }
                    //check Conveyor Error
                    InputState = 1;
                    s = "";
                        //BDaqOcxLib.ErrorCode err = BDaqOcxLib.ErrorCode.Success;
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor2ErrorChannel, MyStatic.DigitalInputs.Conveyor2ErrorBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor2ErrorBit, ref InputState, ref s))
                    {
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ChangeConveyor2TimeOut.Reset();
                        SetError("Error To Change Conveyor 2 - DELTA Error (1) : " + s, "Conveyor 2 Error");
                        return;
                    }
                        //MainHMI.NewMainHMI.IOCardInput(MyStatic.DigitalInputs.Conveyor2ErrorChannel, MyStatic.DigitalInputs.Conveyor2ErrorBit, ref InputState);
                    if (InputState == 1)
                    {
                        ChangeConveyor2TimeOut.Reset();
                        SetError("Error To Change Conveyor 2", "Conveyor 2 Error");
                        return;
                    }
                    Thread.Sleep(100);

                    //------------------------------------------------------------------
                    //change conveyor ready
                    InputState = 0;
                    s = "";
                        //MainHMI.NewMainHMI.DigitalInputs.ReadBit(MyStatic.DigitalInputs.Conveyor2ReadyChannel, MyStatic.DigitalInputs.Conveyor2ReadyBit, out InputState);
                    if (!MainHMI.NewMainHMI.MBmasterReadM(MyStatic.DigitalInputs.Conveyor2ReadyBit, ref InputState, ref s))
                    {
                        //MessageBox.Show(s, "DELTA Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ChangeConveyor2TimeOut.Reset();
                        SetError("Error To Change Conveyor 2 - DELTA Error (2) : " + s, "Conveyor 2 Error");
                        return;
                    }
                    //Thread.Sleep(200);
                    if (InputState == 1)
                    {
                        ChangeConveyor2TimeOut.Reset();
                        MyStatic.CycleStep[(int)MyStatic.Actions.ChangeConveyor2] = 0;
                        MyStatic.CycleStep[(int)MyStatic.Actions.RobotPick] = 1;
                    }
                    break;
                default:
                    break;
                    //hfawhefoaweioaw
            }
            #endregion
        }

        public void bwMainCycle_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //MainHMI.NewMainHMI.SetPanelsEnable(true);
            try
            {
                inv.settxt(MainHMI.NewMainHMI.lblCycleStatus, "Stopped.....");
                if (e.Result == null) return;

                DateTime NowTime = DateTime.Now;
                Stopwatch stopper = new Stopwatch();
                stopper.Restart();
                while (bwMainCycle.IsBusy) //wait end busy for 2 sec
                {
                    NowTime = DateTime.Now;
                    if (300 < stopper.ElapsedMilliseconds) break;
                    Thread.Sleep(2);
                }
                //PlcReply GetParams = new PlcReply();
                //Single[] CycleReply;
                //CycleReply = (Single[])e.Result;

                MainCycleParameters ReplyParameters = new MainCycleParameters();

                ReplyParameters = (MainCycleParameters)e.Result;
                //if (CycleReply[2] == 0) return;
                stopper.Stop();
                Single[] result = ReplyParameters.data;
                GetFini(result);
                return;
            }
            catch
            {
                MessageBox.Show("Error");
                MainHMI.NewMainHMI.ResetAllFlags();
                MyStatic.bStartCycle = false;
                return;
            }
        }
        //  Stopwatch StopperCmd = new Stopwatch();
    }
}
