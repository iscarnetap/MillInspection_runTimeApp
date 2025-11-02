using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Drawing;

namespace Inspection
{
    public class MyStatic
    {
        public static Boolean bSafeSpeed = false;
        public const int Stop = 3;
       
        public static Boolean bReset = false;
        public static Boolean bPrinterAwake = false;
        public static Boolean bPortReading = false;
        public static Boolean bExitcycle = false;
        //public static Boolean bClearTable = false;
        public static Boolean bStartcycle = false;
        public static Boolean bManual = false;
        public static Boolean bLast = false;
        public static Boolean bEmpty = false;
        
        public static int Robot = 0;
        public const int Robot1 = 1;
         public const int Robot2 = 2;
        

        public static Boolean bNotUpdatePlace = false;
        public static IntPtr MyHandle;
        public static String MyName;
        public static int MainWidth;
        public static int MainHeight;
        public static Boolean bOnePosition = false;
        public static DateTime CycleTime;
        public static Boolean bOneCycle = false;

        public static Boolean BigScreen = true;
        public static Boolean startApp = false;
        //public static int SlaveID=1;
        public static int TimerInterval = 100;
        public static Boolean ReadingIO = false;
        public static Boolean ReadingPlc1IO = false;
        public static Boolean ReadingPlc2IO = false;
        public static int SetOut = -1;
        public static int SetHandOut = -1;
        public static int SetPlc1Out = -1;
        public static int SetPlc2Out = -1;
        public static Boolean WaitReady = false;
        public static Boolean RobotErrorMess = false;

        public static Int16 SafeSpeed = 5;
        public static bool CamBusy = false;
        public static int CamNum = 0;
        public static string cmbD70 = "";

        //public static bool PrinterDebug1=true;
        //public static bool PrinterDebug2=true;
        public static bool bMarking = true;
        public static bool VisionMarking = true;
        

        public static Boolean chkDebug = false;
        public static Boolean BeckhoffchkDebug = false;


        public static bool TaskExecute = false;
   
        public enum RobotCmd
        {

           Parm = 13,
           MoveMaint = 14,
           MoveHome = 12,
           SetTool = 19,
           TeachTool = 23,
           TrayCalib = 25,
           GripperAct = 15,
           PickTray = 10,
           PlaceTable = 43,
           PickTable = 44,
           PlaceTray = 11,
           PlaceReject = 65,
           CheckComm = 99,
           MoveSafe = 17,
                      


        }

       


        //public static bool FirstPallet = true;


        //fanuc
        public static bool bRobot1Running = false;
        
        //public static bool Robot1InAction = false;
        //public static bool Robot2InAction = false;
        public static bool Man3InAction = false;
        public static bool Man5InAction = false;
        public static bool Man6InAction = false;
        public static bool SuaInAction2 = false;
        public static bool SuaInAction3 = false;
        public static bool SuaInAction6 = false;
        public static bool SuaInAction7 = false;



        public static bool TableInAction = false;
        public static bool TableReady = false;

        public static bool ConveyorInAction1 = false;
        public static bool ConveyorInAction2 = false;
        public static bool Rot1 = false;
        public static bool Rot2 = false;
        

        public static bool ConvChangeOrder = false;
        public static bool StackRefreshAction = false;
        public static bool VisionInAction = false;
        public static bool VisionDebug = false;
        public static bool RobotDebug = false;
        public static bool TableDisable1 = false;
        public static bool TableDisable2 = false;
       

        public enum Vision
        {
             ncam = 2,
             nNext = 1,
             nFirstPlace = 2,
             noNext = 0,
             st2back = 1,//st2 back light measure
             st2backlock = 16,//st2 back light locator
             st2front = 11,//st2 front light inspect
             st2sua=22,//station2 sua inspection
             st3back = 2,
             st3backlock = 17,
             st3front = 12,//+10
             st3sua = 23,//station3 sua inspection
             st3sumo =20,
             st4 = 3,
             st5 = 5,
             st6back = 4,
             st6backlock = 18,
             st6front = 14,
             st6sua = 24,//station6 sua inspection
             st6sumo =21,
             st7back = 5,
             st7backlock = 19,
             st7front = 15,
             st7sua = 25,//station7 sua inspection
             st8 = 8,
             pick = 6,
             place = 7,
             st2sumo = 26,
             st7sumo = 27,
            // 1: measure 
            // 2: snap + locate + send coordinates in mm and pixels
            // 3: snap + locate + save snap + send coordinates in mm and pixels (surface)
            // 4: measure without snap and locate
            // 5: snap + surface without locator
            // 6: no snap + locate + send coordinates in mm and pixels (==2) (new)
            // 7: no snap + locate + save snap + send coordinates in mm and pixels (surface) (==3) (new)
            // 8: no snap + surface without locator (==5) (new)
             measure = 1,
             locator = 2,
             inspect = 3,
             measurenosnap = 4,
             inspectonly = 5,
             locatornosnap = 6,
             inspectnosnap = 7,
             inspectonlynosnap = 8,
            //camera1,... ,1,0,0,0,1 - snap 1 and 5 cams
            //camera1,... ,1,0,0,0,0 - snap 1 cam only
            //camera5,... ,0,0,0,0,1 - snap 5 cam only






        }
        public enum VisionCmd
        {
            snap  = 100,
            setRY = 101,
           
            model = 104,
            modelexist = 105,
            calib=106,
            aligment = 107,
            loadorder =108,


        }

        public struct VisionAction
        {
            public bool Ready;
            public bool Error;
            public bool Execute;
            public bool Reject;
            public bool Enable;
            public bool Rotated;
            public int Next;
            public int type;
            public string comment;
            public position pos;
            public position sua;
            public string SuaCmd;

        }
        public struct StationAct
        {
            public bool Ready;
            public int Error;
            public bool Execute;
            public bool Reject;
            public bool Enable;
            public bool Rotated;
            public bool InAction;
            public bool Measure;
            public bool Inspect;
            public bool Locator;
            public bool Sumo;
            public bool MeasureNoSnap;
            public bool InspectNoSnap;
            public bool LocatorNoSnap;
            public bool SumoNoSnap;
            public bool Empty;
            public bool ManReady;


        }
            public struct RobotAction
        {
            public int Action;
            public int OnGripPartID;
            public bool InAction;
            public bool InHome;
            public int PartNum;
            //public bool CanPlace;
            //public bool Placefini;

        }
       
        public static bool ReadPlcBusy = false;
        
        //public struct PartData
        //{
        //    public int State;//ontray;onRobot1;inCNC;inflip;onRobot2;ontrayout
        //    public Single pickX;
        //    public Single pickY;
        //    public Single pickZ;
        //    public Single pickR;
        //    public Single placeX;
        //    public Single placeY;
        //    public Single placeZ;
        //    public Single placeR;
        //    public bool OneSideReady;
        //    public bool Flipped;
        //    public bool Ready;


        //}
        public enum Camstate
        {
            Snap = 1,
            Finish = 2,
            Error = 3

        }
        public struct CameraData
        {
            public int State;
            public Single coordX;
            public Single coordY;
            public Single coordZ;
            public Single coordR;
            public bool Ready;


        }
        public static int SlaveID = 1;
        public static Int16 PlcTrayReady = 0;
        public static Int16 PlcSetReady = 1;
        public static Int16 PlcEndCycle = 3;
        public static Int16 PlcEndLoad = 0;//////////////////////////////////////
        public static Int16 PlcEndUnLoad = 1;
        public static Int16 PlcEndReset = 2;
        public static Int16 PlcEndReady = 4;
        public static Int16 PlcEndFanucStart = 5;

        public static int Doffset = 0;//32768;
        public static int Moffset = 0;
        
        public enum E_State
        {
            Empty = 0,
            Ready = 1,
            PartReady = 2,
            Reject = 3,
            End  = 4,
            RejectMeasure = 5,
            RejectInspect = 6,
            

        }
        public static bool[] RejectSt;

        //public const int StBox = 0;
        //public const int StPart = 1;
        //public const int StCover = 2;
        //public const int StIndex = 3;
        //public const int StBarcode1 = 4;
        //public const int StDiam = 5;
        //public const int StScale = 6;
        //public const int StDemagnet = 7;
        //public const int StPrint = 8;
        //public const int StBarcode2 = 9;
        //public const int StStack = 10;
        //public const int StPrintX = 11;
        //public const int StTrayLaser = 12;
        //public const int StTrayOut = 13;
        public const int StRobot1 = 1;
        public const int StRobot2 = 2;
        public const int Station1 = 3;
        public const int Station2 = 4;
        public const int Station3 = 5;
        public const int Station4 = 6;
        public const int Station5 = 7;
        public const int Station6 = 8;
        public const int Station7 = 9;
        public const int Station8 = 10;
        public const int StTrayPick = 11;
        public const int StTrayPlace = 12;
        
        public static int TrayId = 0;


        public static Int16 DplcLamps = 13;
        public static Int16 MplcLamps = 1;
        public static Int16 DplcTrafic = 14;
        public static Int16 MplcTrafic = 6;
        public int Mlamps = 1;
        public static int OrderNum = 0;
        public static int ConvStationNum = 0;
        public struct Station
        {
            public int State;// '0-empty,1-occupied,2-ready,3-flipped
            public int Num;
            public bool Act;
            public int PartNum;
            public bool ManEnable;
            public Single RotAngle;
            public int Times;
            public position pos;
            public bool Error;
            public string RejectType;
            public string RejectMeasure;
            public string RejectInspect;
            public int PictSize;
            public bool Empty;
        }
       
        public struct RobotData
        {
            public Single SpeedOvr;
            public Single NormalSpeed;
            public Single PickSpeed;
            public Single PlaceSpeed;
            public Single RobotAbove;
            public Single RobotAbovePick;
            public Single RobotAbovePlace;
            //public Single RobotAboveRej;
            public Single DelayPick;
            public Single DelayPlace;
            public Single Step;
            public Single CheckGrip;
            //public Single InvLock;
            public Single ToolOffX;
            public Single ToolOffY;
            public Single ToolOffZ;
            public Single ToolOffR;
            //public Single ClampAfterPick;
            public string Gripper;
            public bool GripperExist;
            //public Single Straight;
            public bool AlignMaster;
            public bool AlignCorners;
            public Single WithVision;
            public string address;
            public string port1;
            public string port2;
            public string port3;

            //public Single PartDiam;
            //public Single PartLength;
           


        }
        
        public static int PlcAddress1;
        public static int PlcAddress2;
//igor
        public static int NumOfPickCamera = 1;
        //public static bool PlaceFlipStationOffset = false;
        //public static bool PickFlipStationOffset = false;
       

        public static int[] CycleStep = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public static int[] MainCmdFini = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public static int[] MainCmdRun = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public static Boolean bStartCycle = false;
        public static bool bTsaiDllOpenned = false;
        public static Int64 InsertCount = 0;
        public static Int64 RejectedInsetCount = 0;
        public static Boolean bLoad = false;
        public static bool bPower = false;

        public static Stopwatch Stsw = new Stopwatch();//$
        public static long StswTime = 0;
        public static int StswDelay = 20;
        //public static int SetOut = -1;
        public static int SetCard = -1;
        public static Boolean ReadIOcont = false;

        public struct GnrlCmd
        {
            public const int LockDoors = 409;
            public const int Param = 410;
            public const int Power = 411;
            public const int TraficLights = 412;
            public const int MoveVel = 413;
            public const int Stop = 414;
            public const int MoveAbs = 415;
            public const int Reset = 416;
            public const int CurrPos = 417;
            public const int Status = 418;
            public const int LoadIni = 421;
            public const int ReadIn = 419;
            public const int ReadOut = 420;
           

        }
        public struct Conv1Cmd
        {
            public const int Unload = 110;
            public const int Load = 111;
            public const int ResetLift = 115;
            public const int LoadFirstTray = 116;
            public const int ChangeTray = 117;
            public const int ReleaseTray = 118;
            public const int StoreLiftDown = 119;
            public const int DefaultPos = 120;
            public const int CheckStatus = 121;
            public const int leftUp = 122;
            public const int leftDown = 123;
            public const int rightUp = 124;
            public const int rightDown = 125;
            public const int air = 126;
            public const int leftPins = 127;
            public const int rightPins = 128;
            public const int sideTray1 = 129;
            public const int sideTray2 = 130;
            public const int bottomTray = 131;
            

        }
        public struct Conv2Cmd
        {
            public const int Unload = 210;
            public const int Load = 211;
            public const int ResetLift = 215;
            public const int LoadFirstTray = 216;
            public const int ChangeTray = 217;
            public const int ReleaseTray = 218;
            public const int StoreLiftDown = 219;
            public const int DefaultPos = 220;
            public const int CheckStatus = 221;
            public const int leftUp = 222;
            public const int leftDown = 223;
            public const int rightUp = 224;
            public const int rightDown = 225;
            public const int air = 226;
            public const int leftPins = 227;
            public const int rightPins = 228;
            public const int sideTray1 = 229;
            public const int sideTray2 = 230;
            public const int bottomTray = 231;
            

        }
        public struct Device
        {
            public const int Table = 1;
            public const int Conveyor1 = 2;
            public const int Conveyor2 = 3;
            public const int Man1 = 4;
            public const int Man2 = 5;
            public const int Cams = 6;
            



        }

        public struct TableCmd
        {
            public const int Index = 10;
            public const int Home = 11;



        }
        public struct CamsCmd
        {
            public const int Power = 411;
            public const int Reset = 416;
            public const int Status = 102;
            public const int Error = 103;
            public const int MoveVel = 413;
            public const int MoveAbs = 415;
            public const int Stop = 414;
            //public const int Parms = 107;
            public const int CurrentPos = 417;
            public const int MoveRel = 412;
            public const int Lamps = 423;



        }

        public struct Manipulator3Cmd
        {
            
            public const int Parms = 200;
            public const int Home = 201;
            public const int PickAndPlace = 202;
            public const int Straight = 203;
            public const int Gripper = 204;
        }
        public struct Manipulator6Cmd
        {

            public const int Parms = 800;
            public const int Home = 801;
            public const int PickAndPlace = 802;
            public const int Straight = 803;
            public const int Gripper = 804;
        }
        public struct Manipulator5Cmd
        {

            public const int Parms = 300;
            public const int Home = 301;
            public const int PickAndPlace = 302;
            public const int Straight = 303;
            public const int Gripper = 304;
        }
        public struct StationAxis
        {
            public const int ZF_ST2 = 1;
            public const int XF_ST3 = 2;
            public const int Z_ST3 = 3;
            public const int R_ST3 = 4;
            public const int Z_ST5 = 5;
            public const int R_ST6 = 6;
            public const int Z_ST6 = 7;
            public const int XF_ST6 = 8;
            public const int ZF_ST7 = 9;
            public const int R_ST5 = 10;
            public const int All = 0;

        }

        //public struct Rot1Cmd
        //{
        //    public const int RunRotate = 210;
        //}
        //public struct Rot2Cmd
        //{
        //    public const int RunRotate = 310;
        //}
       
        public static short Speed = 0;
        
        public static int PartIndex = 0;
        public static int TrayOutIndex = 0;
        public static int BoxIndex = 0;
        public static int BoxIndex2 = 0;

        public struct ConvStation
        {

            public Single Xcoord;
            public Single Zbefore;
            public Single Ztray1;
            public Single Ztray2;
            public Single Zup;

        }
       public  static bool Host1Beasy=false;
       public  static bool Host2Beasy=false;
        //public const int St1 = 1;
        //public const int St2 = 2;
        //public const int St3 = 3;
        //public const int St4 = 4;
        //public const int St5 = 5;
        //public const int St6 = 6;
        //public const int St7 = 7;
        //public const int St8 = 8;
        public static bool FirstTime = false;
        public struct position
        {
            public Single x;
            public Single y;
            public Single z;
            public Single r;
            
        }
        public struct light
        {
            public const int FrontON = 1;
            public const int FrontOFF = 2;
            public const int HalfFrontOn = 3;
            public const int Down = 4;
            public const int DownFront = 5;
            public const int DownHalfFront = 6;
            public const int BackON = 1;
            public const int BackOFF = 2;
            public const int Same = 0;
            public const int ON = 1;
            public const int OFF = 2;


        }
        public static bool OneSnap = false;
        public static bool FirstSnapInCycle = false;
        public static bool SnapReadySt7 = false;
        public static bool NextPlaceTray = false;


    }

}
