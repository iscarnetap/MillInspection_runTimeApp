using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using ViDi2;
using SuaKITEvaluatorBatch;
using ViDi;
using BackroundWork;
//using Modbusrun;
using Inspection;
using System.Threading;



namespace RuntimeMultiGPU2
{

    #region -----------------Name space level Declarations---------------------

    #endregion


    public partial class frmMain : Form
    {
        //Author: Yoav Tamari
        //Project: PR115481 SC Inspection Unit
        //Created: 03-12-2023 
        //Description: 


        //constructor
        public frmMain()
        {
            InitializeComponent();
        }




        #region -----------------Class level Declarations---------------------
        //static List<int> GPUList = new List<int>();
        //ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);
        public struct RegionFound
        {
            public double area;
            public int width;
            public int height;
            public ViDi2.Point center;
            public double score;
            public string classColor;
            public double compactness;
            public System.Collections.ObjectModel.ReadOnlyCollection<ViDi2.IRegion> covers;
            public string className;
            public System.Collections.ObjectModel.ReadOnlyCollection<ViDi2.Point> outer;
            public double perimeter;

        }
        public struct IIMageFifo
        {
            public bool xNewImage;
            public int imageIndex;
            public string imageName;
            public ViDi2.IImage iimage;
            public string gpuName;
            public bool xEvaluationDone;
        }
        public struct TupleJobs
        {
            public List<Tuple<string, IIMageFifo, int>> jobs01;
            public List<Tuple<string, IIMageFifo, int>> jobs02;
            public int gpuId01;
            public int gpuId02;
            public Dictionary<string, ViDi2.Runtime.IStream> StreamDict;
            public bool xNoError;
        }
        public struct TupleJobsFifo
        {
            public List<Tuple<string, IIMageFifo, bool>> FifoJob01;
            public List<Tuple<string, IIMageFifo, bool>> FifoJob02;

            public int gpuId01;
            public int gpuId02;
            public Dictionary<string, ViDi2.Runtime.IStream> StreamDict;
            public bool xNoError;
        }
        public struct TupleJobsFifo01
        {
            public List<IIMageFifo> FifoJob01;
            public List<IIMageFifo> FifoJob02;

            public int gpuId01;
            public int gpuId02;
            public Dictionary<string, ViDi2.Runtime.IStream> StreamDict;
            public bool xNoError;
        }
        public struct TupleJobsFifo02
        {
            public IIMageFifo[] FifoJob01;
            public IIMageFifo[] FifoJob02;

            public int gpuId01;
            public int gpuId02;
            public Dictionary<string, ViDi2.Runtime.IStream> StreamDict;
            public bool xNoError;
            public CurrentState state;
        }
        public struct Models
        {
            public string path;
            public string model1FileName;
            public string model2FileName;
        }
        public struct LoadingQueue
        {
            public bool[] loadPic;
        }
        public struct MultiResuls
        {
            public List<Dictionary<string, IMarking>> markings;
            public string[] imagesNames;
        }
        #region--------Class level Declarations Backround tasks--------------
        //public BackgroundWorker BackRoundTasks = new BackgroundWorker();

        BKW_Def bkw = null;

       // P47859_IO ProjMain = new P47859_IO();

        #endregion

        private string version = "21-01-2024";
        private IIMageFifo[] IImageFifo = new IIMageFifo[20];
        private TupleJobs gtupleJobs = new TupleJobs();
        private TupleJobsFifo gtupleJobsFifo = new TupleJobsFifo();
        private TupleJobsFifo01 gtupleJobsFifo01= new TupleJobsFifo01();
        private TupleJobsFifo02 gtupleJobsFifo02 = new TupleJobsFifo02();
        private ViDi2.Runtime.IControl control;
        private List<Tuple<string, string, string>> WorkspaceFiles;
        private bool xInitDone = false;
        private int StationsToMark = 127;
        private int iNumEvaluationCycles = 0;
        private bool xLoading = false;
        private RectangleF roiFromVision = new RectangleF();
        private Models gmodels = new Models();
        private int iLastLoadingPicIndex = -1;     // last picturebox loaded
        private LoadingQueue loadingQueue = new LoadingQueue();
        private int CstationNumber = 0;
        private bool xSingleGpu = false;
        private int iLastLoadingPicIndexTrue = 0;     // last picturebox loaded
        

        #endregion

        #region-------------------------Methods -----------------------------       
        private async Task<bool> DisplayMarkingListFIN(MultiResuls multiResuls)
        {

            List<Dictionary<string, IMarking>> lstIMarking = multiResuls.markings;

            if (multiResuls.markings.Count > 0)
            {
                //invy.ClearListBox(lstMarking);

                invy.ListBoxaddItem(lstMarking, "2xJobs Number Of Images Processed: " + multiResuls.markings.Count.ToString());
            }

            int jobIndex = 0;
            int imgIndex = 0;
            foreach (Dictionary<string, IMarking> item01 in lstIMarking)
            {
                jobIndex++;

                Dictionary<string, IMarking> views01 = item01;   // lstIMarking01[0];

                IMarking mm = views01["red_HDM_20M_5472x3648"];

                ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];

                //string imgName = arrayOfViDi2IIamge[imgIndex].imageName;
                string imgName = multiResuls.imagesNames[imgIndex];

                if (redview.Regions.Count > 0)
                {
                    ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                    RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
                    double[] score = new double[redview.Regions.Count];
                    int index = 0;

                    //lstMarking.Items.Add("Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());
                    invy.ListBoxaddItem(lstMarking, "Job: " + jobIndex.ToString() + " Image " + imgIndex.ToString() + " Image Name " + imgName + " regions found: " + redview.Regions.Count.ToString());

                    foreach (ViDi2.IRegion item in redview.Regions)
                    {
                        regionFound[index].area = item.Area;
                        regionFound[index].width = item.Width;
                        regionFound[index].height = item.Height;
                        regionFound[index].center = item.Center;
                        regionFound[index].score = item.Score;
                        regionFound[index].className = "not possable to know in this application";  // item.Name; region name
                        regionFound[index].classColor = item.Color;
                        regionFound[index].compactness = item.Compactness;
                        regionFound[index].covers = item.Covers;
                        regionFound[index].outer = item.Outer;
                        regionFound[index].perimeter = item.Perimeter;

                        //lstMarking.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                        invy.ListBoxaddItem(lstMarking, (index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                        //regions[index] = item; //testing
                        score[index] = item.Score; //testing

                        index++;



                    }

                    Array.Sort(score);

                    imgIndex++;
                }
                else
                {
                    invy.ListBoxaddItem(lstMarking, "Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());
                    imgIndex++;
                }
            }


            return true;
        }
        private async Task<List<Dictionary<string, IMarking>>> EvaluateJob01(IIMageFifo[] jobs01, int gpuId01,Dictionary<string, ViDi2.Runtime.IStream> StreamDict)
        {
            //------------will work on one job, one model--------------------
            
                List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

            if (jobs01.GetLength(0) == 0) { goto exitprocedure; } 
            
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();                
                string wsName;            
                int jobIndex = 0;

                foreach (var job in jobs01)
                {
                    wsName = job.gpuName;                                  

                    sw.Restart();
                    //int ni = StreamDict[wsName].FrameWindow;
                    if (job.xNewImage && !job.xEvaluationDone)
                    {
                        using (ISample sample = StreamDict[wsName].CreateSample(job.iimage))   //img1))
                        {                                                        
                            sample.Process(null, new List<int>() { gpuId01});
                            //ICollection<Dictionary<string, IMarking>> frames = sample.Frames;
                            //string s = sample.Markings["red_HDM_20M_5472x3648"].ImageInfo.Filename; //name is empty needs to enter name earlier
                            lstIMarking.Add(sample.Markings); //Yoav 29-112023
                             
                        }

                                               
                    }

                    sw.Stop();

                    jobs01[jobIndex].xNewImage = false;
                    jobIndex++;
                }

           exitprocedure:;

            return lstIMarking;            
        }
        private async Task<List<Dictionary<string, IMarking>>> EvaluateJob02(IIMageFifo[] jobs02, int gpuId02, Dictionary<string, ViDi2.Runtime.IStream> StreamDict)
        {
            //------------will work on one job, one model--------------------            
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

            if (jobs02.GetLength(0) == 0) { goto exitprocedure; }


            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();            
            string wsName;            
            int jobIndex = 0;

            foreach (var job in jobs02)
            {
                wsName = job.gpuName;

                sw.Restart();

                if (job.xNewImage && !job.xEvaluationDone)
                {
                    using (ISample sample = StreamDict[wsName].CreateSample(job.iimage))   //img1))
                    {
                        //sample.AddImage(img1);
                        // process all tools on stream with specific gpu(gpuId)
                        sample.Process(null, new List<int>() { gpuId02 });
                        lstIMarking.Add(sample.Markings); //Yoav 29-112023
                    }

                    
                }

                sw.Stop();
                jobs02[jobIndex].xNewImage = false;
                jobIndex++;
            }

         exitprocedure:;

            return lstIMarking;           
        }

        private bool loadLog(string msg)
        {
            string newval = msg;
            invy.ListBoxaddItem(lstGPU2log, newval);
            invy.ListBoxPerformRefresh(lstGPU2log);

            return true;
        }
        private bool OriginalAsGPU2()
        {

            Application.DoEvents();


            bool ret = true;


            invy.ClearListBox(lstGPU2log);

            #region MaximizeThroughput
            //_250723_093900


            int gpu0Add = 0;
            int gpu1Add = 0;
            // (1) 
            //
            // To maximize throughput all tools will use only one GPU. We can then use a hardware concurrency
            // equal to the number of GPUs.
            //{
            //System.Console.WriteLine("Example maximizing throughput");

            loadLog("Example maximizing throughput");

            // List<int> GPUList = new List<int>();
            // We could instead specify which gpu to use by initializing with :

            List<int> GPUList;
            if (gpu0Add == 0 && gpu1Add == 1)
                GPUList = new List<int>() { 0, 1 };
            else
                GPUList = new List<int>();

            // to use only first and second GPUs

            // Initialize a control
            ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);

            //using (ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList))
            //{
            loadLog("control  Initialized");
            // Initialilizes the Compute devices
            // Parameters : - GPUMode.SingleDevicePerTool each tool will use a single GPU -> Maximizing throughput
            //              - new GPUList : automatically resolve all available gpus if empty

            control.InitializeComputeDevices(GpuMode.SingleDevicePerTool, GPUList);

            loadLog("Compute Devices  Initialized");

            var computeDevices = control.ComputeDevices;

            // the example will run with fewer than 2 GPUs, but the results might not be meaningful
            if (computeDevices.Count < 2)
            {
                //Console.WriteLine("Warning ! Example needs at least two GPUs to be meaningfull");                        
                loadLog("Warning ! Example needs at least two GPUs to be meaningfull");
            }

            //Console.WriteLine("Available computing devices :");                   
            loadLog("Available computing devices :");

            foreach (var computeDevice in control.ComputeDevices)  //computeDevices)
            {
                //Console.WriteLine($"{computeDevice.Index} : Card {computeDevice.Name}");                        
                loadLog($"\t\t\t{computeDevice.Index} : Card {computeDevice.Name}");
            }

            string runtimeModelsPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\Resources\runtime\Iscar\";

            var WorkspaceFiles = new List<Tuple<string, string, string>>
                    {
                        // if you want to process HDM tool on a specific GPU, additional parameter needed when opening the workspace
                        // STREAM_NAME/TOOL_NAME/<GPU_INDEX> => default/Classify/0                       
                        //new Tuple<string, string, string>("HDM-Red-0", "..\\..\\..\\..\\resources\\runtime\\Red High-detail Tool.vrws", "default/Analyze/0"),
                        //new Tuple<string, string, string>("HDM-Red-1", "..\\..\\..\\..\\resources\\runtime\\Red High-detail Tool.vrws", "default/Analyze/1"),            

                        new Tuple<string, string, string>("HDM-Red-0",  runtimeModelsPath + "\\Proj_403_23042023_152921.vrws", "default/red_HDM_20M_5472x3648/" + gpu0Add.ToString()),
                        new Tuple<string, string, string>("HDM-Red-1",  runtimeModelsPath + "\\Proj_403_23042023_152921.vrws", "default/red_HDM_20M_5472x3648/" + gpu1Add.ToString())

                        //new Tuple<string, string, string>("HDM-Red-0", "..\\..\\..\\..\\resources\\runtime\\Iscar\\proj_001_050723_093500_17072023_110032.vrws", "default/Analyze/0"),
                        //new Tuple<string, string, string>("HDM-Red-1", "..\\..\\..\\..\\resources\\runtime\\Iscar\\proj_001_050723_093500_17072023_110032.vrws", "default/Analyze/1")
                    };

            //Console.WriteLine($"\n----- Load Runtime Workspaces -----");                    
            loadLog($"\n----- Load Runtime Workspaces -----");

            // opens a runtime workspace from file
            //string WorkspaceFile = "..\\..\\..\\..\\resources\\runtime\\Textile.vrws";

            // Instead of using mutex, you can consider the ConcurrentDictionary.
            var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();
            int toolNum = 0;
            foreach (var wsInfo in WorkspaceFiles)
            {
                if (!File.Exists(wsInfo.Item2))
                {
                    // if you got here then it's likely that the resources were not extracted in the path
                    //Console.WriteLine($"Fatal : {wsInfo.Item2} does not exist");
                    //Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");

                    loadLog($"Fatal : {wsInfo.Item2} does not exist");
                    loadLog($"Current Directory = { Directory.GetCurrentDirectory()}");

                    return false;
                }
                string wsName = wsInfo.Item1;
                string wsPath = wsInfo.Item2;
                string gpuHdm = wsInfo.Item3;
                if (string.IsNullOrEmpty(gpuHdm))
                    StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath).Streams["default"]);
                else
                    // needs additional parameter 'gpuHdm' for allocating dedicated gpu on HDM tool
                    StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath, gpuHdm).Streams["default"]);

                //Console.WriteLine(wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath));
                loadLog(wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath));


                toolNum++;
            }

            //if (!File.Exists(WorkspaceFile))
            //{
            //    // if you got here then it's likely that the resources were not extracted in the path
            //    Console.WriteLine($"{WorkspaceFile} does not exist");
            //    Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");
            //    return;
            //}

            string imagesPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";
            // images for process
            string RedImagePath = imagesPath + "\\resources\\images\\Iscar\\220_06.35_041_1.jpg";
            string RedImagePath1 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_031_1.jpg";
            string RedImagePath2 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_031_2.jpg";
            string RedImagePath3 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_1.jpg";
            string RedImagePath4 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_2.jpg";
            string RedImagePath5 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_3.jpg";
            string RedImagePath6 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_1.jpg";
            string RedImagePath7 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_2.jpg";
            string RedImagePath8 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_3.jpg";
            string RedImagePath9 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_035_1.jpg";
            string RedImagePath10 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_035_2.jpg";
            string RedImagePath11 = imagesPath + "\\resources\\images\\Iscar\\000030.png";

            if (!File.Exists(RedImagePath))
            {
                // if you got here then it's likely that the resources were not extracted in the path
                //Console.WriteLine($"{RedImagePath} does not exist");                       
                //Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");

                loadLog($"{RedImagePath} does not exist");
                loadLog($"Current Directory = { Directory.GetCurrentDirectory()}");

                return false;
            }
            else
            {
                //Console.WriteLine($"\n\rRedImageName   : {Path.GetFullPath(RedImagePath)}");
                loadLog($"\n\rRedImageName   : {Path.GetFullPath(RedImagePath)}");

                //ViDi2.IImage img;
                ViDi2.FormsImage img = new ViDi2.FormsImage(RedImagePath);

                //Console.WriteLine("\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString());                       
                loadLog("\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString());
            }


            // batch job lists for test
            // In order to demonstrate two GPUs performing simultaneously on two threads using different workspaces, 
            // the workspaces are frequently replaced while repeating small count of images.
            // Tuple<WorkspaceName, ImagePath, IterationCount>
            var Thread0Job = new List<Tuple<string, string, int>>
                    {
                       
                        //new Tuple<string, string, int>("HDM-Red-0", RedImagePath, 128),                       
                        //new Tuple<string, string, int>("HDM-Red-0", RedImagePath1, 256),                        
                        //new Tuple<string, string, int>("HDM-Red-0", RedImagePath2, 512),                        
                        //new Tuple<string, string, int>("HDM-Red-0", RedImagePath3, 128),                       
                        //new Tuple<string, string, int>("HDM-Red-0", RedImagePath4, 256),                        
                        //new Tuple<string, string, int>("HDM-Red-0", RedImagePath5, 512)

                        new Tuple<string, string, int>("HDM-Red-0", RedImagePath, 10),
                        new Tuple<string, string, int>("HDM-Red-0", RedImagePath1,10),
                        new Tuple<string, string, int>("HDM-Red-0", RedImagePath2,10),
                        new Tuple<string, string, int>("HDM-Red-0", RedImagePath3,10),
                        new Tuple<string, string, int>("HDM-Red-0", RedImagePath4,10),
                        new Tuple<string, string, int>("HDM-Red-0", RedImagePath5,10)

                    };
            var Thread1Job = new List<Tuple<string, string, int>>
                    {                        
                        //new Tuple<string, string, int>("HDM-Red-1", RedImagePath6, 128),                        
                        //new Tuple<string, string, int>("HDM-Red-1", RedImagePath7, 256),                        
                        //new Tuple<string, string, int>("HDM-Red-1", RedImagePath8, 512),                        
                        //new Tuple<string, string, int>("HDM-Red-1", RedImagePath9, 128),                        
                        //new Tuple<string, string, int>("HDM-Red-1", RedImagePath10, 256)


                        new Tuple<string, string, int>("HDM-Red-1", RedImagePath6, 10),
                        new Tuple<string, string, int>("HDM-Red-1", RedImagePath7, 10),
                        new Tuple<string, string, int>("HDM-Red-1", RedImagePath8, 10),
                        new Tuple<string, string, int>("HDM-Red-1", RedImagePath9, 10),
                        new Tuple<string, string, int>("HDM-Red-1", RedImagePath10, 10)
                    };

            //add roi
            bool xroiActive = false;
            if (xroiActive)
            {
                //----------------------------------tool-0-------------------------------------
                IToolList<ViDi2.Runtime.ITool> toolslst = StreamDict["HDM-Red-0"].Tools;
                IRedTool hdRedTool = (IRedTool)toolslst["red_HDM_20M_5472x3648"];
                ViDi2.IManualRegionOfInterest redROI = (ViDi2.IManualRegionOfInterest)hdRedTool.RegionOfInterest;
                redROI.Parameters.Units = ViDi2.UnitsMode.Pixel;
                redROI.Parameters.Offset = new ViDi2.Point(2009, 1733);
                redROI.Parameters.Size = new ViDi2.Size(1092, 508);
                //Console.WriteLine("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());

                loadLog("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());

                //----------------------------------tool-1-------------------------------------
                IToolList<ViDi2.Runtime.ITool> toolslst1 = StreamDict["HDM-Red-1"].Tools;
                IRedTool hdRedTool1 = (IRedTool)toolslst1["red_HDM_20M_5472x3648"];
                ViDi2.IManualRegionOfInterest redROI1 = (ViDi2.IManualRegionOfInterest)hdRedTool1.RegionOfInterest;
                redROI1.Parameters.Units = ViDi2.UnitsMode.Pixel;
                redROI1.Parameters.Offset = new ViDi2.Point(2009, 1733);
                redROI1.Parameters.Size = new ViDi2.Size(1092, 508);
                //Console.WriteLine("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());

                loadLog("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());

            }
            else
            {
                //Console.WriteLine("\n\rtool_1, tool_2, roi not active");
                loadLog("\n\rtool_1, tool_2, roi not active");
            }

            //Yoav 29-11-2023
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

            //StreamDict[wsName]
            Func<List<Tuple<string, string, int>>, int, int> ThreadAction = (jobs, gpuId) =>
            {
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                string ls, rs;
                string wsName, imgPath;
                int iterCount = 0;
                foreach (var job in jobs)
                {
                    wsName = job.Item1;
                    imgPath = job.Item2;
                    iterCount = job.Item3;

                    ls = (gpuId == 0) ? ("ENTER : " + wsName) : "...";
                    rs = (gpuId == 0) ? "..." : ("ENTER : " + wsName);
                        //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");                           
                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");

                    sw.Restart();
                    using (var img1 = new ViDi2.Local.LibraryImage(imgPath))
                        for (var iteration = 0; iteration < iterCount; ++iteration)
                        {
                            using (ISample sample = StreamDict[wsName].CreateSample(img1))
                            {
                                    // process all tools on stream with specific gpu(gpuId)
                                    sample.Process(null, new List<int>() { gpuId });
                                lstIMarking.Add(sample.Markings); //Yoav 29-112023
                                }
                        }
                    sw.Stop();

                    bool xNoTeting = true;
                    if (!xNoTeting)
                    {
                        Dictionary<string, IMarking> views01 = lstIMarking[0];

                        IMarking mm = views01["red_HDM_20M_5472x3648"];
                        ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];
                        ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                            //IReadOnlyCollection<IView> rm = mm.Views; //OK but regions are not expose 
                            //IView rm = mm.Views[0];

                            //var views02 = lstIMarking[1];
                            //var vv = views02["red_HDM_20M_5472x3648"];

                            //ViDi2.IRedView redview = (ViDi2.IRedView)vv.Views[0]; //must be IReadView to get the regions

                            ViDi2.IRegion[] regions = new IRegion[redview.Regions.Count];
                        double[] score = new double[redview.Regions.Count];
                        int index = 0;
                        foreach (ViDi2.IRegion item in redview.Regions)
                        {
                                //regionFound[index].area = item.Area;
                                //regionFound[index].width = item.Width;
                                //regionFound[index].height = item.Height;
                                //regionFound[index].center = item.Center;
                                //regionFound[index].score = item.Score;
                                //regionFound[index].className = cn;  // item.Name; region name
                                //regionFound[index].classColor = item.Color;
                                //regionFound[index].compactness = item.Compactness;
                                //regionFound[index].covers = item.Covers;
                                //regionFound[index].outer = item.Outer;
                                //regionFound[index].perimeter = item.Perimeter;

                                //lstGreenToolResults.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());

                                regions[index] = item; //testing
                                score[index] = item.Score; //testing

                                index++;



                        }

                        Array.Sort(score);
                    }

                    ls = (gpuId == 0) ? ("EXIT  : " + wsName) : "...";
                    rs = (gpuId == 0) ? "..." : ("EXIT  : " + wsName);
                        //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                }

                    //Console.WriteLine($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");                        
                    loadLog($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");

                return gpuId;
            };

            System.Diagnostics.Stopwatch globalSw = new System.Diagnostics.Stopwatch();

            //Console.WriteLine($"\n----- Threads -----");                    
            loadLog($"\n----- Threads -----");

            globalSw.Start();
            // Create two threads and put corresponding JobList and gpu index.


            var threads = new List<Task>();
            threads.Add(Task.Factory.StartNew(() => ThreadAction(Thread0Job, gpu0Add)));
            threads.Add(Task.Factory.StartNew(() => ThreadAction(Thread1Job, gpu1Add)));
            // wait for all tasks to finish
            Task.WaitAll(threads.ToArray());
            globalSw.Stop();
            //Console.WriteLine($"\n----- End, Total {globalSw.ElapsedMilliseconds} ms -----");                    
            loadLog($"\n----- End, Total {globalSw.ElapsedMilliseconds} ms -----");

            // close workspaces, //can't close workspace, causing problems
            //foreach (var wsInfo in WorkspaceFiles)
            //    control.Workspaces.Remove(wsInfo.Item1);



            //if (control != null)
            //    control.Dispose();


            //Console.WriteLine("Press Any Key To Continue");                    
            //bool ret = loadLog("Press Any Key To Continue");


            //Console.ReadKey();                    


            //}

            try
            {
                // System.Threading.Thread.Sleep(2000);
                //System.Windows.Forms.MessageBox.Show("Control OK: " + control.ToString()); //don't disable or remove this message box
            }
            catch (ViDi2.Exception e)
            {
                string message = e.Message;
            }

            if (control != null)
            {
                control.Dispose();
                control = null;
            }


            #endregion            

            return ret;
        }
        private bool OriginalAsGPU2NoMultiThreading()
        {

            Application.DoEvents();


            bool ret = true;


            invy.ClearListBox(lstGPU2log);

            #region MaximizeThroughput
            //_250723_093900


            int gpu0Add = (int)nuUDJob0gpuIndex.Value;
            int gpu1Add = (int)nuUDJob1gpuIndex.Value;
            // (1) 
            //
            // To maximize throughput all tools will use only one GPU. We can then use a hardware concurrency
            // equal to the number of GPUs.
            //{
            //System.Console.WriteLine("Example maximizing throughput");
            loadLog("Example maximizing throughput With Image Loading");

            // List<int> GPUList = new List<int>();
            // We could instead specify which gpu to use by initializing with :

            List<int> GPUList;
            if (gpu0Add == 0 && gpu1Add == 1)
                GPUList = new List<int>() { 0, 1 };
            else
                GPUList = new List<int>();

            // to use only first and second GPUs

            // Initialize a control
            ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);

            //using (ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList))
            //{
            loadLog("control  Initialized");
            // Initialilizes the Compute devices
            // Parameters : - GPUMode.SingleDevicePerTool each tool will use a single GPU -> Maximizing throughput
            //              - new GPUList : automatically resolve all available gpus if empty

            control.InitializeComputeDevices(GpuMode.SingleDevicePerTool, GPUList);

            loadLog("Compute Devices  Initialized");

            var computeDevices = control.ComputeDevices;

            // the example will run with fewer than 2 GPUs, but the results might not be meaningful
            if (computeDevices.Count < 2)
            {
                //Console.WriteLine("Warning ! Example needs at least two GPUs to be meaningfull");                        
                loadLog("Warning ! Example needs at least two GPUs to be meaningfull");
            }

            //Console.WriteLine("Available computing devices :");                   
            loadLog("Available computing devices :");

            foreach (var computeDevice in control.ComputeDevices)  //computeDevices)
            {
                //Console.WriteLine($"{computeDevice.Index} : Card {computeDevice.Name}");                        
                loadLog($"\t\t\t{computeDevice.Index} : Card {computeDevice.Name}");
            }

            string runtimeModelsPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\Resources\runtime\Iscar\";

            var WorkspaceFiles = new List<Tuple<string, string, string>>
                    {
                        // if you want to process HDM tool on a specific GPU, additional parameter needed when opening the workspace
                        // STREAM_NAME/TOOL_NAME/<GPU_INDEX> => default/Classify/0                       
                        //new Tuple<string, string, string>("HDM-Red-0", "..\\..\\..\\..\\resources\\runtime\\Red High-detail Tool.vrws", "default/Analyze/0"),
                        //new Tuple<string, string, string>("HDM-Red-1", "..\\..\\..\\..\\resources\\runtime\\Red High-detail Tool.vrws", "default/Analyze/1"),            

                        new Tuple<string, string, string>("HDM-Red-0",  runtimeModelsPath + "\\Proj_403_23042023_152921.vrws", "default/red_HDM_20M_5472x3648/" + gpu0Add.ToString()),
                        new Tuple<string, string, string>("HDM-Red-1",  runtimeModelsPath + "\\Proj_403_23042023_152921.vrws", "default/red_HDM_20M_5472x3648/" + gpu1Add.ToString())

                        //new Tuple<string, string, string>("HDM-Red-0", "..\\..\\..\\..\\resources\\runtime\\Iscar\\proj_001_050723_093500_17072023_110032.vrws", "default/Analyze/0"),
                        //new Tuple<string, string, string>("HDM-Red-1", "..\\..\\..\\..\\resources\\runtime\\Iscar\\proj_001_050723_093500_17072023_110032.vrws", "default/Analyze/1")
                    };

            //Console.WriteLine($"\n----- Load Runtime Workspaces -----");                    
            loadLog($"\n----- Load Runtime Workspaces -----");

            // opens a runtime workspace from file
            //string WorkspaceFile = "..\\..\\..\\..\\resources\\runtime\\Textile.vrws";

            // Instead of using mutex, you can consider the ConcurrentDictionary.
            var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();
            int toolNum = 0;
            foreach (var wsInfo in WorkspaceFiles)
            {
                if (!File.Exists(wsInfo.Item2))
                {
                    // if you got here then it's likely that the resources were not extracted in the path
                    //Console.WriteLine($"Fatal : {wsInfo.Item2} does not exist");
                    //Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");

                    loadLog($"Fatal : {wsInfo.Item2} does not exist");
                    loadLog($"Current Directory = { Directory.GetCurrentDirectory()}");

                    return false;
                }
                string wsName = wsInfo.Item1;
                string wsPath = wsInfo.Item2;
                string gpuHdm = wsInfo.Item3;
                if (string.IsNullOrEmpty(gpuHdm))
                    StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath).Streams["default"]);
                else
                    // needs additional parameter 'gpuHdm' for allocating dedicated gpu on HDM tool
                    StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath, gpuHdm).Streams["default"]);

                //Console.WriteLine(wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath));
                loadLog(wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath));


                toolNum++;
            }

            //if (!File.Exists(WorkspaceFile))
            //{
            //    // if you got here then it's likely that the resources were not extracted in the path
            //    Console.WriteLine($"{WorkspaceFile} does not exist");
            //    Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");
            //    return;
            //}

            string imagesPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";
            // images for process            
            string RedImagePath = imagesPath + "\\resources\\images\\Iscar\\220_06.35_041_1.jpg";
            string RedImagePath1 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_031_1.jpg";
            string RedImagePath2 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_031_2.jpg";
            string RedImagePath3 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_1.jpg";
            string RedImagePath4 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_2.jpg";
            string RedImagePath5 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_3.jpg";
            string RedImagePath6 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_1.jpg";
            string RedImagePath7 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_2.jpg";
            string RedImagePath8 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_3.jpg";
            string RedImagePath9 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_035_1.jpg";
            string RedImagePath10 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_035_2.jpg";
            string RedImagePath11 = imagesPath + "\\resources\\images\\Iscar\\000030.png";

            if (!File.Exists(RedImagePath))
            {
                // if you got here then it's likely that the resources were not extracted in the path
                //Console.WriteLine($"{RedImagePath} does not exist");                       
                //Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");

                loadLog($"{RedImagePath} does not exist");
                loadLog($"Current Directory = { Directory.GetCurrentDirectory()}");

                return false;
            }
            else
            {
                //Console.WriteLine($"\n\rRedImageName   : {Path.GetFullPath(RedImagePath)}");
                loadLog($"\n\rRedImageName   : {Path.GetFullPath(RedImagePath)}");

                //ViDi2.IImage img;
                ViDi2.FormsImage img = new ViDi2.FormsImage(RedImagePath);

                //Console.WriteLine("\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString());                       
                loadLog("\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString());
            }


            int batchSize = (int)nuUDBatchSize.Value;
            // batch job lists for test
            // In order to demonstrate two GPUs performing simultaneously on two threads using different workspaces, 
            // the workspaces are frequently replaced while repeating small count of images.
            // Tuple<WorkspaceName, ImagePath, IterationCount>
            var Thread0Job = new List<Tuple<string, string, int>>();

            if (nuUDSizejob1.Value > 0)
                Thread0Job.Add(new Tuple<string, string, int>("HDM-Red-0", RedImagePath, batchSize));
            if (nuUDSizejob1.Value > 1)
                Thread0Job.Add(new Tuple<string, string, int>("HDM-Red-0", RedImagePath1, batchSize));
            if (nuUDSizejob1.Value > 2)
                Thread0Job.Add(new Tuple<string, string, int>("HDM-Red-0", RedImagePath2, batchSize));
            if (nuUDSizejob1.Value > 3)
                Thread0Job.Add(new Tuple<string, string, int>("HDM-Red-0", RedImagePath3, batchSize));
            if (nuUDSizejob1.Value > 4)
                Thread0Job.Add(new Tuple<string, string, int>("HDM-Red-0", RedImagePath4, batchSize));
            if (nuUDSizejob1.Value > 5)
                Thread0Job.Add(new Tuple<string, string, int>("HDM-Red-0", RedImagePath5, batchSize));

            var Thread1Job = new List<Tuple<string, string, int>>();

            if (nuUDSizejob2.Value > 0)
                Thread1Job.Add(new Tuple<string, string, int>("HDM-Red-1", RedImagePath6, batchSize));
            if (nuUDSizejob2.Value > 1)
                Thread1Job.Add(new Tuple<string, string, int>("HDM-Red-1", RedImagePath7, batchSize));
            if (nuUDSizejob2.Value > 2)
                Thread1Job.Add(new Tuple<string, string, int>("HDM-Red-1", RedImagePath8, batchSize));
            if (nuUDSizejob2.Value > 3)
                Thread1Job.Add(new Tuple<string, string, int>("HDM-Red-1", RedImagePath9, batchSize));
            if (nuUDSizejob2.Value > 4)
                Thread1Job.Add(new Tuple<string, string, int>("HDM-Red-1", RedImagePath10, batchSize));



            //add roi
            bool xroiActive = false;
            if (xroiActive)
            {
                //----------------------------------tool-0-------------------------------------
                IToolList<ViDi2.Runtime.ITool> toolslst = StreamDict["HDM-Red-0"].Tools;
                IRedTool hdRedTool = (IRedTool)toolslst["red_HDM_20M_5472x3648"];
                ViDi2.IManualRegionOfInterest redROI = (ViDi2.IManualRegionOfInterest)hdRedTool.RegionOfInterest;
                redROI.Parameters.Units = ViDi2.UnitsMode.Pixel;
                redROI.Parameters.Offset = new ViDi2.Point(2009, 1733);
                redROI.Parameters.Size = new ViDi2.Size(1092, 508);
                //Console.WriteLine("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());

                loadLog("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());

                //----------------------------------tool-1-------------------------------------
                IToolList<ViDi2.Runtime.ITool> toolslst1 = StreamDict["HDM-Red-1"].Tools;
                IRedTool hdRedTool1 = (IRedTool)toolslst1["red_HDM_20M_5472x3648"];
                ViDi2.IManualRegionOfInterest redROI1 = (ViDi2.IManualRegionOfInterest)hdRedTool1.RegionOfInterest;
                redROI1.Parameters.Units = ViDi2.UnitsMode.Pixel;
                redROI1.Parameters.Offset = new ViDi2.Point(2009, 1733);
                redROI1.Parameters.Size = new ViDi2.Size(1092, 508);
                //Console.WriteLine("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());

                loadLog("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());

            }
            else
            {
                //Console.WriteLine("\n\rtool_1, tool_2, roi not active");
                loadLog("\n\rtool_1, tool_2, roi not active");
            }

            //Yoav 29-11-2023
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

            //StreamDict[wsName]
            bool xNotUsed = true;
            if (!xNotUsed)
            {
                Func<List<Tuple<string, string, int>>, int, int> ThreadAction = (jobs, gpuId) =>
                {
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    string ls, rs;
                    string wsName, imgPath;
                    int iterCount = 0;
                    foreach (var job in jobs)
                    {
                        wsName = job.Item1;
                        imgPath = job.Item2;
                        iterCount = job.Item3;

                        ls = (gpuId == 0) ? ("ENTER : " + wsName) : "...";
                        rs = (gpuId == 0) ? "..." : ("ENTER : " + wsName);
                        //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");                           
                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");

                        sw.Restart();
                        using (var img1 = new ViDi2.Local.LibraryImage(imgPath))
                            for (var iteration = 0; iteration < iterCount; ++iteration)
                            {
                                using (ISample sample = StreamDict[wsName].CreateSample(img1))
                                {
                                    // process all tools on stream with specific gpu(gpuId)
                                    sample.Process(null, new List<int>() { gpuId });
                                    lstIMarking.Add(sample.Markings); //Yoav 29-112023
                                }
                            }
                        sw.Stop();

                        bool xNoTesing = true;
                        if (!xNoTesing)
                        {
                            Dictionary<string, IMarking> views01 = lstIMarking[0];

                            IMarking mm = views01["red_HDM_20M_5472x3648"];
                            ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];
                            ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                            //IReadOnlyCollection<IView> rm = mm.Views; //OK but regions are not expose 
                            //IView rm = mm.Views[0];

                            //var views02 = lstIMarking[1];
                            //var vv = views02["red_HDM_20M_5472x3648"];

                            //ViDi2.IRedView redview = (ViDi2.IRedView)vv.Views[0]; //must be IReadView to get the regions

                            RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
                            double[] score = new double[redview.Regions.Count];
                            int index = 0;
                            foreach (ViDi2.IRegion item in redview.Regions)
                            {
                                regionFound[index].area = item.Area;
                                regionFound[index].width = item.Width;
                                regionFound[index].height = item.Height;
                                regionFound[index].center = item.Center;
                                regionFound[index].score = item.Score;
                                regionFound[index].className = "not possable to know in this application";  // cn;  // item.Name; region name
                                regionFound[index].classColor = item.Color;
                                regionFound[index].compactness = item.Compactness;
                                regionFound[index].covers = item.Covers;
                                regionFound[index].outer = item.Outer;
                                regionFound[index].perimeter = item.Perimeter;

                                //this.lstGPU2log.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                                invy.ListBoxaddItem(lstGPU2log, (index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                                
                                //regionFound[index] = item; //testing
                                score[index] = item.Score; //testing

                                index++;



                            }

                            Array.Sort(score);
                        }

                        ls = (gpuId == 0) ? ("EXIT  : " + wsName) : "...";
                        rs = (gpuId == 0) ? "..." : ("EXIT  : " + wsName);
                        //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                    }

                    //Console.WriteLine($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");                        
                    loadLog($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");

                    return gpuId;
                };

                System.Diagnostics.Stopwatch globalSw = new System.Diagnostics.Stopwatch();

                //Console.WriteLine($"\n----- Threads -----");                    
                loadLog($"\n----- Threads -----");

                globalSw.Start();
                // Create two threads and put corresponding JobList and gpu index.


                var threads = new List<Task>();
                threads.Add(Task.Factory.StartNew(() => ThreadAction(Thread0Job, gpu0Add)));
                threads.Add(Task.Factory.StartNew(() => ThreadAction(Thread1Job, gpu1Add)));
                // wait for all tasks to finish
                Task.WaitAll(threads.ToArray());
                globalSw.Stop();
                //Console.WriteLine($"\n----- End, Total {globalSw.ElapsedMilliseconds} ms -----");                    
                loadLog($"\n----- End, Total {globalSw.ElapsedMilliseconds} ms -----");
            }

            DateTime nowS = DateTime.Now;

            string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", nowS);
            loadLog(timenow14 + ": Start GPU2 Test...");

            List<Dictionary<string, IMarking>> lstIMarking01 = ThreadAction01(Thread0Job, gpu0Add, Thread1Job, gpu1Add, StreamDict);

            DateTime nowE = DateTime.Now;
            timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", nowE);
            loadLog(timenow14 + ": End GPU2 Test");
            //int iMinutes = nowE.Minute - nowS.Minute;
            //int iSeconds = nowE.Second - nowS.Second;

            //if (iSeconds < 0) { iSeconds = 60 + iSeconds; }
            //loadLog("Test Time: " + iMinutes.ToString() + ":"  + iSeconds.ToString() + ", Minutes:Seconds");

            int hours = (nowE - nowS).Hours;
            int minutes = (nowE - nowS).Minutes;
            int seconds = (nowE - nowS).Seconds;
            loadLog("Test Time: " + minutes.ToString() + ":" + seconds.ToString() + ", Minutes:Seconds");

            loadLog("");

            loadLog("Display Results " + lstIMarking01.Count.ToString() + " Images:");

            bool xNoTeting = false;
            if (!xNoTeting)
            {
                int imgIndex = 0;
                foreach (Dictionary<string, IMarking> item01 in lstIMarking01)
                {
                    Dictionary<string, IMarking> views01 = item01;   // lstIMarking01[0];

                    IMarking mm = views01["red_HDM_20M_5472x3648"];

                    ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];
                    ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions



                    RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
                    double[] score = new double[redview.Regions.Count];
                    int index = 0;

                    //lstGPU2log.Items.Add("Image " + imgIndex.ToString() + " regions found");
                    invy.ListBoxaddItem(lstGPU2log, "Image " + imgIndex.ToString() + " regions found");

                    foreach (ViDi2.IRegion item in redview.Regions)
                    {
                        regionFound[index].area = item.Area;
                        regionFound[index].width = item.Width;
                        regionFound[index].height = item.Height;
                        regionFound[index].center = item.Center;
                        regionFound[index].score = item.Score;
                        regionFound[index].className = "not possable to know in this application";  // item.Name; region name
                        regionFound[index].classColor = item.Color;
                        regionFound[index].compactness = item.Compactness;
                        regionFound[index].covers = item.Covers;
                        regionFound[index].outer = item.Outer;
                        regionFound[index].perimeter = item.Perimeter;

                        //lstGPU2log.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                        invy.ListBoxaddItem(lstGPU2log, (index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());

                        //regions[index] = item; //testing
                        score[index] = item.Score; //testing

                        index++;



                    }

                    Array.Sort(score);

                    imgIndex++;
                }
            }


            try
            {
                // System.Threading.Thread.Sleep(2000);
                //System.Windows.Forms.MessageBox.Show("Control OK: " + control.ToString()); //don't disable or remove this message box
            }
            catch (ViDi2.Exception e)
            {
                string message = e.Message;
            }

            if (control != null)
            {
                control.Dispose();
                control = null;
            }


            #endregion            

            return ret;
        }
        private bool OriginalAsGPU2NoMultiThreadingMM(IIMageFifo[] IImages)
        {

            Application.DoEvents();


            bool ret = true;


            invy.ClearListBox(lstGPU2log);

            #region MaximizeThroughput
            //_250723_093900


            int gpu0Add = (int)nuUDJob0gpuIndex.Value;
            int gpu1Add = (int)nuUDJob1gpuIndex.Value;
            // (1) 
            //
            // To maximize throughput all tools will use only one GPU. We can then use a hardware concurrency
            // equal to the number of GPUs.
            //{
            //System.Console.WriteLine("Example maximizing throughput");

            loadLog("Example maximizing throughput With IImage Buffer Loading");

            // List<int> GPUList = new List<int>();
            // We could instead specify which gpu to use by initializing with :

            List<int> GPUList;
            if (gpu0Add == 0 && gpu1Add == 1)
                GPUList = new List<int>() { 0, 1 };
            else
                GPUList = new List<int>();

            // to use only first and second GPUs

            // Initialize a control
            ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);

            //using (ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList))
            //{
            loadLog("control  Initialized");
            // Initialilizes the Compute devices
            // Parameters : - GPUMode.SingleDevicePerTool each tool will use a single GPU -> Maximizing throughput
            //              - new GPUList : automatically resolve all available gpus if empty

            control.InitializeComputeDevices(GpuMode.SingleDevicePerTool, GPUList);

            loadLog("Compute Devices  Initialized");

            var computeDevices = control.ComputeDevices;

            // the example will run with fewer than 2 GPUs, but the results might not be meaningful
            if (computeDevices.Count < 2)
            {
                //Console.WriteLine("Warning ! Example needs at least two GPUs to be meaningfull");                        
                loadLog("Warning ! Example needs at least two GPUs to be meaningfull");
            }

            //Console.WriteLine("Available computing devices :");                   
            loadLog("Available computing devices :");

            foreach (var computeDevice in control.ComputeDevices)  //computeDevices)
            {
                //Console.WriteLine($"{computeDevice.Index} : Card {computeDevice.Name}");                        
                loadLog($"\t\t\t{computeDevice.Index} : Card {computeDevice.Name}");
            }

            string runtimeModelsPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\Resources\runtime\Iscar\Endmill\";

            var WorkspaceFiles = new List<Tuple<string, string, string>>
                    {
                        // if you want to process HDM tool on a specific GPU, additional parameter needed when opening the workspace
                        // STREAM_NAME/TOOL_NAME/<GPU_INDEX> => default/Classify/0                       
                        //new Tuple<string, string, string>("HDM-Red-0", "..\\..\\..\\..\\resources\\runtime\\Red High-detail Tool.vrws", "default/Analyze/0"),
                        //new Tuple<string, string, string>("HDM-Red-1", "..\\..\\..\\..\\resources\\runtime\\Red High-detail Tool.vrws", "default/Analyze/1"),            

                        new Tuple<string, string, string>("HDM-Red-0",  runtimeModelsPath + "proj_001_050723_093500_06072023_134019.vrws", "default/red_HDM_20M_5472x3648/" + gpu0Add.ToString()),
                        new Tuple<string, string, string>("HDM-Red-1",  runtimeModelsPath + "proj_002_100723_114100_10072023_144042.vrws", "default/red_HDM_20M_5472x3648/" + gpu1Add.ToString())

                        //new Tuple<string, string, string>("HDM-Red-0", "..\\..\\..\\..\\resources\\runtime\\Iscar\\proj_001_050723_093500_17072023_110032.vrws", "default/Analyze/0"),
                        //new Tuple<string, string, string>("HDM-Red-1", "..\\..\\..\\..\\resources\\runtime\\Iscar\\proj_001_050723_093500_17072023_110032.vrws", "default/Analyze/1")
                    };

            //Console.WriteLine($"\n----- Load Runtime Workspaces -----");                    
            loadLog($"\n----- Load Runtime Workspaces -----");

            // opens a runtime workspace from file
            //string WorkspaceFile = "..\\..\\..\\..\\resources\\runtime\\Textile.vrws";

            // Instead of using mutex, you can consider the ConcurrentDictionary.
            var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();
            int toolNum = 0;
            foreach (var wsInfo in WorkspaceFiles)
            {
                if (!File.Exists(wsInfo.Item2))
                {
                    // if you got here then it's likely that the resources were not extracted in the path
                    //Console.WriteLine($"Fatal : {wsInfo.Item2} does not exist");
                    //Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");

                    loadLog($"Fatal : {wsInfo.Item2} does not exist");
                    loadLog($"Current Directory = { Directory.GetCurrentDirectory()}");

                    return false;
                }
                string wsName = wsInfo.Item1;
                string wsPath = wsInfo.Item2;
                string gpuHdm = wsInfo.Item3;
                if (string.IsNullOrEmpty(gpuHdm))
                    StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath).Streams["default"]);
                else
                    // needs additional parameter 'gpuHdm' for allocating dedicated gpu on HDM tool
                    StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath, gpuHdm).Streams["default"]);

                //Console.WriteLine(wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath));
                loadLog(wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath));


                toolNum++;
            }

            //if (!File.Exists(WorkspaceFile))
            //{
            //    // if you got here then it's likely that the resources were not extracted in the path
            //    Console.WriteLine($"{WorkspaceFile} does not exist");
            //    Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");
            //    return;
            //}

            string imagesPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";
            // images for process            
            string RedImagePath = imagesPath + "\\resources\\images\\Iscar\\220_06.35_041_1.jpg";
            string RedImagePath1 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_031_1.jpg";
            string RedImagePath2 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_031_2.jpg";
            string RedImagePath3 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_1.jpg";
            string RedImagePath4 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_2.jpg";
            string RedImagePath5 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_3.jpg";
            string RedImagePath6 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_1.jpg";
            string RedImagePath7 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_2.jpg";
            string RedImagePath8 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_3.jpg";
            string RedImagePath9 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_035_1.jpg";
            string RedImagePath10 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_035_2.jpg";
            string RedImagePath11 = imagesPath + "\\resources\\images\\Iscar\\000030.png";

            if (!File.Exists(RedImagePath))
            {
                // if you got here then it's likely that the resources were not extracted in the path
                //Console.WriteLine($"{RedImagePath} does not exist");                       
                //Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");

                loadLog($"{RedImagePath} does not exist");
                loadLog($"Current Directory = { Directory.GetCurrentDirectory()}");

                return false;
            }
            else
            {
                //Console.WriteLine($"\n\rRedImageName   : {Path.GetFullPath(RedImagePath)}");
                loadLog($"\n\rRedImageName   : {Path.GetFullPath(RedImagePath)}");

                //ViDi2.IImage img;
                ViDi2.FormsImage img = new ViDi2.FormsImage(RedImagePath);

                //Console.WriteLine("\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString());                       
                loadLog("\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString());
            }


            int batchSize = (int)nuUDBatchSize.Value;
            // batch job lists for test
            // In order to demonstrate two GPUs performing simultaneously on two threads using different workspaces, 
            // the workspaces are frequently replaced while repeating small count of images.
            // Tuple<WorkspaceName, ImagePath, IterationCount>
            //var Thread0Job = new List<Tuple<string, string, int>>(); //when using image path
            var Thread0Job = new List<Tuple<string, IIMageFifo, int>>(); //when using images buffer

            if (nuUDSizejob1.Value > 0)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[0], batchSize));
            if (nuUDSizejob1.Value > 1)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[1], batchSize));
            if (nuUDSizejob1.Value > 2)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[2], batchSize));
            if (nuUDSizejob1.Value > 3)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[3], batchSize));
            if (nuUDSizejob1.Value > 4)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[4], batchSize));
            if (nuUDSizejob1.Value > 5)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[5], batchSize));

            var Thread1Job = new List<Tuple<string, IIMageFifo, int>>();

            if (nuUDSizejob2.Value > 0)
                Thread1Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-1", IImages[6], batchSize));
            if (nuUDSizejob2.Value > 1)
                Thread1Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-1", IImages[7], batchSize));
            if (nuUDSizejob2.Value > 2)
                Thread1Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-1", IImages[8], batchSize));
            if (nuUDSizejob2.Value > 3)
                Thread1Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-1", IImages[9], batchSize));
            if (nuUDSizejob2.Value > 4)
                Thread1Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-1", IImages[10], batchSize));



            //add roi
            bool xroiActive = false;
            if (xroiActive)
            {
                //----------------------------------tool-0-------------------------------------
                IToolList<ViDi2.Runtime.ITool> toolslst = StreamDict["HDM-Red-0"].Tools;
                IRedTool hdRedTool = (IRedTool)toolslst["red_HDM_20M_5472x3648"];
                ViDi2.IManualRegionOfInterest redROI = (ViDi2.IManualRegionOfInterest)hdRedTool.RegionOfInterest;
                redROI.Parameters.Units = ViDi2.UnitsMode.Pixel;
                redROI.Parameters.Offset = new ViDi2.Point(2009, 1733);
                redROI.Parameters.Size = new ViDi2.Size(1092, 508);
                //Console.WriteLine("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());

                loadLog("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());

                //----------------------------------tool-1-------------------------------------
                IToolList<ViDi2.Runtime.ITool> toolslst1 = StreamDict["HDM-Red-1"].Tools;
                IRedTool hdRedTool1 = (IRedTool)toolslst1["red_HDM_20M_5472x3648"];
                ViDi2.IManualRegionOfInterest redROI1 = (ViDi2.IManualRegionOfInterest)hdRedTool1.RegionOfInterest;
                redROI1.Parameters.Units = ViDi2.UnitsMode.Pixel;
                redROI1.Parameters.Offset = new ViDi2.Point(2009, 1733);
                redROI1.Parameters.Size = new ViDi2.Size(1092, 508);
                //Console.WriteLine("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());

                loadLog("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());

            }
            else
            {
                //Console.WriteLine("\n\rtool_1, tool_2, roi not active");
                loadLog("\n\rtool_1, tool_2, roi not active");
            }

            //Yoav 29-11-2023
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

            //StreamDict[wsName]
            bool xNotUsed = true;
            if (!xNotUsed)
            {
                Func<List<Tuple<string, IIMageFifo, int>>, int, int> ThreadAction = (jobs, gpuId) =>
                {
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    string ls, rs;
                    string wsName;
                    IImage imgPath;
                    int iterCount = 0;
                    foreach (var job in jobs)
                    {
                        wsName = job.Item1;
                        imgPath = job.Item2.iimage;
                        iterCount = job.Item3;

                        ls = (gpuId == 0) ? ("ENTER : " + wsName) : "...";
                        rs = (gpuId == 0) ? "..." : ("ENTER : " + wsName);
                        //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");                           
                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");

                        sw.Restart();
                        using (var img1 = imgPath)   //new ViDi2.Local.LibraryImage(imgPath))
                            for (var iteration = 0; iteration < iterCount; ++iteration)
                            {
                                using (ISample sample = StreamDict[wsName].CreateSample(img1))
                                {
                                    // process all tools on stream with specific gpu(gpuId)
                                    sample.Process(null, new List<int>() { gpuId });
                                    lstIMarking.Add(sample.Markings); //Yoav 29-112023
                                }
                            }
                        sw.Stop();

                        bool xNoTesing = true;
                        if (!xNoTesing)
                        {
                            Dictionary<string, IMarking> views01 = lstIMarking[0];

                            IMarking mm = views01["red_HDM_20M_5472x3648"];
                            ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];
                            ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                            //IReadOnlyCollection<IView> rm = mm.Views; //OK but regions are not expose 
                            //IView rm = mm.Views[0];

                            //var views02 = lstIMarking[1];
                            //var vv = views02["red_HDM_20M_5472x3648"];

                            //ViDi2.IRedView redview = (ViDi2.IRedView)vv.Views[0]; //must be IReadView to get the regions

                            RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
                            double[] score = new double[redview.Regions.Count];
                            int index = 0;
                            foreach (ViDi2.IRegion item in redview.Regions)
                            {
                                regionFound[index].area = item.Area;
                                regionFound[index].width = item.Width;
                                regionFound[index].height = item.Height;
                                regionFound[index].center = item.Center;
                                regionFound[index].score = item.Score;
                                regionFound[index].className = "not possable to know in this application";  // cn;  // item.Name; region name
                                regionFound[index].classColor = item.Color;
                                regionFound[index].compactness = item.Compactness;
                                regionFound[index].covers = item.Covers;
                                regionFound[index].outer = item.Outer;
                                regionFound[index].perimeter = item.Perimeter;

                                //this.lstGPU2log.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                                invy.ListBoxaddItem(lstGPU2log, (index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                                //regionFound[index] = item; //testing
                                score[index] = item.Score; //testing

                                index++;



                            }

                            Array.Sort(score);
                        }

                        ls = (gpuId == 0) ? ("EXIT  : " + wsName) : "...";
                        rs = (gpuId == 0) ? "..." : ("EXIT  : " + wsName);
                        //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                    }

                    //Console.WriteLine($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");                        
                    loadLog($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");

                    return gpuId;
                };

                System.Diagnostics.Stopwatch globalSw = new System.Diagnostics.Stopwatch();

                //Console.WriteLine($"\n----- Threads -----");                    
                loadLog($"\n----- Threads -----");

                globalSw.Start();
                // Create two threads and put corresponding JobList and gpu index.


                var threads = new List<Task>();
                threads.Add(Task.Factory.StartNew(() => ThreadAction(Thread0Job, gpu0Add)));
                threads.Add(Task.Factory.StartNew(() => ThreadAction(Thread1Job, gpu1Add)));
                // wait for all tasks to finish
                Task.WaitAll(threads.ToArray());
                globalSw.Stop();
                //Console.WriteLine($"\n----- End, Total {globalSw.ElapsedMilliseconds} ms -----");                    
                loadLog($"\n----- End, Total {globalSw.ElapsedMilliseconds} ms -----");
            }

            DateTime nowS = DateTime.Now;

            string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", nowS);
            loadLog(timenow14 + ": Start GPU2 Test...");

            List<Dictionary<string, IMarking>> lstIMarking01 = ThreadAction01MM(Thread0Job, gpu0Add, Thread1Job, gpu1Add, StreamDict);

            DateTime nowE = DateTime.Now;
            timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss.fff}", nowE);
            loadLog(timenow14 + ": End GPU2 Test");
            //int iMinutes = nowE.Minute - nowS.Minute;
            //int iSeconds = nowE.Second - nowS.Second;

            //if (iSeconds < 0) { iSeconds = 60 + iSeconds; }
            //loadLog("Test Time: " + iMinutes.ToString() + ":"  + iSeconds.ToString() + ", Minutes:Seconds");

            int hours = (nowE - nowS).Hours;
            int minutes = (nowE - nowS).Minutes;
            int seconds = (nowE - nowS).Seconds;
            int milliseconds = (nowE - nowS).Milliseconds;
            //loadLog("Test Time: " + minutes.ToString() + ":" + seconds.ToString() + ", Minutes:Seconds");
            loadLog("Test Time: " + minutes.ToString() + ":" + seconds.ToString() + ":" + milliseconds.ToString() + ", Minutes:Seconds:Millisconds");

            loadLog("");

            loadLog("Display Results " + lstIMarking01.Count.ToString() + " Images:");

            bool xNoTeting = false;
            if (!xNoTeting)
            {
                int imgIndex = 0;
                foreach (Dictionary<string, IMarking> item01 in lstIMarking01)
                {
                    Dictionary<string, IMarking> views01 = item01;   // lstIMarking01[0];

                    IMarking mm = views01["red_HDM_20M_5472x3648"];

                    ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];

                    if (redview.Regions.Count > 0)
                    {
                        ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                        RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
                        double[] score = new double[redview.Regions.Count];
                        int index = 0;

                        //lstGPU2log.Items.Add("Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());
                        invy.ListBoxaddItem(lstGPU2log, "Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());

                        foreach (ViDi2.IRegion item in redview.Regions)
                        {
                            regionFound[index].area = item.Area;
                            regionFound[index].width = item.Width;
                            regionFound[index].height = item.Height;
                            regionFound[index].center = item.Center;
                            regionFound[index].score = item.Score;
                            regionFound[index].className = "not possable to know in this application";  // item.Name; region name
                            regionFound[index].classColor = item.Color;
                            regionFound[index].compactness = item.Compactness;
                            regionFound[index].covers = item.Covers;
                            regionFound[index].outer = item.Outer;
                            regionFound[index].perimeter = item.Perimeter;

                            //lstGPU2log.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                            invy.ListBoxaddItem(lstGPU2log, (index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                            //regions[index] = item; //testing
                            score[index] = item.Score; //testing

                            index++;



                        }

                        Array.Sort(score);

                        imgIndex++;
                    }
                }
            }


            try
            {
                // System.Threading.Thread.Sleep(2000);
                //System.Windows.Forms.MessageBox.Show("Control OK: " + control.ToString()); //don't disable or remove this message box
            }
            catch (ViDi2.Exception e)
            {
                string message = e.Message;
            }

            if (control != null)
            {
                control.Dispose();
                control = null;
            }


            #endregion            

            return ret;
        }
        private TupleJobs OriginalAsGPU2NoMultiThreadingMMWithOuitput(IIMageFifo[] IImages)
        {

            //TupleJobs tupleJobs = new TupleJobs();

            Application.DoEvents();

            if (xInitDone) { goto initdone; }

            gtupleJobs.xNoError = true;


            invy.ClearListBox(lstGPU2log);

            #region MaximizeThroughput
            //_250723_093900


            int gpu0Add = (int)nuUDJob0gpuIndex.Value;
            int gpu1Add = (int)nuUDJob1gpuIndex.Value;
            // (1) 
            //
            // To maximize throughput all tools will use only one GPU. We can then use a hardware concurrency
            // equal to the number of GPUs.
            //{
            //System.Console.WriteLine("Example maximizing throughput");

            loadLog("Example maximizing throughput With IImage Buffer Loading");

            // List<int> GPUList = new List<int>();
            // We could instead specify which gpu to use by initializing with :

            List<int> GPUList;
            if (gpu0Add == 0 && gpu1Add == 1)
                GPUList = new List<int>() { 0, 1 };
            else
                GPUList = new List<int>();

            // to use only first and second GPUs

            // Initialize a control
            //ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);

            control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);

            //using (ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList))
            //{
            loadLog("control  Initialized");
            // Initialilizes the Compute devices
            // Parameters : - GPUMode.SingleDevicePerTool each tool will use a single GPU -> Maximizing throughput
            //              - new GPUList : automatically resolve all available gpus if empty

            control.InitializeComputeDevices(GpuMode.SingleDevicePerTool, GPUList);

            loadLog("Compute Devices  Initialized");

            var computeDevices = control.ComputeDevices;

            // the example will run with fewer than 2 GPUs, but the results might not be meaningful
            if (computeDevices.Count < 2)
            {
                //Console.WriteLine("Warning ! Example needs at least two GPUs to be meaningfull");                        
                loadLog("Warning ! Example needs at least two GPUs to be meaningfull");
            }

            //Console.WriteLine("Available computing devices :");                   
            loadLog("Available computing devices :");

            foreach (var computeDevice in control.ComputeDevices)  //computeDevices)
            {
                //Console.WriteLine($"{computeDevice.Index} : Card {computeDevice.Name}");                        
                loadLog($"\t\t\t{computeDevice.Index} : Card {computeDevice.Name}");
            }

            string runtimeModelsPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\Resources\runtime\Iscar\Endmill\";

            WorkspaceFiles = new List<Tuple<string, string, string>>
            {
                        // if you want to process HDM tool on a specific GPU, additional parameter needed when opening the workspace
                        // STREAM_NAME/TOOL_NAME/<GPU_INDEX> => default/Classify/0                       
                        //new Tuple<string, string, string>("HDM-Red-0", "..\\..\\..\\..\\resources\\runtime\\Red High-detail Tool.vrws", "default/Analyze/0"),
                        //new Tuple<string, string, string>("HDM-Red-1", "..\\..\\..\\..\\resources\\runtime\\Red High-detail Tool.vrws", "default/Analyze/1"),            

                        new Tuple<string, string, string>("HDM-Red-0",  runtimeModelsPath + "proj_001_050723_093500_06072023_134019.vrws", "default/red_HDM_20M_5472x3648/" + gpu0Add.ToString()),
                        new Tuple<string, string, string>("HDM-Red-1",  runtimeModelsPath + "proj_002_100723_114100_10072023_144042.vrws", "default/red_HDM_20M_5472x3648/" + gpu1Add.ToString())

                        //new Tuple<string, string, string>("HDM-Red-0", "..\\..\\..\\..\\resources\\runtime\\Iscar\\proj_001_050723_093500_17072023_110032.vrws", "default/Analyze/0"),
                        //new Tuple<string, string, string>("HDM-Red-1", "..\\..\\..\\..\\resources\\runtime\\Iscar\\proj_001_050723_093500_17072023_110032.vrws", "default/Analyze/1")
            };

            //Console.WriteLine($"\n----- Load Runtime Workspaces -----");                    
            loadLog($"\n----- Load Runtime Workspaces -----");

            // opens a runtime workspace from file
            //string WorkspaceFile = "..\\..\\..\\..\\resources\\runtime\\Textile.vrws";

            // Instead of using mutex, you can consider the ConcurrentDictionary.
            var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();
            int toolNum = 0;
            foreach (var wsInfo in WorkspaceFiles)
            {
                if (!File.Exists(wsInfo.Item2))
                {
                    // if you got here then it's likely that the resources were not extracted in the path
                    //Console.WriteLine($"Fatal : {wsInfo.Item2} does not exist");
                    //Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");

                    loadLog($"Fatal : {wsInfo.Item2} does not exist");
                    loadLog($"Current Directory = { Directory.GetCurrentDirectory()}");

                    gtupleJobs.xNoError = false;
                    return gtupleJobs;
                }
                string wsName = wsInfo.Item1;
                string wsPath = wsInfo.Item2;
                string gpuHdm = wsInfo.Item3;
                if (string.IsNullOrEmpty(gpuHdm))
                    StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath).Streams["default"]);
                else
                    // needs additional parameter 'gpuHdm' for allocating dedicated gpu on HDM tool
                    StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath, gpuHdm).Streams["default"]);

                //Console.WriteLine(wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath));
                loadLog(wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath));


                toolNum++;
            }

            //if (!File.Exists(WorkspaceFile))
            //{
            //    // if you got here then it's likely that the resources were not extracted in the path
            //    Console.WriteLine($"{WorkspaceFile} does not exist");
            //    Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");
            //    return;
            //}

            string imagesPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";
            // images for process            
            string RedImagePath = imagesPath + "\\resources\\images\\Iscar\\220_06.35_041_1.jpg";
            string RedImagePath1 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_031_1.jpg";
            string RedImagePath2 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_031_2.jpg";
            string RedImagePath3 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_1.jpg";
            string RedImagePath4 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_2.jpg";
            string RedImagePath5 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_3.jpg";
            string RedImagePath6 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_1.jpg";
            string RedImagePath7 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_2.jpg";
            string RedImagePath8 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_3.jpg";
            string RedImagePath9 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_035_1.jpg";
            string RedImagePath10 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_035_2.jpg";
            string RedImagePath11 = imagesPath + "\\resources\\images\\Iscar\\000030.png";

            if (!File.Exists(RedImagePath))
            {
                // if you got here then it's likely that the resources were not extracted in the path
                //Console.WriteLine($"{RedImagePath} does not exist");                       
                //Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");

                loadLog($"{RedImagePath} does not exist");
                loadLog($"Current Directory = { Directory.GetCurrentDirectory()}");

                gtupleJobs.xNoError = false;
                return gtupleJobs;
            }
            else
            {
                //Console.WriteLine($"\n\rRedImageName   : {Path.GetFullPath(RedImagePath)}");
                loadLog($"\n\rRedImageName   : {Path.GetFullPath(RedImagePath)}");

                //ViDi2.IImage img;
                ViDi2.FormsImage img = new ViDi2.FormsImage(RedImagePath);

                //Console.WriteLine("\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString());                       
                loadLog("\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString());
            }


            int batchSize = (int)nuUDBatchSize.Value;
            // batch job lists for test
            // In order to demonstrate two GPUs performing simultaneously on two threads using different workspaces, 
            // the workspaces are frequently replaced while repeating small count of images.
            // Tuple<WorkspaceName, ImagePath, IterationCount>
            //var Thread0Job = new List<Tuple<string, string, int>>(); //when using image path
            var Thread0Job = new List<Tuple<string, IIMageFifo, int>>(); //when using images buffer

            if (nuUDSizejob1.Value > 0)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[0], batchSize));
            if (nuUDSizejob1.Value > 1)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[1], batchSize));
            if (nuUDSizejob1.Value > 2)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[2], batchSize));
            if (nuUDSizejob1.Value > 3)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[3], batchSize));
            if (nuUDSizejob1.Value > 4)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[4], batchSize));
            if (nuUDSizejob1.Value > 5)
                Thread0Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-0", IImages[5], batchSize));

            var Thread1Job = new List<Tuple<string, IIMageFifo, int>>();

            if (nuUDSizejob2.Value > 0)
                Thread1Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-1", IImages[6], batchSize));
            if (nuUDSizejob2.Value > 1)
                Thread1Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-1", IImages[7], batchSize));
            if (nuUDSizejob2.Value > 2)
                Thread1Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-1", IImages[8], batchSize));
            if (nuUDSizejob2.Value > 3)
                Thread1Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-1", IImages[9], batchSize));
            if (nuUDSizejob2.Value > 4)
                Thread1Job.Add(new Tuple<string, IIMageFifo, int>("HDM-Red-1", IImages[10], batchSize));



            //add roi
            bool xroiActive = false;
            if (xroiActive)
            {
                //----------------------------------tool-0-------------------------------------
                IToolList<ViDi2.Runtime.ITool> toolslst = StreamDict["HDM-Red-0"].Tools;
                IRedTool hdRedTool = (IRedTool)toolslst["red_HDM_20M_5472x3648"];
                ViDi2.IManualRegionOfInterest redROI = (ViDi2.IManualRegionOfInterest)hdRedTool.RegionOfInterest;
                redROI.Parameters.Units = ViDi2.UnitsMode.Pixel;
                redROI.Parameters.Offset = new ViDi2.Point(2009, 1733);
                redROI.Parameters.Size = new ViDi2.Size(1092, 508);
                //Console.WriteLine("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());

                loadLog("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());

                //----------------------------------tool-1-------------------------------------
                IToolList<ViDi2.Runtime.ITool> toolslst1 = StreamDict["HDM-Red-1"].Tools;
                IRedTool hdRedTool1 = (IRedTool)toolslst1["red_HDM_20M_5472x3648"];
                ViDi2.IManualRegionOfInterest redROI1 = (ViDi2.IManualRegionOfInterest)hdRedTool1.RegionOfInterest;
                redROI1.Parameters.Units = ViDi2.UnitsMode.Pixel;
                redROI1.Parameters.Offset = new ViDi2.Point(2009, 1733);
                redROI1.Parameters.Size = new ViDi2.Size(1092, 508);
                //Console.WriteLine("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());

                loadLog("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());

            }
            else
            {
                //Console.WriteLine("\n\rtool_1, tool_2, roi not active");
                loadLog("\n\rtool_1, tool_2, roi not active");
            }

            //Yoav 29-11-2023
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

            //StreamDict[wsName]
            bool xNotUsed = true;
            if (!xNotUsed)
            {
                Func<List<Tuple<string, IIMageFifo, int>>, int, int> ThreadAction = (jobs, gpuId) =>
                {
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    string ls, rs;
                    string wsName;
                    IImage imgPath;
                    int iterCount = 0;
                    foreach (var job in jobs)
                    {
                        wsName = job.Item1;
                        imgPath = job.Item2.iimage;
                        iterCount = job.Item3;

                        ls = (gpuId == 0) ? ("ENTER : " + wsName) : "...";
                        rs = (gpuId == 0) ? "..." : ("ENTER : " + wsName);
                        //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");                           
                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");

                        sw.Restart();
                        using (var img1 = imgPath)   //new ViDi2.Local.LibraryImage(imgPath))
                            for (var iteration = 0; iteration < iterCount; ++iteration)
                            {
                                using (ISample sample = StreamDict[wsName].CreateSample(img1))
                                {
                                    // process all tools on stream with specific gpu(gpuId)
                                    sample.Process(null, new List<int>() { gpuId });
                                    lstIMarking.Add(sample.Markings); //Yoav 29-112023
                                }
                            }
                        sw.Stop();

                        bool xNoTesing = true;
                        if (!xNoTesing)
                        {
                            Dictionary<string, IMarking> views01 = lstIMarking[0];

                            IMarking mm = views01["red_HDM_20M_5472x3648"];
                            ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];
                            ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                            //IReadOnlyCollection<IView> rm = mm.Views; //OK but regions are not expose 
                            //IView rm = mm.Views[0];

                            //var views02 = lstIMarking[1];
                            //var vv = views02["red_HDM_20M_5472x3648"];

                            //ViDi2.IRedView redview = (ViDi2.IRedView)vv.Views[0]; //must be IReadView to get the regions

                            RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
                            double[] score = new double[redview.Regions.Count];
                            int index = 0;
                            foreach (ViDi2.IRegion item in redview.Regions)
                            {
                                regionFound[index].area = item.Area;
                                regionFound[index].width = item.Width;
                                regionFound[index].height = item.Height;
                                regionFound[index].center = item.Center;
                                regionFound[index].score = item.Score;
                                regionFound[index].className = "not possable to know in this application";  // cn;  // item.Name; region name
                                regionFound[index].classColor = item.Color;
                                regionFound[index].compactness = item.Compactness;
                                regionFound[index].covers = item.Covers;
                                regionFound[index].outer = item.Outer;
                                regionFound[index].perimeter = item.Perimeter;

                                //this.lstGPU2log.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                                invy.ListBoxaddItem(lstGPU2log, (index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                                //regionFound[index] = item; //testing
                                score[index] = item.Score; //testing

                                index++;



                            }

                            Array.Sort(score);
                        }

                        ls = (gpuId == 0) ? ("EXIT  : " + wsName) : "...";
                        rs = (gpuId == 0) ? "..." : ("EXIT  : " + wsName);
                        //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                    }

                    //Console.WriteLine($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");                        
                    loadLog($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");

                    return gpuId;
                };

                System.Diagnostics.Stopwatch globalSw = new System.Diagnostics.Stopwatch();

                //Console.WriteLine($"\n----- Threads -----");                    
                loadLog($"\n----- Threads -----");

                globalSw.Start();
                // Create two threads and put corresponding JobList and gpu index.


                var threads = new List<Task>();
                threads.Add(Task.Factory.StartNew(() => ThreadAction(Thread0Job, gpu0Add)));
                threads.Add(Task.Factory.StartNew(() => ThreadAction(Thread1Job, gpu1Add)));
                // wait for all tasks to finish
                Task.WaitAll(threads.ToArray());
                globalSw.Stop();
                //Console.WriteLine($"\n----- End, Total {globalSw.ElapsedMilliseconds} ms -----");                    
                loadLog($"\n----- End, Total {globalSw.ElapsedMilliseconds} ms -----");
            }

            DateTime nowS = DateTime.Now;

            string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", nowS);
            loadLog(timenow14 + ": Start GPU2 Test...");

            //store results globally
            gtupleJobs.jobs01 = Thread0Job;
            gtupleJobs.jobs02 = Thread1Job;

            gtupleJobs.gpuId01 = gpu0Add;
            gtupleJobs.gpuId02 = gpu1Add;

            gtupleJobs.StreamDict = StreamDict;

            xInitDone = true;

        initdone:;

            //run jobs
            //runJobsOnly(tupleJobs.jobs01, tupleJobs.gpuId01, tupleJobs.jobs02, tupleJobs.gpuId02, tupleJobs.StreamDict);

            goto initrun;

            #region ------------------------------------not used------------------------------------------
            List<Dictionary<string, IMarking>> lstIMarking01 = ThreadAction01MM(Thread0Job, gpu0Add, Thread1Job, gpu1Add, StreamDict);

            DateTime nowE = DateTime.Now;
            timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss.fff}", nowE);
            loadLog(timenow14 + ": End GPU2 Test");
            //int iMinutes = nowE.Minute - nowS.Minute;
            //int iSeconds = nowE.Second - nowS.Second;

            //if (iSeconds < 0) { iSeconds = 60 + iSeconds; }
            //loadLog("Test Time: " + iMinutes.ToString() + ":"  + iSeconds.ToString() + ", Minutes:Seconds");

            int hours = (nowE - nowS).Hours;
            int minutes = (nowE - nowS).Minutes;
            int seconds = (nowE - nowS).Seconds;
            int milliseconds = (nowE - nowS).Milliseconds;
            //loadLog("Test Time: " + minutes.ToString() + ":" + seconds.ToString() + ", Minutes:Seconds");
            loadLog("Test Time: " + minutes.ToString() + ":" + seconds.ToString() + ":" + milliseconds.ToString() + ", Minutes:Seconds:Millisconds");

            loadLog("");

            loadLog("Display Results " + lstIMarking01.Count.ToString() + " Images:");

            bool xNoTeting = false;
            if (!xNoTeting)
            {
                int imgIndex = 0;
                foreach (Dictionary<string, IMarking> item01 in lstIMarking01)
                {
                    Dictionary<string, IMarking> views01 = item01;   // lstIMarking01[0];

                    IMarking mm = views01["red_HDM_20M_5472x3648"];

                    ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];

                    if (redview.Regions.Count > 0)
                    {
                        ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                        RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
                        double[] score = new double[redview.Regions.Count];
                        int index = 0;

                        //lstGPU2log.Items.Add("Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());
                        invy.ListBoxaddItem(lstGPU2log, "Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());

                        foreach (ViDi2.IRegion item in redview.Regions)
                        {
                            regionFound[index].area = item.Area;
                            regionFound[index].width = item.Width;
                            regionFound[index].height = item.Height;
                            regionFound[index].center = item.Center;
                            regionFound[index].score = item.Score;
                            regionFound[index].className = "not possable to know in this application";  // item.Name; region name
                            regionFound[index].classColor = item.Color;
                            regionFound[index].compactness = item.Compactness;
                            regionFound[index].covers = item.Covers;
                            regionFound[index].outer = item.Outer;
                            regionFound[index].perimeter = item.Perimeter;

                            //lstGPU2log.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                            invy.ListBoxaddItem(lstGPU2log, (index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                            //regions[index] = item; //testing
                            score[index] = item.Score; //testing

                            index++;



                        }

                        Array.Sort(score);

                        imgIndex++;
                    }
                }
            }
        #endregion ------------------------------------not used------------------------------------------

        initrun:;
            try
            {
                // System.Threading.Thread.Sleep(2000);
                //System.Windows.Forms.MessageBox.Show("Control OK: " + control.ToString()); //don't disable or remove this message box
            }
            catch (ViDi2.Exception e)
            {
                string message = e.Message;
            }

            //done in form close
            //if (control != null)
            //{
            //    control.Dispose();
            //    control = null;
            //}


            #endregion            

            return gtupleJobs;
        }
        private TupleJobsFifo OriginalAsGPU2NoMultiThreadingMMWithOuitputForFifo(IIMageFifo[] IImages)
        {

            //TupleJobs tupleJobs = new TupleJobs();

            Application.DoEvents();

            if (xInitDone) { goto initdone; }

            gtupleJobs.xNoError = true;


            invy.ClearListBox(lstGPU2log);

            #region MaximizeThroughput
            //_250723_093900


            int gpu0Add = (int)nuUDJob0gpuIndex.Value;
            int gpu1Add = (int)nuUDJob1gpuIndex.Value;
            // (1) 
            //
            // To maximize throughput all tools will use only one GPU. We can then use a hardware concurrency
            // equal to the number of GPUs.
            //{
            //System.Console.WriteLine("Example maximizing throughput");

            loadLog("Example maximizing throughput With IImage Buffer Loading");

            // List<int> GPUList = new List<int>();
            // We could instead specify which gpu to use by initializing with :

            List<int> GPUList;
            if (gpu0Add == 0 && gpu1Add == 1)
                GPUList = new List<int>() { 0, 1 };
            else
                GPUList = new List<int>();

            // to use only first and second GPUs

            // Initialize a control
            //ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);

            control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);

            //using (ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList))
            //{
            loadLog("control  Initialized");
            // Initialilizes the Compute devices
            // Parameters : - GPUMode.SingleDevicePerTool each tool will use a single GPU -> Maximizing throughput
            //              - new GPUList : automatically resolve all available gpus if empty

            control.InitializeComputeDevices(GpuMode.SingleDevicePerTool, GPUList);

            loadLog("Compute Devices  Initialized");

            var computeDevices = control.ComputeDevices;

            // the example will run with fewer than 2 GPUs, but the results might not be meaningful
            if (computeDevices.Count < 2)
            {
                //Console.WriteLine("Warning ! Example needs at least two GPUs to be meaningfull");                        
                loadLog("Warning ! Example needs at least two GPUs to be meaningfull");
            }

            //Console.WriteLine("Available computing devices :");                   
            loadLog("Available computing devices :");

            foreach (var computeDevice in control.ComputeDevices)  //computeDevices)
            {
                //Console.WriteLine($"{computeDevice.Index} : Card {computeDevice.Name}");                        
                loadLog($"\t\t\t{computeDevice.Index} : Card {computeDevice.Name}");
            }

            string runtimeModelsPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\Resources\runtime\Iscar\Endmill\";

            WorkspaceFiles = new List<Tuple<string, string, string>>
            {
                        // if you want to process HDM tool on a specific GPU, additional parameter needed when opening the workspace
                        // STREAM_NAME/TOOL_NAME/<GPU_INDEX> => default/Classify/0                       
                        //new Tuple<string, string, string>("HDM-Red-0", "..\\..\\..\\..\\resources\\runtime\\Red High-detail Tool.vrws", "default/Analyze/0"),
                        //new Tuple<string, string, string>("HDM-Red-1", "..\\..\\..\\..\\resources\\runtime\\Red High-detail Tool.vrws", "default/Analyze/1"),            

                        new Tuple<string, string, string>("HDM-Red-0",  runtimeModelsPath + "proj_001_050723_093500_06072023_134019.vrws", "default/red_HDM_20M_5472x3648/" + gpu0Add.ToString()),
                        new Tuple<string, string, string>("HDM-Red-1",  runtimeModelsPath + "proj_002_100723_114100_10072023_144042.vrws", "default/red_HDM_20M_5472x3648/" + gpu1Add.ToString())

                        //new Tuple<string, string, string>("HDM-Red-0", "..\\..\\..\\..\\resources\\runtime\\Iscar\\proj_001_050723_093500_17072023_110032.vrws", "default/Analyze/0"),
                        //new Tuple<string, string, string>("HDM-Red-1", "..\\..\\..\\..\\resources\\runtime\\Iscar\\proj_001_050723_093500_17072023_110032.vrws", "default/Analyze/1")
            };

            //Console.WriteLine($"\n----- Load Runtime Workspaces -----");                    
            loadLog($"\n----- Load Runtime Workspaces -----");

            // opens a runtime workspace from file
            //string WorkspaceFile = "..\\..\\..\\..\\resources\\runtime\\Textile.vrws";

            // Instead of using mutex, you can consider the ConcurrentDictionary.
            var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();
            int toolNum = 0;
            foreach (var wsInfo in WorkspaceFiles)
            {
                if (!File.Exists(wsInfo.Item2))
                {
                    // if you got here then it's likely that the resources were not extracted in the path
                    //Console.WriteLine($"Fatal : {wsInfo.Item2} does not exist");
                    //Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");

                    loadLog($"Fatal : {wsInfo.Item2} does not exist");
                    loadLog($"Current Directory = { Directory.GetCurrentDirectory()}");

                    gtupleJobs.xNoError = false;
                    return gtupleJobsFifo;
                }
                string wsName = wsInfo.Item1;
                string wsPath = wsInfo.Item2;
                string gpuHdm = wsInfo.Item3;
                if (string.IsNullOrEmpty(gpuHdm))
                    StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath).Streams["default"]);
                else
                    // needs additional parameter 'gpuHdm' for allocating dedicated gpu on HDM tool
                    StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath, gpuHdm).Streams["default"]);

                //Console.WriteLine(wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath));
                loadLog(wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath));


                toolNum++;
            }

            //if (!File.Exists(WorkspaceFile))
            //{
            //    // if you got here then it's likely that the resources were not extracted in the path
            //    Console.WriteLine($"{WorkspaceFile} does not exist");
            //    Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");
            //    return;
            //}

            string imagesPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";
            // images for process            
            string RedImagePath = imagesPath + "\\resources\\images\\Iscar\\220_06.35_041_1.jpg";
            //string RedImagePath1 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_031_1.jpg";
            //string RedImagePath2 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_031_2.jpg";
            //string RedImagePath3 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_1.jpg";
            //string RedImagePath4 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_2.jpg";
            //string RedImagePath5 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_033_3.jpg";
            //string RedImagePath6 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_1.jpg";
            //string RedImagePath7 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_2.jpg";
            //string RedImagePath8 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_034_3.jpg";
            //string RedImagePath9 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_035_1.jpg";
            //string RedImagePath10 = imagesPath + "\\resources\\images\\Iscar\\223_03.00_035_2.jpg";
            //string RedImagePath11 = imagesPath + "\\resources\\images\\Iscar\\000030.png";

            if (!File.Exists(RedImagePath))
            {
                // if you got here then it's likely that the resources were not extracted in the path
                //Console.WriteLine($"{RedImagePath} does not exist");                       
                //Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");

                loadLog($"{RedImagePath} does not exist");
                loadLog($"Current Directory = { Directory.GetCurrentDirectory()}");

                gtupleJobs.xNoError = false;
                return gtupleJobsFifo;
            }
            else
            {
                //Console.WriteLine($"\n\rRedImageName   : {Path.GetFullPath(RedImagePath)}");
                loadLog($"\n\rRedImageName   : {Path.GetFullPath(RedImagePath)}");

                //ViDi2.IImage img;
                ViDi2.FormsImage img = new ViDi2.FormsImage(RedImagePath);

                //Console.WriteLine("\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString());                       
                loadLog("\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString());
            }


            int batchSize = (int)nuUDBatchSize.Value;
            // batch job lists for test
            // In order to demonstrate two GPUs performing simultaneously on two threads using different workspaces, 
            // the workspaces are frequently replaced while repeating small count of images.
            // Tuple<WorkspaceName, ImagePath, IterationCount>
            //var Thread0Job = new List<Tuple<string, string, int>>(); //when using image path
            var Thread0JobFifo = new List<Tuple<string, IIMageFifo, bool>>(); //when using images buffer
            bool xNewDate = true;

            //set active
            IImages[0].xNewImage = true;
            IImages[1].xNewImage = true;
            IImages[2].xNewImage = true;
            IImages[3].xNewImage = true;
            IImages[4].xNewImage = true;
            IImages[5].xNewImage = true;

            if (nuUDSizejob1.Value > 0)
                Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[0], xNewDate));
            if (nuUDSizejob1.Value > 1)
                Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[1], xNewDate));
            if (nuUDSizejob1.Value > 2)
                Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[2], xNewDate));
            if (nuUDSizejob1.Value > 3)
                Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[3], xNewDate));
            if (nuUDSizejob1.Value > 4)
                Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[4], xNewDate));
            if (nuUDSizejob1.Value > 5)
                Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[5], xNewDate));

            var Thread1JobFifo = new List<Tuple<string, IIMageFifo, bool>>();

            //set active
            IImages[6].xNewImage = true;
            IImages[7].xNewImage = true;
            IImages[8].xNewImage = true;
            IImages[9].xNewImage = true;
            IImages[10].xNewImage = true;

            if (nuUDSizejob2.Value > 0)
                Thread1JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-1", IImages[6], xNewDate));
            if (nuUDSizejob2.Value > 1)
                Thread1JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-1", IImages[7], xNewDate));
            if (nuUDSizejob2.Value > 2)
                Thread1JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-1", IImages[8], xNewDate));
            if (nuUDSizejob2.Value > 3)
                Thread1JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-1", IImages[9], xNewDate));
            if (nuUDSizejob2.Value > 4)
                Thread1JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-1", IImages[10], xNewDate));



            //add roi
            bool xroiActive = false;
            if (xroiActive)
            {
                //----------------------------------tool-0-------------------------------------
                IToolList<ViDi2.Runtime.ITool> toolslst = StreamDict["HDM-Red-0"].Tools;
                IRedTool hdRedTool = (IRedTool)toolslst["red_HDM_20M_5472x3648"];
                ViDi2.IManualRegionOfInterest redROI = (ViDi2.IManualRegionOfInterest)hdRedTool.RegionOfInterest;
                redROI.Parameters.Units = ViDi2.UnitsMode.Pixel;
                redROI.Parameters.Offset = new ViDi2.Point(2009, 1733);
                redROI.Parameters.Size = new ViDi2.Size(1092, 508);
                //Console.WriteLine("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());

                loadLog("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());

                //----------------------------------tool-1-------------------------------------
                IToolList<ViDi2.Runtime.ITool> toolslst1 = StreamDict["HDM-Red-1"].Tools;
                IRedTool hdRedTool1 = (IRedTool)toolslst1["red_HDM_20M_5472x3648"];
                ViDi2.IManualRegionOfInterest redROI1 = (ViDi2.IManualRegionOfInterest)hdRedTool1.RegionOfInterest;
                redROI1.Parameters.Units = ViDi2.UnitsMode.Pixel;
                redROI1.Parameters.Offset = new ViDi2.Point(2009, 1733);
                redROI1.Parameters.Size = new ViDi2.Size(1092, 508);
                //Console.WriteLine("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());

                loadLog("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());

            }
            else
            {
                //Console.WriteLine("\n\rtool_1, tool_2, roi not active");
                loadLog("\n\rtool_1, tool_2, roi not active");
            }

            //Yoav 29-11-2023
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

            //StreamDict[wsName]
            bool xNotUsed = true;
            if (!xNotUsed)
            {
                Func<List<Tuple<string, IIMageFifo, bool>>, int, int> ThreadAction = (jobs, gpuId) =>
                {
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    string ls, rs;
                    string wsName;
                    IImage imgPath;
                    int iterCount = 0;
                    foreach (var job in jobs)
                    {
                        wsName = job.Item1;
                        imgPath = job.Item2.iimage;
                        //iterCount = job.Item3;

                        ls = (gpuId == 0) ? ("ENTER : " + wsName) : "...";
                        rs = (gpuId == 0) ? "..." : ("ENTER : " + wsName);
                        //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");                           
                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");

                        sw.Restart();

                        using (var img1 = imgPath)   //new ViDi2.Local.LibraryImage(imgPath))
                            if (job.Item3)
                            {
                                using (ISample sample = StreamDict[wsName].CreateSample(img1))
                                {
                                    // process all tools on stream with specific gpu(gpuId)
                                    sample.Process(null, new List<int>() { gpuId });
                                    lstIMarking.Add(sample.Markings); //Yoav 29-112023
                                }
                            }
                        sw.Stop();

                        bool xNoTesing = true;
                        if (!xNoTesing)
                        {
                            Dictionary<string, IMarking> views01 = lstIMarking[0];

                            IMarking mm = views01["red_HDM_20M_5472x3648"];
                            ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];
                            ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                            //IReadOnlyCollection<IView> rm = mm.Views; //OK but regions are not expose 
                            //IView rm = mm.Views[0];

                            //var views02 = lstIMarking[1];
                            //var vv = views02["red_HDM_20M_5472x3648"];

                            //ViDi2.IRedView redview = (ViDi2.IRedView)vv.Views[0]; //must be IReadView to get the regions

                            RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
                            double[] score = new double[redview.Regions.Count];
                            int index = 0;
                            foreach (ViDi2.IRegion item in redview.Regions)
                            {
                                regionFound[index].area = item.Area;
                                regionFound[index].width = item.Width;
                                regionFound[index].height = item.Height;
                                regionFound[index].center = item.Center;
                                regionFound[index].score = item.Score;
                                regionFound[index].className = "not possable to know in this application";  // cn;  // item.Name; region name
                                regionFound[index].classColor = item.Color;
                                regionFound[index].compactness = item.Compactness;
                                regionFound[index].covers = item.Covers;
                                regionFound[index].outer = item.Outer;
                                regionFound[index].perimeter = item.Perimeter;

                                //this.lstGPU2log.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                                invy.ListBoxaddItem(lstGPU2log, (index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                                //regionFound[index] = item; //testing
                                score[index] = item.Score; //testing

                                index++;



                            }

                            Array.Sort(score);
                        }

                        ls = (gpuId == 0) ? ("EXIT  : " + wsName) : "...";
                        rs = (gpuId == 0) ? "..." : ("EXIT  : " + wsName);
                        //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                    }

                    //Console.WriteLine($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");                        
                    loadLog($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");

                    return gpuId;
                };

                System.Diagnostics.Stopwatch globalSw = new System.Diagnostics.Stopwatch();

                //Console.WriteLine($"\n----- Threads -----");                    
                loadLog($"\n----- Threads -----");

                globalSw.Start();
                // Create two threads and put corresponding JobList and gpu index.


                var threads = new List<Task>();
                threads.Add(Task.Factory.StartNew(() => ThreadAction(Thread0JobFifo, gpu0Add)));
                threads.Add(Task.Factory.StartNew(() => ThreadAction(Thread1JobFifo, gpu1Add)));
                // wait for all tasks to finish
                Task.WaitAll(threads.ToArray());
                globalSw.Stop();
                //Console.WriteLine($"\n----- End, Total {globalSw.ElapsedMilliseconds} ms -----");                    
                loadLog($"\n----- End, Total {globalSw.ElapsedMilliseconds} ms -----");
            }

            DateTime nowS = DateTime.Now;

            string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", nowS);
            loadLog(timenow14 + ": Start GPU2 Test...");

            //store results globally
            gtupleJobsFifo.FifoJob01 = Thread0JobFifo;
            gtupleJobsFifo.FifoJob02 = Thread1JobFifo;

            gtupleJobsFifo.gpuId01 = gpu0Add;
            gtupleJobsFifo.gpuId02 = gpu1Add;

            gtupleJobsFifo.StreamDict = StreamDict;

            xInitDone = true;

        initdone:;

            //run jobs
            //runJobsOnly(tupleJobs.jobs01, tupleJobs.gpuId01, tupleJobs.jobs02, tupleJobs.gpuId02, tupleJobs.StreamDict);

            goto initrun;

        initrun:;
            try
            {
                // System.Threading.Thread.Sleep(2000);
                //System.Windows.Forms.MessageBox.Show("Control OK: " + control.ToString()); //don't disable or remove this message box
            }
            catch (ViDi2.Exception e)
            {
                string message = e.Message;
            }

            //done in form close
            //if (control != null)
            //{
            //    control.Dispose();
            //    control = null;
            //}


            #endregion            

            return gtupleJobsFifo;
        }       
        private async Task<TupleJobsFifo02> OriginalAsGPU2NoMultiThreadingMMWithOuitputForFifoBW01(IIMageFifo[] IImages,   CurrentState state)
        {

            //TupleJobs tupleJobs = new TupleJobs();
            try
            {
                Application.DoEvents();

                if (xInitDone) { goto initdone; }

                gtupleJobs.xNoError = true;

                //invy.ClearListBox(lstGPU2log);

                #region MaximizeThroughput           
                /*set job num to cpu num*/
                int gpu0Add = (int)nuUDJob0gpuIndex.Value;
                int gpu1Add = (int)nuUDJob1gpuIndex.Value;


                //bool ReadOK = false;
                state.Msg = "Example maximizing throughput With IImage Buffer Loading";
                //worker.ReportProgress(1, state);
                var task=Task.Run(()=>ProgressChangedAsync(state));
                System.Threading.Thread.Sleep(50);

                // List<int> GPUList = new List<int>();
                // We could instead specify which gpu to use by initializing with :

                List<int> GPUList;
                if (gpu0Add == 0 && gpu1Add == 1)
                    GPUList = new List<int>() { 0, 1 };
                else
                    GPUList = new List<int>();

                // to use only first and second GPUs

                // Initialize a control
                //ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);

                control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);

                state.Msg = "control  Initialized";
                //worker.ReportProgress(2, state);
                Task.Run(()=>ProgressChangedAsync(state));
                System.Threading.Thread.Sleep(50);

                // Initialilizes the Compute devices
                // Parameters : - GPUMode.SingleDevicePerTool each tool will use a single GPU -> Maximizing throughput
                //              - new GPUList : automatically resolve all available gpus if empty

                control.InitializeComputeDevices(GpuMode.SingleDevicePerTool, GPUList);

                state.Msg = "Compute Devices  Initialized";
                //worker.ReportProgress(3, state);
                System.Threading.Thread.Sleep(50);

                var computeDevices = control.ComputeDevices;

                // the example will run with fewer than 2 GPUs, but the results might not be meaningful
                if (computeDevices.Count < 2)
                {
                    //Console.WriteLine("Warning ! Example needs at least two GPUs to be meaningfull");                                       
                    state.Msg = "Warning ! Example needs at least two GPUs to be meaningfull";
                    //worker.ReportProgress(4, state);
                    Task.Run(()=>ProgressChangedAsync(state));
                    state.Msg = ""; //clear message
                    System.Threading.Thread.Sleep(100);
                }

                //
                state.Msg = ""; //clear message

                //Console.WriteLine("Available computing devices :");                               
                state.Msg = "Available computing devices :";
                //worker.ReportProgress(5, state);
                Task.Run(()=>ProgressChangedAsync(state));
                System.Threading.Thread.Sleep(50);
                string sentMsg = "";
                foreach (var computeDevice in control.ComputeDevices)  //computeDevices)
                {

                    //loadLog($"\t\t\t{computeDevice.Index} : Card {computeDevice.Name}");
                    state.Msg = $"\t\t\t{computeDevice.Index} : Card {computeDevice.Name}";
                    //worker.ReportProgress(6, state);
                    Task.Run(()=>ProgressChangedAsync(state));
                    System.Threading.Thread.Sleep(50);
                }

                //string runtimeModelsPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\Resources\runtime\Iscar\";
                string runtimeModelsPath = gmodels.path + @"\";
                string model1Name = gmodels.model1FileName;
                string model2Name = gmodels.model2FileName;
                WorkspaceFiles = new List<Tuple<string, string, string>>
            {
                 new Tuple<string, string, string>("HDM-Red-0",  runtimeModelsPath + model1Name, "default/red_HDM_20M_5472x3648/" + gpu0Add.ToString()),
                 new Tuple<string, string, string>("HDM-Red-1",  runtimeModelsPath + model2Name, "default/red_HDM_20M_5472x3648/" + gpu1Add.ToString())
            };
                //activate the second gpu
                state.Msg = "------ Loading Runtime Workspaces -----";
                //worker.ReportProgress(6, state);
                Task.Run(()=>ProgressChangedAsync(state));
                System.Threading.Thread.Sleep(50);


                // opens a runtime workspace from file
                //string WorkspaceFile = "..\\..\\..\\..\\resources\\runtime\\Textile.vrws";

                // Instead of using mutex, you can consider the ConcurrentDictionary.
                var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();
                int toolNum = 0;
                foreach (var wsInfo in WorkspaceFiles)
                {
                    if (!File.Exists(wsInfo.Item2))
                    {
                        // if you got here then it's likely that the resources were not extracted in the path

                        //loadLog($"Fatal : {wsInfo.Item2} does not exist");
                        //loadLog($"Current Directory = { Directory.GetCurrentDirectory()}");

                        state.Msg = $"Fatal : {wsInfo.Item2} does not exist";
                        //worker.ReportProgress(6, state);
                        Task.Run(()=>ProgressChangedAsync(state));
                        System.Threading.Thread.Sleep(50);
                        state.Msg = $"Current Directory = { Directory.GetCurrentDirectory()}";
                        //worker.ReportProgress(6, state);
                        Task.Run(()=>ProgressChangedAsync(state));
                        System.Threading.Thread.Sleep(20);

                        gtupleJobs.xNoError = false;
                        gtupleJobsFifo02.state = state;
                        return gtupleJobsFifo02;
                    }
                    string wsName = wsInfo.Item1;
                    string wsPath = wsInfo.Item2;
                    string gpuHdm = wsInfo.Item3;
                    if (string.IsNullOrEmpty(gpuHdm))
                        StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath).Streams["default"]);
                    else
                        // needs additional parameter 'gpuHdm' for allocating dedicated gpu on HDM tool
                        StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath, gpuHdm).Streams["default"]);

                    //loadLog(wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath));
                    state.Msg = wsName.PadRight(18, ' ') + "LOADED Tool " + toolNum.ToString() + " => " + Path.GetFullPath(wsPath);
                    //worker.ReportProgress(6, state);
                    Task.Run(()=>ProgressChangedAsync(state));
                    System.Threading.Thread.Sleep(20);
                    toolNum++;
                }

                string imagesPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";
                // images for process            
                string RedImagePath = imagesPath + "\\resources\\images\\Iscar\\220_06.35_041_1.jpg";

                if (!File.Exists(RedImagePath))
                {
                    // if you got here then it's likely that the resources were not extracted in the path
                    //Console.WriteLine($"{RedImagePath} does not exist");                       
                    //Console.WriteLine($"Current Directory = { Directory.GetCurrentDirectory()}");


                    state.Msg = $"{RedImagePath} does not exist";
                    //worker.ReportProgress(7, state);
                    var task10 = Task.Run(()=>ProgressChangedAsync(state));
                    System.Threading.Thread.Sleep(20);
                    state.Msg = $"Current Directory = { Directory.GetCurrentDirectory()}";
                    //worker.ReportProgress(7, state);
                    Task.Run(() => ProgressChangedAsync(state));
                    gtupleJobsFifo02.state = state;
                    gtupleJobs.xNoError = false;
                    return gtupleJobsFifo02;
                }
                else
                {

                    state.Msg = $"\n\rRedImageName   : {Path.GetFullPath(RedImagePath)}";
                    //worker.ReportProgress(8, state);
                    var task12 = Task.Run(() => ProgressChangedAsync(state));
                    System.Threading.Thread.Sleep(50);
                    //ViDi2.IImage img;
                    ViDi2.FormsImage img = new ViDi2.FormsImage(RedImagePath);

                    //loadLog("\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString());
                    state.Msg = "\n\rImage Size: " + img.Width.ToString() + "x" + img.Height.ToString();
                    //worker.ReportProgress(8, state);
                    Task.Run(() => ProgressChangedAsync(state));
                    System.Threading.Thread.Sleep(20);
                }


                int batchSize = (int)nuUDBatchSize.Value;
                // batch job lists for test
                // In order to demonstrate two GPUs performing simultaneously on two threads using different workspaces, 
                // the workspaces are frequently replaced while repeating small count of images.
                // Tuple<WorkspaceName, ImagePath, IterationCount>


                //var Thread0JobFifo = new List<Tuple<string, IIMageFifo, bool>>(); //when using images buffer
                //var Thread0JobFifo = new List<IIMageFifo>(); //when using images buffer

                IIMageFifo[] Thread0JobFifo = new IIMageFifo[20]; //when using images buffer
                IIMageFifo[] Thread1JobFifo = new IIMageFifo[20];

                IIMageFifo iIMageFifo = new IIMageFifo();
                bool xNewDate = true;
                /*divition into two gpu*/
                for (int i = 0; i < 6; i++)
                {
                    if (IImages[i].iimage != null)
                    {
                        IImages[i].gpuName = "HDM-Red-0";
                        IImages[i].xNewImage = xNewDate;
                        Thread0JobFifo[i] = IImages[i];
                    }
                }

                for (int i = 0; i < 5; i++)
                {
                    if (IImages[i + 6].iimage != null)
                    {
                        IImages[i + 6].gpuName = "HDM-Red-1";
                        IImages[i + 6].xNewImage = xNewDate;
                        Thread1JobFifo[i] = IImages[i + 6];
                    }
                }

                //if (nuUDSizejob1.Value > 0)
                //{
                //    //Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[0], xNewDate));
                //    // iIMageFifo = new IIMageFifo();
                //    //;iIMageFifo.gpuName   = "HDM-Red-0";
                //    //iIMageFifo.iimage    = IImages[0].iimage;
                //    //iIMageFifo.xNewImage = xNewDate;

                //    IImages[0].gpuName = "HDM-Red-0";
                //    IImages[0].xNewImage = xNewDate;
                //    Thread0JobFifo[0] = IImages[0];  
                //}

                //if (nuUDSizejob1.Value > 1)
                //{
                //    //Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[1], xNewDate));
                //    iIMageFifo = new IIMageFifo();
                //    iIMageFifo.gpuName = "HDM-Red-0";
                //    iIMageFifo.iimage = IImages[1].iimage;
                //    iIMageFifo.xNewImage = xNewDate;
                //    //Thread0JobFifo.Add(iIMageFifo);
                //    Thread0JobFifo[1] = iIMageFifo;
                //}

                //if (nuUDSizejob1.Value > 2)
                //{
                //    //Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[2], xNewDate));
                //    iIMageFifo = new IIMageFifo();
                //    iIMageFifo.gpuName   = "HDM-Red-0";
                //    iIMageFifo.iimage    = IImages[2].iimage;
                //    iIMageFifo.xNewImage = xNewDate;
                //    //Thread0JobFifo.Add(iIMageFifo);
                //    Thread0JobFifo[2] = iIMageFifo;
                //}
                //if (nuUDSizejob1.Value > 3)
                //{
                //    //Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[3], xNewDate));
                //    iIMageFifo = new IIMageFifo();
                //    iIMageFifo.gpuName = "HDM-Red-0";
                //    iIMageFifo.iimage = IImages[3].iimage;
                //    iIMageFifo.xNewImage = xNewDate;
                //    //Thread0JobFifo.Add(iIMageFifo);
                //    Thread0JobFifo[3] = iIMageFifo;
                //}
                //if (nuUDSizejob1.Value > 4)
                //{
                //    //Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[4], xNewDate));
                //    iIMageFifo = new IIMageFifo();
                //    iIMageFifo.gpuName = "HDM-Red-0";
                //    iIMageFifo.iimage = IImages[4].iimage;
                //    iIMageFifo.xNewImage = xNewDate;
                //    //Thread0JobFifo.Add(iIMageFifo);
                //    Thread0JobFifo[4] = iIMageFifo;
                //}
                //if (nuUDSizejob1.Value > 5)
                //{
                //    //Thread0JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-0", IImages[5], xNewDate));
                //    iIMageFifo           = new IIMageFifo();
                //    iIMageFifo.gpuName   = "HDM-Red-0";
                //    iIMageFifo.iimage    = IImages[5].iimage;
                //    iIMageFifo.xNewImage = xNewDate;
                //    //Thread0JobFifo.Add(iIMageFifo);
                //    Thread0JobFifo[5] = iIMageFifo;
                //}

                ////var Thread1JobFifo = new List<IIMageFifo>();
                ////IIMageFifo[] Thread1JobFifo = new IIMageFifo[20];
                ////
                ////IImages[6].xNewImage = xNewDate;
                ////IImages[7].xNewImage = xNewDate;
                ////IImages[8].xNewImage = xNewDate;
                ////IImages[9].xNewImage = xNewDate;
                ////IImages[10].xNewImage = xNewDate;

                //if (nuUDSizejob2.Value > 0)
                //{
                //    //Thread1JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-1", IImages[6], xNewDate));
                //    iIMageFifo = new IIMageFifo();
                //    iIMageFifo.gpuName = "HDM-Red-1";
                //    iIMageFifo.iimage = IImages[6].iimage;
                //    iIMageFifo.xNewImage = xNewDate;
                //    //Thread1JobFifo.Add(iIMageFifo);
                //    Thread1JobFifo[0] = iIMageFifo;
                //}

                //if (nuUDSizejob2.Value > 1)
                //{
                //    //Thread1JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-1", IImages[7], xNewDate));
                //    iIMageFifo = new IIMageFifo();
                //    iIMageFifo.gpuName = "HDM-Red-1";
                //    iIMageFifo.iimage = IImages[7].iimage;
                //    iIMageFifo.xNewImage = xNewDate;
                //    //Thread1JobFifo.Add(iIMageFifo);
                //    Thread1JobFifo[1] = iIMageFifo;
                //}

                //if (nuUDSizejob2.Value > 2)
                //{
                //    //Thread1JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-1", IImages[8], xNewDate));
                //    iIMageFifo = new IIMageFifo();
                //    iIMageFifo.gpuName = "HDM-Red-1";
                //    iIMageFifo.iimage = IImages[8].iimage;
                //    iIMageFifo.xNewImage = xNewDate;
                //    //Thread1JobFifo.Add(iIMageFifo);
                //    Thread1JobFifo[2] = iIMageFifo;
                //}
                //if (nuUDSizejob2.Value > 3)
                //{
                //    //Thread1JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-1", IImages[9], xNewDate));
                //    iIMageFifo = new IIMageFifo();
                //    iIMageFifo.gpuName = "HDM-Red-1";
                //    iIMageFifo.iimage = IImages[9].iimage;
                //    iIMageFifo.xNewImage = xNewDate;
                //    //Thread1JobFifo.Add(iIMageFifo);
                //    Thread1JobFifo[3] = iIMageFifo;
                //}

                //if (nuUDSizejob2.Value > 4)
                //{
                //    //Thread1JobFifo.Add(new Tuple<string, IIMageFifo, bool>("HDM-Red-1", IImages[10], xNewDate));
                //    iIMageFifo = new IIMageFifo();
                //    iIMageFifo.gpuName = "HDM-Red-1";
                //    iIMageFifo.iimage = IImages[10].iimage;
                //    iIMageFifo.xNewImage = xNewDate;
                //    //Thread1JobFifo.Add(iIMageFifo);
                //    Thread1JobFifo[4] = iIMageFifo;
                //}

                //add roi
                bool xroiActive = false;
                if (xroiActive)
                {
                    //----------------------------------tool-0-------------------------------------
                    IToolList<ViDi2.Runtime.ITool> toolslst = StreamDict["HDM-Red-0"].Tools;
                    IRedTool hdRedTool = (IRedTool)toolslst["red_HDM_20M_5472x3648"];
                    ViDi2.IManualRegionOfInterest redROI = (ViDi2.IManualRegionOfInterest)hdRedTool.RegionOfInterest;
                    redROI.Parameters.Units = ViDi2.UnitsMode.Pixel;
                    redROI.Parameters.Offset = new ViDi2.Point(2009, 1733);
                    redROI.Parameters.Size = new ViDi2.Size(1092, 508);
                    //Console.WriteLine("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());

                    //loadLog("\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString());
                    state.Msg = "\n\rtool_1 roi active, offset:" + redROI.Parameters.Offset.ToString() + ", size: " + redROI.Parameters.Size.ToString();
                    //worker.ReportProgress(9, state);
                    Task.Run(() => ProgressChangedAsync(state));
                    System.Threading.Thread.Sleep(50);
                    //----------------------------------tool-1-------------------------------------
                    IToolList<ViDi2.Runtime.ITool> toolslst1 = StreamDict["HDM-Red-1"].Tools;
                    IRedTool hdRedTool1 = (IRedTool)toolslst1["red_HDM_20M_5472x3648"];
                    ViDi2.IManualRegionOfInterest redROI1 = (ViDi2.IManualRegionOfInterest)hdRedTool1.RegionOfInterest;
                    redROI1.Parameters.Units = ViDi2.UnitsMode.Pixel;
                    redROI1.Parameters.Offset = new ViDi2.Point(2009, 1733);
                    redROI1.Parameters.Size = new ViDi2.Size(1092, 508);
                    //Console.WriteLine("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());

                    //loadLog("tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString());
                    state.Msg = "tool_2 roi active, offset:" + redROI1.Parameters.Offset.ToString() + ", size: " + redROI1.Parameters.Size.ToString();
                    //worker.ReportProgress(9, state);
                    Task.Run(() => ProgressChangedAsync(state));
                    System.Threading.Thread.Sleep(20);
                }
                else
                {
                    //loadLog("\n\rtool_1, tool_2, roi not active");
                    state.Msg = "\n\rtool_1, tool_2, roi not active";
                    //worker.ReportProgress(10, state);
                    Task.Run(() => ProgressChangedAsync(state));
                    System.Threading.Thread.Sleep(50);
                }

                //Yoav 29-11-2023
                //List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

                DateTime nowS = DateTime.Now;

                string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", nowS);
                //loadLog(timenow14 + ": Start GPU2 Test...");
                state.Msg = timenow14 + ": Start GPU2 Test...";
                //worker.ReportProgress(10, state);
                Task.Run(() => ProgressChangedAsync(state));
                System.Threading.Thread.Sleep(50);

                //store results globally
                gtupleJobsFifo02.FifoJob01 = Thread0JobFifo;
                gtupleJobsFifo02.FifoJob02 = Thread1JobFifo;

                gtupleJobsFifo.gpuId01 = gpu0Add;
                gtupleJobsFifo.gpuId02 = gpu1Add;

                gtupleJobsFifo.StreamDict = StreamDict;

                xInitDone = true;

            initdone:;

                try
                {
                    // System.Threading.Thread.Sleep(2000);
                    //System.Windows.Forms.MessageBox.Show("Control OK: " + control.ToString()); //don't disable or remove this message box
                }
                catch (ViDi2.Exception e)
                {
                    string message = e.Message;
                }

                //done in form close
                //if (control != null)
                //{
                //    control.Dispose();
                //    control = null;
                //}


                #endregion
                gtupleJobsFifo02.state = state;
                return gtupleJobsFifo02;
            }
            catch (System.Exception) { gtupleJobsFifo02.state = state;  return gtupleJobsFifo02; }
        }
        private void runJobsOnly(List<Tuple<string, IIMageFifo, int>> jobs01, int gpuId01, List<Tuple<string, IIMageFifo, int>> jobs02, int gpuId02, Dictionary<string, ViDi2.Runtime.IStream> StreamDict)
        {
            DateTime nowS = DateTime.Now;

            List<Dictionary<string, IMarking>> lstIMarking01 = ThreadAction01MM(jobs01, gpuId01, jobs02, gpuId02, StreamDict);

            DateTime nowE = DateTime.Now;
            string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss.fff}", nowE);
            loadLog(timenow14 + ": End GPU2 Test");
            //int iMinutes = nowE.Minute - nowS.Minute;
            //int iSeconds = nowE.Second - nowS.Second;

            //if (iSeconds < 0) { iSeconds = 60 + iSeconds; }
            //loadLog("Test Time: " + iMinutes.ToString() + ":"  + iSeconds.ToString() + ", Minutes:Seconds");

            int hours = (nowE - nowS).Hours;
            int minutes = (nowE - nowS).Minutes;
            int seconds = (nowE - nowS).Seconds;
            int milliseconds = (nowE - nowS).Milliseconds;
            //loadLog("Test Time: " + minutes.ToString() + ":" + seconds.ToString() + ", Minutes:Seconds");
            loadLog("Test Time: " + minutes.ToString() + ":" + seconds.ToString() + ":" + milliseconds.ToString() + ", Minutes:Seconds:Millisconds");

            loadLog("");

            loadLog("Display Results " + lstIMarking01.Count.ToString() + " Images:");

            bool xNoTeting = false;
            if (!xNoTeting)
            {
                int imgIndex = 0;
                foreach (Dictionary<string, IMarking> item01 in lstIMarking01)
                {
                    Dictionary<string, IMarking> views01 = item01;   // lstIMarking01[0];

                    IMarking mm = views01["red_HDM_20M_5472x3648"];

                    ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];

                    if (redview.Regions.Count > 0)
                    {
                        ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                        RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
                        double[] score = new double[redview.Regions.Count];
                        int index = 0;

                        //lstGPU2log.Items.Add("Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());
                        invy.ListBoxaddItem(lstGPU2log, "Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());

                        foreach (ViDi2.IRegion item in redview.Regions)
                        {
                            regionFound[index].area = item.Area;
                            regionFound[index].width = item.Width;
                            regionFound[index].height = item.Height;
                            regionFound[index].center = item.Center;
                            regionFound[index].score = item.Score;
                            regionFound[index].className = "not possable to know in this application";  // item.Name; region name
                            regionFound[index].classColor = item.Color;
                            regionFound[index].compactness = item.Compactness;
                            regionFound[index].covers = item.Covers;
                            regionFound[index].outer = item.Outer;
                            regionFound[index].perimeter = item.Perimeter;

                           // lstGPU2log.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                            invy.ListBoxaddItem(lstGPU2log, (index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                            //regions[index] = item; //testing
                            score[index] = item.Score; //testing

                            index++;



                        }


                        Array.Sort(score);


                    }
                    else
                    {
                        //lstGPU2log.Items.Add("Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());
                        invy.ListBoxaddItem(lstGPU2log, "Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());
                    }
                    imgIndex++;
                }
            }

        }
        private void runJobsOnlyForFifo(List<Tuple<string, IIMageFifo, bool>> jobs01, int gpuId01, List<Tuple<string, IIMageFifo, bool>> jobs02, int gpuId02, Dictionary<string, ViDi2.Runtime.IStream> StreamDict)
        {
            DateTime nowS = DateTime.Now;

            List<Dictionary<string, IMarking>> lstIMarking01 = ThreadAction01MMForFifo(jobs01, gpuId01, jobs02, gpuId02, StreamDict);

            DateTime nowE = DateTime.Now;
            string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss.fff}", nowE);
            loadLog(timenow14 + ": End GPU2 Test");
            //int iMinutes = nowE.Minute - nowS.Minute;
            //int iSeconds = nowE.Second - nowS.Second;

            //if (iSeconds < 0) { iSeconds = 60 + iSeconds; }
            //loadLog("Test Time: " + iMinutes.ToString() + ":"  + iSeconds.ToString() + ", Minutes:Seconds");

            int hours = (nowE - nowS).Hours;
            int minutes = (nowE - nowS).Minutes;
            int seconds = (nowE - nowS).Seconds;
            int milliseconds = (nowE - nowS).Milliseconds;
            //loadLog("Test Time: " + minutes.ToString() + ":" + seconds.ToString() + ", Minutes:Seconds");
            loadLog("Test Time: " + minutes.ToString() + ":" + seconds.ToString() + ":" + milliseconds.ToString() + ", Minutes:Seconds:Millisconds");

            loadLog("");

            loadLog("Display Results " + lstIMarking01.Count.ToString() + " Images:");

            bool xNoTeting = false;
            if (!xNoTeting)
            {
                int imgIndex = 0;
                foreach (Dictionary<string, IMarking> item01 in lstIMarking01)
                {
                    Dictionary<string, IMarking> views01 = item01;   // lstIMarking01[0];

                    IMarking mm = views01["red_HDM_20M_5472x3648"];

                    ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];

                    if (redview.Regions.Count > 0)
                    {
                        ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                        RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
                        double[] score = new double[redview.Regions.Count];
                        int index = 0;

                        //lstGPU2log.Items.Add("Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());
                        invy.ListBoxaddItem(lstGPU2log, "Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());

                        foreach (ViDi2.IRegion item in redview.Regions)
                        {
                            regionFound[index].area = item.Area;
                            regionFound[index].width = item.Width;
                            regionFound[index].height = item.Height;
                            regionFound[index].center = item.Center;
                            regionFound[index].score = item.Score;
                            regionFound[index].className = "not possable to know in this application";  // item.Name; region name
                            regionFound[index].classColor = item.Color;
                            regionFound[index].compactness = item.Compactness;
                            regionFound[index].covers = item.Covers;
                            regionFound[index].outer = item.Outer;
                            regionFound[index].perimeter = item.Perimeter;

                            //lstGPU2log.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                            invy.ListBoxaddItem(lstGPU2log, (index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());
                            //regions[index] = item; //testing
                            score[index] = item.Score; //testing

                            index++;



                        }


                        Array.Sort(score);


                    }
                    else
                    {
                        //lstGPU2log.Items.Add("Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());
                        invy.ListBoxaddItem(lstGPU2log, "Image " + imgIndex.ToString() + " regions found: " + redview.Regions.Count.ToString());


                    }
                    imgIndex++;
                }
            }

        }
        private List<Dictionary<string, IMarking>> ThreadAction01(List<Tuple<string, string, int>> jobs01, int gpuId01, List<Tuple<string, string, int>> jobs02, int gpuId02, Dictionary<string, ViDi2.Runtime.IStream> StreamDict)
        {
            //var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

            bool xNoLog = true;

            Func<List<Tuple<string, string, int>>, int, int> ThreadAction = (jobs, gpuId) =>
            {
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                string ls, rs;
                string wsName, imgPath;
                int iterCount = 0;
                foreach (var job in jobs)
                {
                    wsName = job.Item1;
                    imgPath = job.Item2;
                    iterCount = job.Item3;

                    ls = (gpuId == 0) ? ("ENTER : " + wsName) : "...";
                    rs = (gpuId == 0) ? "..." : ("ENTER : " + wsName);
                    //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");
                    if (!xNoLog)
                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");

                    sw.Restart();
                    using (var img1 = new ViDi2.Local.LibraryImage(imgPath)) //needs to find a solution for this: load var img1 from memory
                        for (var iteration = 0; iteration < iterCount; ++iteration)
                        {
                            using (ISample sample = StreamDict[wsName].CreateSample(img1))
                            {
                                //sample.AddImage(img1);
                                // process all tools on stream with specific gpu(gpuId)
                                sample.Process(null, new List<int>() { gpuId });
                                lstIMarking.Add(sample.Markings); //Yoav 29-112023
                            }
                        }
                    sw.Stop();

                    bool xNoTsting = true;
                    if (!xNoTsting)
                    {
                        Dictionary<string, IMarking> views01 = lstIMarking[0];

                        IMarking mm = views01["red_HDM_20M_5472x3648"];
                        ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];
                        ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                        //IReadOnlyCollection<IView> rm = mm.Views; //OK but regions are not expose 
                        //IView rm = mm.Views[0];

                        //var views02 = lstIMarking[1];
                        //var vv = views02["red_HDM_20M_5472x3648"];

                        //ViDi2.IRedView redview = (ViDi2.IRedView)vv.Views[0]; //must be IReadView to get the regions

                        ViDi2.IRegion[] regions = new IRegion[redview.Regions.Count];
                        double[] score = new double[redview.Regions.Count];
                        int index = 0;
                        foreach (ViDi2.IRegion item in redview.Regions)
                        {
                            //regionFound[index].area = item.Area;
                            //regionFound[index].width = item.Width;
                            //regionFound[index].height = item.Height;
                            //regionFound[index].center = item.Center;
                            //regionFound[index].score = item.Score;
                            //regionFound[index].className = cn;  // item.Name; region name
                            //regionFound[index].classColor = item.Color;
                            //regionFound[index].compactness = item.Compactness;
                            //regionFound[index].covers = item.Covers;
                            //regionFound[index].outer = item.Outer;
                            //regionFound[index].perimeter = item.Perimeter;

                            //lstGreenToolResults.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());

                            regions[index] = item; //testing
                            score[index] = item.Score; //testing

                            index++;



                        }

                        Array.Sort(score);
                    }

                    ls = (gpuId == 0) ? ("EXIT  : " + wsName) : "...";
                    rs = (gpuId == 0) ? "..." : ("EXIT  : " + wsName);
                    //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");
                    if (!xNoLog)
                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                }

                if (!xNoLog)
                    loadLog($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");


                return gpuId;
            };

            var threads = new List<Task>();
            threads.Add(Task.Factory.StartNew(() => ThreadAction(jobs01, gpuId01)));
            threads.Add(Task.Factory.StartNew(() => ThreadAction(jobs02, gpuId02)));
            // wait for all tasks to finish
            Task.WaitAll(threads.ToArray());

            return lstIMarking;
        }
        private List<Dictionary<string, IMarking>> ThreadAction01MM(List<Tuple<string, IIMageFifo, int>> jobs01, int gpuId01, List<Tuple<string, IIMageFifo, int>> jobs02, int gpuId02, Dictionary<string, ViDi2.Runtime.IStream> StreamDict)
        {
            //var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

            bool xNoLog = true;

            Func<List<Tuple<string, IIMageFifo, int>>, int, int> ThreadAction = (jobs, gpuId) =>
            {
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                string ls, rs;
                string wsName;
                //IImage imgPath;
                int iterCount = 0;
                foreach (var job in jobs)
                {
                    wsName = job.Item1;
                    //imgPath = job.Item2;
                    iterCount = job.Item3;

                    ls = (gpuId == 0) ? ("ENTER : " + wsName) : "...";
                    rs = (gpuId == 0) ? "..." : ("ENTER : " + wsName);
                    //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");
                    if (!xNoLog)
                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");

                    sw.Restart();
                    //using (var img1 = imgPath) //new ViDi2.Local.LibraryImage(imgPath) //needs to find a solution for this: load var img1 from memory
                    for (var iteration = 0; iteration < iterCount; ++iteration)
                    {
                        using (ISample sample = StreamDict[wsName].CreateSample(job.Item2.iimage))   //img1))
                        {
                            //sample.AddImage(img1);
                            // process all tools on stream with specific gpu(gpuId)
                            sample.Process(null, new List<int>() { gpuId });
                            lstIMarking.Add(sample.Markings); //Yoav 29-112023
                        }
                    }
                    sw.Stop();

                    bool xNoTsting = true;
                    if (!xNoTsting)
                    {
                        Dictionary<string, IMarking> views01 = lstIMarking[0];

                        IMarking mm = views01["red_HDM_20M_5472x3648"];
                        ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];
                        ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                        //IReadOnlyCollection<IView> rm = mm.Views; //OK but regions are not expose 
                        //IView rm = mm.Views[0];

                        //var views02 = lstIMarking[1];
                        //var vv = views02["red_HDM_20M_5472x3648"];

                        //ViDi2.IRedView redview = (ViDi2.IRedView)vv.Views[0]; //must be IReadView to get the regions

                        ViDi2.IRegion[] regions = new IRegion[redview.Regions.Count];
                        double[] score = new double[redview.Regions.Count];
                        int index = 0;
                        foreach (ViDi2.IRegion item in redview.Regions)
                        {
                            //regionFound[index].area = item.Area;
                            //regionFound[index].width = item.Width;
                            //regionFound[index].height = item.Height;
                            //regionFound[index].center = item.Center;
                            //regionFound[index].score = item.Score;
                            //regionFound[index].className = cn;  // item.Name; region name
                            //regionFound[index].classColor = item.Color;
                            //regionFound[index].compactness = item.Compactness;
                            //regionFound[index].covers = item.Covers;
                            //regionFound[index].outer = item.Outer;
                            //regionFound[index].perimeter = item.Perimeter;

                            //lstGreenToolResults.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());

                            regions[index] = item; //testing
                            score[index] = item.Score; //testing

                            index++;



                        }

                        Array.Sort(score);
                    }

                    ls = (gpuId == 0) ? ("EXIT  : " + wsName) : "...";
                    rs = (gpuId == 0) ? "..." : ("EXIT  : " + wsName);
                    //Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");
                    if (!xNoLog)
                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");

                }

                if (!xNoLog)
                    loadLog($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");


                return gpuId;
            };

            var threads = new List<Task>();
            threads.Add(Task.Factory.StartNew(() => ThreadAction(jobs01, gpuId01)));
            threads.Add(Task.Factory.StartNew(() => ThreadAction(jobs02, gpuId02)));
            // wait for all tasks to finish
            Task.WaitAll(threads.ToArray());

            threads[0].Dispose();
            threads[1].Dispose();
            //threads.Remove(null);
            threads.Clear();

            return lstIMarking;
        }
        private List<Dictionary<string, IMarking>> ThreadAction01MMForFifo(List<Tuple<string, IIMageFifo, bool>> jobs01, int gpuId01, List<Tuple<string, IIMageFifo, bool>> jobs02, int gpuId02, Dictionary<string, ViDi2.Runtime.IStream> StreamDict)
        {
            //var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

            bool xNoLog = true;

            Func<List<Tuple<string, IIMageFifo, bool>>, int, int> ThreadAction = (jobs, gpuId) =>
            {
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                string ls, rs;
                string wsName;
                //IImage imgPath;
                //int iterCount = 0;
                foreach (var job in jobs)
                {
                    wsName = job.Item1;
                    //imgPath = job.Item2;
                    //iterCount = job.Item3;

                    ls = (gpuId == 0) ? ("ENTER : " + wsName) : "...";
                    rs = (gpuId == 0) ? "..." : ("ENTER : " + wsName);
                    
                    if (!xNoLog)
                        loadLog($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");

                    sw.Restart();
                    
                    if (job.Item2.xNewImage)
                    {
                        using (ISample sample = StreamDict[wsName].CreateSample(job.Item2.iimage))   //img1))
                        {
                            //sample.AddImage(img1);
                            // process all tools on stream with specific gpu(gpuId)
                            sample.Process(null, new List<int>() { gpuId });
                            lstIMarking.Add(sample.Markings); //Yoav 29-112023
                        }                        
                    }
                    sw.Stop();

                    bool xNoTsting = true;
                    if (!xNoTsting)
                    {
                        Dictionary<string, IMarking> views01 = lstIMarking[0];

                        IMarking mm = views01["red_HDM_20M_5472x3648"];
                        ViDi2.IRedView redview = (ViDi2.IRedView)mm.Views[0];
                        ViDi2.IRegion reg = redview.Regions[0];   //must be IReadView to get the regions

                        //IReadOnlyCollection<IView> rm = mm.Views; //OK but regions are not expose 
                        //IView rm = mm.Views[0];

                        //var views02 = lstIMarking[1];
                        //var vv = views02["red_HDM_20M_5472x3648"];

                        //ViDi2.IRedView redview = (ViDi2.IRedView)vv.Views[0]; //must be IReadView to get the regions

                        ViDi2.IRegion[] regions = new IRegion[redview.Regions.Count];
                        double[] score = new double[redview.Regions.Count];
                        int index = 0;
                        foreach (ViDi2.IRegion item in redview.Regions)
                        {
                            //regionFound[index].area = item.Area;
                            //regionFound[index].width = item.Width;
                            //regionFound[index].height = item.Height;
                            //regionFound[index].center = item.Center;
                            //regionFound[index].score = item.Score;
                            //regionFound[index].className = cn;  // item.Name; region name
                            //regionFound[index].classColor = item.Color;
                            //regionFound[index].compactness = item.Compactness;
                            //regionFound[index].covers = item.Covers;
                            //regionFound[index].outer = item.Outer;
                            //regionFound[index].perimeter = item.Perimeter;

                            //lstGreenToolResults.Items.Add((index + 1).ToString() + " Score: " + item.Score.ToString("0.00") + " Width: " + item.Width.ToString() + " Height: " + item.Height.ToString());

                            regions[index] = item; //testing
                            score[index] = item.Score; //testing

                            index++;



                        }

                        Array.Sort(score);
                    }                    

                }
               
                return gpuId;
            };

            var threads = new List<Task>();
            threads.Add(Task.Factory.StartNew(() => ThreadAction(jobs01, gpuId01)));
            threads.Add(Task.Factory.StartNew(() => ThreadAction(jobs02, gpuId02)));
            // wait for all tasks to finish
            Task.WaitAll(threads.ToArray());

            threads[0].Dispose();
            threads[1].Dispose();
            //threads.Remove(null);
            threads.Clear();

            return lstIMarking;
        }        
        private async Task<bool> StartTaske1()
        {

            //start from button on form

            // This method runs on the main thread.
            //this.WordsCounted.Text = "0";

            // Initialize the object that the background worker calls.             
            //BackroundWork.BKW_Def bw = new BKW_Def();

            //put this (as texts boxes) on the tab test backround worker
            try
            {
                xContinue = true;
                CancelAsync = false;
                //sget input parameters of worker
                string TaskToDo = "Task1"; ;
                int ParamIN1 = 1;
                long ParamIN2 = 2;
                float ParamIN3 = 3.1F;

                //run local method doing the requested task using input parameters
                
                CurrentState state = new CurrentState();
                var task1= Task.Run(()=>RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3, state));
                await task1;

                TaskToDo = "Task2"; ;
                ParamIN1 = 0;
                ParamIN2 = 0;
                ParamIN3 = 0;

                //run local method doing the requested task using input parameters
                var task2 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3, state));
                await task2;

                TaskToDo = "Task3"; 
                ParamIN1 = 0;
                ParamIN2 = 0;
                ParamIN3 = 0;

                //run local method doing the requested task using input parameters
                var task3 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3, state));
                await task3;

                TaskToDo = "Task4"; ;
                ParamIN1 = 0;
                ParamIN2 = 0;
                ParamIN3 = 0;

                //run local method doing the requested task using input parameters
                var task4 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3, state));
                await task4;

                TaskToDo = "Task5"; ;
                ParamIN1 = 0;
                ParamIN2 = 0;
                ParamIN3 = 0;

                //run local method doing the requested task using input parameters
                var task5 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3, state));
                await task5;
                if (state.CurrentStation < 7)
                    TaskToDo = "Task3"; 
                else TaskToDo = "Task6";
                ParamIN1 = 0;
                ParamIN2 = 0;
                ParamIN3 = 0;

                //run local method doing the requested task using input parameters
                var task6 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3, state));
                await task6;
                return true;

                


            }
            catch (System.Exception ex) { return false; }

        }
        private void EndAutoCycle(bool std, string DispMsg)
        {
            //lblTaskError.Text = "";
            //CycleNotAbort = true;
            //LockControlAuto(false);
            //lblInCycle.BackColor = Color.Gray;
            //lblInCycle.Refresh();
            if (std)
            {
                DateTime nowE = DateTime.Now;
                string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", nowE);
                //lstGPU2log.Items.Add(timenow14 + ": End of auto cycle" + DispMsg);
                invy.ListBoxaddItem(lstGPU2log, timenow14 + ": End of auto cycle" + DispMsg);
            }
        }
        private bool NoMoreToMark(int StationsToMark, int CurrentStations)
        {
            bool xNoMoreToMark = false;

            for (int i = CurrentStations; i < 7; i++)
            {
                int intbit = Convert.ToInt32(Math.Pow(2, i));
                int results = StationsToMark & intbit;
                if (results > 0) { xNoMoreToMark = false; goto ExitFor; } else { xNoMoreToMark = true; }

            }

        ExitFor:;
            return xNoMoreToMark;

        }
        private int MarkingOnly(ref string ErrMsg)
        {
            ErrMsg = "";



            return 0;
        }
        private int GetLocation()
        {
            //throw new NotImplementedException();

            return 0;
        }                
        private int loadIImageBufferAsnc01Pic(int numImages2Load)
        {

            //PictureBox[] pictureBoxes = new PictureBox[16];

            //for (int i = 0; i < numImages2Load; i++)
            //{
                DateTime nowS = DateTime.Now;
                string timenow14 = string.Format("{0:ddMMyy_HHmmss.fff}", nowS);
                Bitmap btm = null;

            //workout load control
                int iImagesLoaded = 0;
                //iLastLoadingPicIndex = iLastLoadingPicIndex + numImages2Load;
                for (int i = iLastLoadingPicIndex; i < iLastLoadingPicIndex + numImages2Load; i++)
                {
                    loadingQueue.loadPic[i] = true;
                    //System.Diagnostics.Debug.WriteLine("load indexes: " + i.ToString() + " numImages2Load: " + numImages2Load.ToString());
                }
            //iLastLoadingPicIndex=pointer to picture box
            //iLastLoadingPicIndex=how many pictures already done
            iLastLoadingPicIndex = iLastLoadingPicIndex + numImages2Load;
                iLastLoadingPicIndexTrue = iLastLoadingPicIndexTrue + 1;

                if (!arrayOfViDi2IIamge[0].xNewImage && loadingQueue.loadPic[0])
                {
                    loadingQueue.loadPic[0] = false;
                    btm = null;
                    int cnt = 0;
                    while (btm == null && cnt < 3)
                    {
                        try
                        {
                        Thread.Sleep(100);//delay 500
                                btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct1.Image.Clone();
                        }
                        catch { Thread.Sleep(500); cnt++; }
                    }
                    if (cnt >= 3)
                    {
                        return 1;
                    }
                    arrayOfViDi2IIamge[0].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[0].xNewImage = true;
                    arrayOfViDi2IIamge[0].imageName = timenow14 + "_pic_1";
                    arrayOfViDi2IIamge[0].imageIndex = 0;
                    iImagesLoaded++;
                }
                
                if (!arrayOfViDi2IIamge[1].xNewImage && loadingQueue.loadPic[1])
                {
                    loadingQueue.loadPic[1] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct2.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 2;
                }
                arrayOfViDi2IIamge[1].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[1].xNewImage = true;
                    arrayOfViDi2IIamge[1].imageName = timenow14 + "_pic_2";
                    arrayOfViDi2IIamge[1].imageIndex = 1;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[2].xNewImage && loadingQueue.loadPic[2])
                {
                    loadingQueue.loadPic[2] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct3.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 3;
                }
                arrayOfViDi2IIamge[2].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[2].xNewImage = true;
                    arrayOfViDi2IIamge[2].imageName = timenow14 + "_pic_3";
                    arrayOfViDi2IIamge[2].imageIndex = 2;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[3].xNewImage && loadingQueue.loadPic[3])
                {
                    loadingQueue.loadPic[3] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct4.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 4;
                }
                arrayOfViDi2IIamge[3].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[3].xNewImage = true;
                    arrayOfViDi2IIamge[3].imageName = timenow14 + "_pic_4";
                    arrayOfViDi2IIamge[3].imageIndex = 3;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[4].xNewImage && loadingQueue.loadPic[4])
                {
                    loadingQueue.loadPic[4] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct5.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 5;
                }
                arrayOfViDi2IIamge[4].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[4].xNewImage = true;
                    arrayOfViDi2IIamge[4].imageName = timenow14 + "_pic_5";
                    arrayOfViDi2IIamge[4].imageIndex = 4;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[5].xNewImage && loadingQueue.loadPic[5])
                {
                    loadingQueue.loadPic[5] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct6.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 6;
                }
                arrayOfViDi2IIamge[5].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[5].xNewImage = true;
                    arrayOfViDi2IIamge[5].imageName = timenow14 + "_pic_6";
                    arrayOfViDi2IIamge[5].imageIndex = 5;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[6].xNewImage && loadingQueue.loadPic[6])
                {
                    loadingQueue.loadPic[6] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct7.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 7;
                }
                arrayOfViDi2IIamge[6].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[6].xNewImage = true;
                    arrayOfViDi2IIamge[6].imageName = timenow14 + "_pic_7";
                    arrayOfViDi2IIamge[6].imageIndex = 6;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[7].xNewImage && loadingQueue.loadPic[7])
                {
                    loadingQueue.loadPic[7] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct8.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 8;
                }
                arrayOfViDi2IIamge[7].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[7].xNewImage = true;
                    arrayOfViDi2IIamge[7].imageName = timenow14 + "_pic_8";
                    arrayOfViDi2IIamge[7].imageIndex = 7;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[8].xNewImage && loadingQueue.loadPic[8])
                {
                    loadingQueue.loadPic[8] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct9.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 9;
                }
                arrayOfViDi2IIamge[8].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[8].xNewImage = true;
                    arrayOfViDi2IIamge[8].imageName = timenow14 + "_pic_9";
                    arrayOfViDi2IIamge[8].imageIndex = 8;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[9].xNewImage && loadingQueue.loadPic[9])
                {
                    loadingQueue.loadPic[9] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct10.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 10;
                }
                arrayOfViDi2IIamge[9].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[9].xNewImage = true;
                    arrayOfViDi2IIamge[9].imageName = timenow14 + "_pic_10";
                    arrayOfViDi2IIamge[9].imageIndex = 9;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[10].xNewImage && loadingQueue.loadPic[10])
                {
                    loadingQueue.loadPic[10] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct11.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 11;
                }
                arrayOfViDi2IIamge[10].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[10].xNewImage = true;
                    arrayOfViDi2IIamge[10].imageName = timenow14 + "_pic_11";
                    arrayOfViDi2IIamge[10].imageIndex = 10;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[11].xNewImage && loadingQueue.loadPic[11])
                {
                    loadingQueue.loadPic[11] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct12.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 12;
                }
                arrayOfViDi2IIamge[11].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[11].xNewImage = true;
                    arrayOfViDi2IIamge[11].imageName = timenow14 + "_pic_12";
                    arrayOfViDi2IIamge[11].imageIndex = 11;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[12].xNewImage && loadingQueue.loadPic[12])
                {
                    loadingQueue.loadPic[12] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct13.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 13;
                }
                arrayOfViDi2IIamge[12].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[12].xNewImage = true;
                    arrayOfViDi2IIamge[12].imageName = timenow14 + "_pic_13";
                    arrayOfViDi2IIamge[12].imageIndex = 12;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[13].xNewImage && loadingQueue.loadPic[13])
                {
                    loadingQueue.loadPic[13] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct14.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 14;
                }
                arrayOfViDi2IIamge[13].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[13].xNewImage = true;
                    arrayOfViDi2IIamge[13].imageName = timenow14 + "_pic_14";
                    arrayOfViDi2IIamge[13].imageIndex = 13;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[14].xNewImage && loadingQueue.loadPic[14])
                {
                    loadingQueue.loadPic[14] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct15.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 15;
                }
                arrayOfViDi2IIamge[14].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[14].xNewImage = true;
                    arrayOfViDi2IIamge[14].imageName = timenow14 + "_pic_15";
                    arrayOfViDi2IIamge[14].imageIndex = 14;
                    iImagesLoaded++;
                }

                if (!arrayOfViDi2IIamge[15].xNewImage && loadingQueue.loadPic[15])
                {
                    loadingQueue.loadPic[15] = false;
                int cnt = 0;
                while (btm == null && cnt < 3)
                {
                    try
                    {
                        btm = (Bitmap)frmBeckhoff.mFormBeckhoffDefInstance.pct16.Image.Clone();
                    }
                    catch { Thread.Sleep(500); cnt++; }
                }
                if (cnt >= 3)
                {
                    return 16;
                }
                arrayOfViDi2IIamge[15].iimage = Bitmap2ViDi2ByteImage(btm);
                    arrayOfViDi2IIamge[15].xNewImage = true;
                    arrayOfViDi2IIamge[15].imageName = timenow14 + "_pic_16";
                    arrayOfViDi2IIamge[15].imageIndex = 15;
                    iImagesLoaded++;
                }

                System.Diagnostics.Debug.WriteLine("iImages Loaded: " + iImagesLoaded.ToString());
            return 0;

                //arrayOfViDi2IIamge[i].imageName = timenow14 + "_pic_" + (i + 1).ToString();
                //arrayOfViDi2IIamge[i].imageIndex = i;
                //arrayOfViDi2IIamge[i].xNewImage = true;
                //}
        }
        #endregion

        #region------------------------Properties----------------------------



        #endregion

        #region---------------Methods For Events, Delegates------------------
        private void frmMain_Load(object sender, EventArgs e)
        {
            try
            {


                this.Text = "RuntimeMultiGPU2, inspmachha, " + version;

                jgpEncoder = GetEncoder(System.Drawing.Imaging.ImageFormat.Jpeg);

                myEncoderParameters = new EncoderParameters(1);
                EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, 95L);
                myEncoderParameters.Param[0] = myEncoderParameter;

                string INIpath = Application.StartupPath + @"\Data\Models.ini";
                gmodels = getModels(INIpath);

                txtModel1.Text = gmodels.model1FileName.Trim();
                txtModel2.Text = gmodels.model2FileName.Trim();

                chkSingleGpu.Checked = false;
                chkSingleGpu.Visible = false;

                //----------------------Background task set events---------------------                                                                                         

                //backround worker events from class P47859_IO.cs
                #region Backround task init


                //BackRoundTasks.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.BackRoundTasks_ProgressChanged);
                //BackRoundTasks.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BackRoundTasks_RunWorkerCompleted);

                #endregion


                #region Backround task init
                //this.BackRoundTasks.WorkerReportsProgress = true;
                //this.BackRoundTasks.WorkerSupportsCancellation = true;
                //this.BackRoundTasks.DoWork += new System.ComponentModel.DoWorkEventHandler(this.BackRoundTasks_DoWork);               

                //loading queue init
                loadingQueue.loadPic = new bool[20];


                #endregion
            }
            catch(System.Exception e1)
            {
                MessageBox.Show("Error Loading Evaluator Application: " + e1.Message);
            }
        }
        private void btnTest_Click(object sender, EventArgs e)
        {
            //var task = Task.Run(() =>
            //            funcRun());
        }
        private void btnTest2_Click(object sender, EventArgs e)
        {
            //loadLog("Start GPU2 Test");

            Application.DoEvents();

            OriginalAsGPU2();

            //loadLog("End GPU2 Test");

            MessageBox.Show("Done GPU2 Run");
        }   
        private void btnTest3_Click(object sender, EventArgs e)
        {

            unloadBuffer();

            Application.DoEvents();

            OriginalAsGPU2NoMultiThreading();

            loadLog("End GPU2 Test");

            MessageBox.Show("Done GPU2 Run File Loading On The Fly");
        }
        public async void BackRoundTasks_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //BackroundWork.BKW_Def.CurrentState state = (BackroundWork.BKW_Def.CurrentState)e.UserState;
            return;
            CurrentState state = (CurrentState)e.UserState;

            //testing only
            //label12.Text = state.Msg;
            if (state.ErrOK)
            {
                //label13.Text = "ErrOK = true";
                DateTime nowE = DateTime.Now;
                string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss.fff}", nowE);
                loadLog(timenow14 + ": Progress report: " + "ErrOK = true, " + state.Msg);
                //lstGPU2log.Refresh();
            }
            else
            {
                //label13.Text = "ErrOK = false";
                DateTime nowE = DateTime.Now;
                string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss.fff}", nowE);
                loadLog(timenow14 + ": Progress report: " + "ErrOK = false, " + state.Msg);
                //lstGPU2log.Refresh();

                btnTest8.Enabled = true;
            }

            //lstGPU2log.SelectedIndex = lstGPU2log.Items.Count - 1;
            //lstGPU2log.ClearSelected();
            //lstGPU2log.Refresh();

            //manitor and act on main cycle
            string stdreportEvaluation = "Evaluation Cycle: ";
            if (state.TaskNumber == 3 && state.Msg.Substring(0,18) == stdreportEvaluation)      //display marking async            
            {
                //var task = Task.Run(() =>
                //            DisplayMarkingList(state.MarkingsList));

                var task = Task.Run(() =>
                            DisplayMarkingListFIN(state.multiResuls));
                await task;


            } 
        }
        public async void ProgressChangedAsync(CurrentState state)
        {
            //BackroundWork.BKW_Def.CurrentState state = (BackroundWork.BKW_Def.CurrentState)e.UserState;

            //CurrentState state = (CurrentState)e.UserState;

            //testing only
            //label12.Text = state.Msg;
            try
            {
                if (state.ErrOK)
                {
                    //label13.Text = "ErrOK = true";
                    DateTime nowE = DateTime.Now;
                    string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss.fff}", nowE);
                    loadLog(timenow14 + ": Progress report: " + "ErrOK = true, " + state.Msg);
                    //lstGPU2log.Refresh();
                }
                else
                {
                    //label13.Text = "ErrOK = false";
                    DateTime nowE = DateTime.Now;
                    string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss.fff}", nowE);
                    loadLog(timenow14 +": Progress report: " + "ErrOK = false, " + state.Msg);
                    //lstGPU2log.Refresh();

                    //btnTest8.Enabled = true;
                }

                //lstGPU2log.SelectedIndex = lstGPU2log.Items.Count - 1;
                //lstGPU2log.ClearSelected();
                //lstGPU2log.Refresh();

                //manitor and act on main cycle
                string stdreportEvaluation = "Evaluation Cycle: ";
                if (state.TaskNumber == 3 && state.Msg.Substring(0, 18) == stdreportEvaluation)      //display marking async            
                {
                    //var task = Task.Run(() =>
                    //            DisplayMarkingList(state.MarkingsList));

                    var task = Task.Run(() =>
                                DisplayMarkingListFIN(state.multiResuls));

                    await task;
                }
                //Thread.Sleep(50);
                //return true;
            }
            catch (System.Exception ex)  { 
                //return false; 
            }
        }
        //public void BackRoundTasks_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        //{
        //    // This event handler is called when the background thread finishes.
        //    // This method runs on the main thread.

        //    bool debug = true;

        //    BackroundWork.BKW_Def bw = new BKW_Def();

        //    CurrentState state = null;

        //    if (!e.Cancelled)
        //    {
        //        state = (CurrentState)e.Result;

        //        if (state.ErrOK)
        //        {
        //            switch (state.TaskNumber)
        //            {
        //                case 1: //End of: Image Buffer Loading
        //                        //label13.Text = "ErrOK = true";
        //                    DateTime nowE = DateTime.Now;
        //                    string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", nowE);

        //                    if (!state.ErrOK)
        //                    {
        //                        //lstGPU2log.Items.Add(timenow14 + ": End Image Buffer Loading with Error: " + state.Msg);
        //                        invy.ListBoxaddItem(lstGPU2log, timenow14 + ": End Image Buffer Loading with Error: " + state.Msg);
        //                        //lblTaskError.Text = state.Msg;
        //                        //this.EndAutoCycle(false, "");
        //                    }
        //                    else
        //                    {
        //                        //lstGPU2log.Items.Add(timenow14 + ": Run Completed, End Image Buffer Loading Successfully: "); // + state.Msg);
        //                        invy.ListBoxaddItem(lstGPU2log, timenow14 + ": Run Completed, End Image Buffer Loading Successfully: "); // + state.Msg);

        //                        //lstGPU2log.Items.Add(timenow14 + ": Run Completed, Start initializing first Fifo run");
        //                        invy.ListBoxaddItem(lstGPU2log, timenow14 + ": Run Completed, Start initializing first Fifo run");
        //                        //lstGPU2log.Refresh();
        //                        invy.ListBoxPerformRefresh(lstGPU2log);
        //                        System.Threading.Thread.Sleep(100);
        //                        // if all ok next step
        //                        bw = new BKW_Def();
        //                        //backround worker
        //                        bw.ParamsIN.sTasklName = "Task2";       //initializing first Fifo run
        //                        // Start the asynchronous operation.
        //                        BackRoundTasks.RunWorkerAsync(bw);
        //                    }
        //                    break;

        //                case 2: //run Fifo-evaluator
        //                    bw = new BKW_Def();
        //                    //backround worker
        //                    bw.ParamsIN.sTasklName = "Task3";       //run Fifo-evaluator
        //                                                            // Start the asynchronous operation.
        //                    BackRoundTasks.RunWorkerAsync(bw);




        //                    break;

        //                case 3: //End of: Cycle up to turn lights ON


        //                    goto bypass;

        //                    //check if this station is to be marked
        //                    if (!state.NextStation)
        //                    {
        //                        //task3 end ok, call take picture

        //                        //1
        //                        int rc = GetLocation();     //empty in this project

        //                        if (rc == 0)
        //                        {
        //                            //2 - start task4
        //                            BackroundWork.BKW_Def bw4 = new BKW_Def();

        //                            //Turn lights off, and raised laser head up
        //                            bw4.ParamsIN.sTasklName = "Task4";

        //                            // Start the asynchronous operation task4.
        //                            BackRoundTasks.RunWorkerAsync(bw4);
        //                        }
        //                        else
        //                        {
        //                            string ErrMsg1 = "";
        //                            if (rc == -1)
        //                                ErrMsg1 = "Ret.code: snap error";
        //                            else if (rc == -2)
        //                                ErrMsg1 = "Ret.code: vision in use";
        //                            else if (rc == -3)
        //                                ErrMsg1 = "Ret.code: picture is null";
        //                            else if (rc == -4)
        //                                ErrMsg1 = "catch (Exception e1)";
        //                            else if (rc == -5)
        //                                ErrMsg1 = "missing data";
        //                            else
        //                                ErrMsg1 = "Ret.code: unknown error";

        //                            //lstGPU2log.Items.Add("End GetLocation with error: " + ErrMsg1 + ", " + state.Msg);
        //                            invy.ListBoxaddItem(lstGPU2log, "End GetLocation with error: " + ErrMsg1 + ", " + state.Msg);
        //                            //lstGPU2log.Items.Add(DateTime.Now.ToString() + ": End cycle with error, " + ErrMsg1 + ", " + state.Msg);
        //                            invy.ListBoxaddItem(lstGPU2log, DateTime.Now.ToString() + ": End cycle with error, " + ErrMsg1 + ", " + state.Msg);
        //                            //lblTaskError.Text = "End GetLocation with error: " + ErrMsg1 + ", " + state.Msg;
        //                        } //end if (rc == 0)
        //                    }
        //                    else
        //                    {
        //                        if (!NoMoreToMark(this.StationsToMark, state.CurrentStation))
        //                        {
        //                            //current station not to be marked, move next
        //                            BackroundWork.BKW_Def bw3 = new BKW_Def();
        //                            //move to next marking station
        //                            bw3.ParamsIN.sTasklName = "Task3";

        //                            // Start the asynchronous operation.
        //                            BackRoundTasks.RunWorkerAsync(bw3);
        //                        }
        //                        else  //end of cycle
        //                        {
        //                            //send conveyer home
        //                            //send conveyer home (task6)
        //                            //2 - start task6
        //                            BackroundWork.BKW_Def bw6 = new BKW_Def();

        //                            //backround worker
        //                            bw6.ParamsIN.sTasklName = "Task6";       //send conveyer back home

        //                            // Start the asynchronous operation task6.
        //                            BackRoundTasks.RunWorkerAsync(bw6);

        //                        }

        //                    }

        //                bypass:;
        //                    break;

        //                case 4: ////End of: Turn lights off, and raised laser head up

        //                    //mark tools here
        //                    string ErrMsg = "";
        //                    int rc1 = this.MarkingOnly(ref ErrMsg);  //call here marking procedure (Fadi's procedure).
        //                    if (rc1 == 0)
        //                    {
        //                        //2 - start task5
        //                        BackroundWork.BKW_Def bw5 = new BKW_Def();

        //                        //put this (as texts boxes) on the tab test backround worker
        //                        bw5.ParamsIN.sTasklName = "Task5";       //check move to first marking station

        //                        // Start the asynchronous operation task4.
        //                        BackRoundTasks.RunWorkerAsync(bw5);
        //                    }
        //                    else
        //                    {
        //                        //display error to log, and end cycle with error
        //                    }

        //                    //1. call here marking procedure (Fadi's procedure). 

        //                    //2. After coming back call 'lower laser head' task5


        //                    break;

        //                case 5: ////End of: Lower laser head

        //                    //1. check bin number 
        //                    //2. if bin number = 6 (0 -->6), last one then end of cycle, else next bin
        //                    if (state.CurrentStation < 7)   //incroment after move, so end of station 5, move to last station to6
        //                    {

        //                        BackroundWork.BKW_Def bw3 = new BKW_Def();

        //                        //put this (as texts boxes) on the tab test backround worker
        //                        bw3.ParamsIN.sTasklName = "Task3";       //check move to next marking station

        //                        // Start the asynchronous operation.
        //                        BackRoundTasks.RunWorkerAsync(bw3);
        //                    }
        //                    else
        //                    {
        //                        //send conveyer home (task6)
        //                        //2 - start task6
        //                        BackroundWork.BKW_Def bw6 = new BKW_Def();

        //                        //backround worker
        //                        bw6.ParamsIN.sTasklName = "Task6";       //send conveyer back home

        //                        // Start the asynchronous operation task6.
        //                        BackRoundTasks.RunWorkerAsync(bw6);

        //                    }
        //                    break;


        //                case 6: ////End of: send conveyer back home if end of cycle  

        //                    //end of cycle
        //                    string Msg = "";
        //                    EndAutoCycle(true, Msg);
        //                    break;


        //                default:
        //                    MessageBox.Show("Error task number: " + state.TaskNumber.ToString());
        //                    break;
        //            } //end tasks monitoring (select case)
        //        }
        //        else
        //        {
        //            DateTime now = DateTime.Now;
        //            string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", now);
        //            loadLog(timenow14 + ": End with task: " + state.TaskNumber + ", error: " + state.Msg);

        //            //lblTaskError.Text = state.Msg;
        //            this.EndAutoCycle(true, "");

        //        } //end if (state.ErrOK)

        //    }// e.Canceled
        //    else
        //    {
        //        string Msg = ", user aborted";
        //        this.EndAutoCycle(true, Msg);

        //        debug = false;
        //        if (debug)
        //        {
        //            //lstGPU2log.Items.Add(DateTime.Now.ToString() + ": Backround task: " + state.TaskNumber + " FinishedOK");
        //            invy.ListBoxaddItem(lstGPU2log, DateTime.Now.ToString() + ": Backround task: " + state.TaskNumber + " FinishedOK");
        //            //lblTaskError.Text = state.Msg;
        //        }
        //    }


        //    //if (e.Error != null)
        //    //    MessageBox.Show("Task " + state.TaskNumber + " end with error: " + e.Error.Message);
        //    //else if (e.Cancelled)
        //    //    //MessageBox.Show("Task " + state.TaskNumber + " canceled");
        //    //else
        //    //{

        //    //    //if (debug)
        //    //    //{
        //    //    //    this.lstAutoCycleLog.Items.Add(DateTime.Now.ToString() + ": Backround task: " + state.TaskNumber + " FinishedOK");
        //    //    //    lblTaskError.Text = state.Msg;
        //    //    //}
        //    //}
        //}
        public async Task<bool> BackRoundTasks_RunWorkerCompletedAsync(CurrentState state, bool Cancelled)
        {
            // This event handler is called when the background thread finishes.
            // This method runs on the main thread.

            bool debug = true;
            int ParamIN1 = 0;
            long ParamIN2 =0;
            float ParamIN3 = 0;
            string TaskToDo = "";

            //BackroundWork.BKW_Def bw = new BKW_Def();

            //CurrentState state = null;

            if (!Cancelled)
            {
                //state = (CurrentState)e.Result;

                if (state.ErrOK)
                {
                    switch (state.TaskNumber)
                    {
                        case 1: //End of: Image Buffer Loading
                                //label13.Text = "ErrOK = true";
                            DateTime nowE = DateTime.Now;
                            string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", nowE);

                            if (!state.ErrOK)
                            {
                               
                                invy.ListBoxaddItem(lstGPU2log, timenow14 + ": End Image Buffer Loading with Error: " + state.Msg);
                                //lblTaskError.Text = state.Msg;
                                //this.EndAutoCycle(false, "");
                            }
                            else
                            {
                                invy.ListBoxaddItem(lstGPU2log, timenow14 + ": Run Completed, End Image Buffer Loading Successfully: "); // + state.Msg);
                                invy.ListBoxaddItem(lstGPU2log, timenow14 + ": Run Completed, Start initializing first Fifo run");
                                
                                invy.ListBoxPerformRefresh(lstGPU2log);
                                System.Threading.Thread.Sleep(100);
                                // if all ok next step
                               
                                //TaskToDo = "Task2";
                                //ParamIN1 = 0;
                                //ParamIN2 = 0;
                                //ParamIN3 = 0;

                                //var task7 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3,  state));
                                //await task7;
                            }
                            break;

                        case 2: //run Fifo-evaluator
                                   //run Fifo-evaluator
                                   // Start the asynchronous operation.
                             //TaskToDo = "Task3";
                             //ParamIN1 = 0;
                             //ParamIN2 = 0;
                             //ParamIN3 = 03;

                            //var task = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3,  state));
                            //await task;
                            //BackRoundTasks.RunWorkerAsync(bw);




                            break;

                        case 3: //End of: Cycle up to turn lights ON


                            goto bypass;

                            //check if this station is to be marked
                            if (!state.NextStation)
                            {
                                //task3 end ok, call take picture

                                //1
                                int rc = GetLocation();     //empty in this project

                                if (rc == 0)
                                {
                                    //2 - start task4
                                    
                                    //TaskToDo = "Task4";
                                    //ParamIN1 = 0;
                                    //ParamIN2 = 0;
                                    //ParamIN3 = 0;

                                    //var task1 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3,  state));
                                    //await task1;
                                }
                                else
                                {
                                    string ErrMsg1 = "";
                                    if (rc == -1)
                                        ErrMsg1 = "Ret.code: snap error";
                                    else if (rc == -2)
                                        ErrMsg1 = "Ret.code: vision in use";
                                    else if (rc == -3)
                                        ErrMsg1 = "Ret.code: picture is null";
                                    else if (rc == -4)
                                        ErrMsg1 = "catch (Exception e1)";
                                    else if (rc == -5)
                                        ErrMsg1 = "missing data";
                                    else
                                        ErrMsg1 = "Ret.code: unknown error";

                                    
                                    invy.ListBoxaddItem(lstGPU2log, "End GetLocation with error: " + ErrMsg1 + ", " + state.Msg);
                                    
                                    invy.ListBoxaddItem(lstGPU2log, DateTime.Now.ToString() + ": End cycle with error, " + ErrMsg1 + ", " + state.Msg);
                                    
                                } //end if (rc == 0)
                            }
                            else
                            {
                                if (!NoMoreToMark(this.StationsToMark, state.CurrentStation))
                                {
                                    //current station not to be marked, move next
                                    
                                    //TaskToDo = "Task3";
                                    //ParamIN1 = 0;
                                    //ParamIN2 = 0;
                                    //ParamIN3 = 0;

                                    //var task2 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3, state));
                                    //await task2;
                                }
                                else  //end of cycle
                                {
                                    //send conveyer home
                                    //send conveyer home (task6)
                                                                       

                                    // Start the asynchronous operation task6.
                                    //BackRoundTasks.RunWorkerAsync(bw6);
                                    //TaskToDo = "Task6";
                                    //ParamIN1 = 0;
                                    //ParamIN2 = 0;
                                    //ParamIN3 = 0;

                                    //var task3 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3,  state));
                                    //await task3;

                                }

                            }

                        bypass:;
                            break;

                        case 4: ////End of: Turn lights off, and raised laser head up

                            //mark tools here
                            string ErrMsg = "";
                            int rc1 = this.MarkingOnly(ref ErrMsg);  //call here marking procedure (Fadi's procedure).
                            if (rc1 == 0)
                            {
                                //2 - start task5
                                //BackroundWork.BKW_Def bw5 = new BKW_Def();

                                //put this (as texts boxes) on the tab test backround worker
                                // Start the asynchronous operation task4.
                                //BackRoundTasks.RunWorkerAsync(bw5);
                                //TaskToDo = "Task5";
                                //ParamIN1 = 0;
                                //ParamIN2 = 0;
                                //ParamIN3 = 0;

                                //var task4 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3,  state));
                                //await task4;
                            }
                            else
                            {
                                //display error to log, and end cycle with error
                            }

                            //1. call here marking procedure (Fadi's procedure). 

                            //2. After coming back call 'lower laser head' task5


                            break;

                        case 5: ////End of: Lower laser head

                            //1. check bin number 
                            //2. if bin number = 6 (0 -->6), last one then end of cycle, else next bin
                            if (state.CurrentStation < 7)   //incroment after move, so end of station 5, move to last station to6
                            {

                                //put this (as texts boxes) on the tab test backround worker
                                //check move to next marking station

                                // Start the asynchronous operation.
                               
                                //TaskToDo = "Task3";
                                //ParamIN1 = 0;
                                //ParamIN2 = 0;
                                //ParamIN3 = 0;

                                //var task5 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3,  state));
                                //await task5;
                            }
                            else
                            {
                                //send conveyer home (task6)
                                //2 - start task6
                                //send conveyer back home

                                // Start the asynchronous operation task6.
                                //BackRoundTasks.RunWorkerAsync(bw6);
                                //TaskToDo = "Task6";
                                //ParamIN1 = 0;
                                //ParamIN2 = 0;
                                //ParamIN3 = 0;

                                //var task6 = Task.Run(() => RunEvaluationTasksAsync(TaskToDo, ParamIN1, ParamIN2, ParamIN3,  state));
                                //await task6;


                            }
                            break;


                        case 6: ////End of: send conveyer back home if end of cycle  

                            //end of cycle
                            string Msg = "";
                            EndAutoCycle(true, Msg);
                            break;


                        default:
                            MessageBox.Show("Error task number: " + state.TaskNumber.ToString());
                            break;
                    } //end tasks monitoring (select case)
                }
                else
                {
                    DateTime now = DateTime.Now;
                    string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", now);
                    loadLog(timenow14 + ": End with task: " + state.TaskNumber + ", error: " + state.Msg);

                    this.EndAutoCycle(true, "");

                } //end if (state.ErrOK)

            }// e.Canceled
            else
            {
                string Msg = ", user aborted";
                this.EndAutoCycle(true, Msg);

                debug = false;
                if (debug)
                {
                    
                    invy.ListBoxaddItem(lstGPU2log, DateTime.Now.ToString() + ": Backround task: " + state.TaskNumber + " FinishedOK");
                    
                }
            }

            return true;
            //if (e.Error != null)
            //    MessageBox.Show("Task " + state.TaskNumber + " end with error: " + e.Error.Message);
            //else if (e.Cancelled)
            //    //MessageBox.Show("Task " + state.TaskNumber + " canceled");
            //else
            //{

            //    //if (debug)
            //    //{
            //    //    this.lstAutoCycleLog.Items.Add(DateTime.Now.ToString() + ": Backround task: " + state.TaskNumber + " FinishedOK");
            //    //    lblTaskError.Text = state.Msg;
            //    //}
            //}
        }
        private void btnTest5_Click(object sender, EventArgs e)
        {
            //
            if (control != null)
            {
                control.Dispose();
                control = null;
            }
            //
            string imagesPath00 = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";

            string imagesPath = "";
            int numImages2Load = -1;

            if (!chkLargeJPG.Checked)
            {
                imagesPath = imagesPath00 + @"resources\images\Iscar";
                numImages2Load = -1;
            }
            else
            {
                imagesPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\Resources\images\Iscar\PROJ_403";
                numImages2Load = 11;
            }



            //load test
            //ArrayOfByteArray
            loadIImageBuffer(imagesPath, numImages2Load);

            //  
            Application.DoEvents();

            OriginalAsGPU2NoMultiThreadingMM(arrayOfViDi2IIamge);

            loadLog("End GPU2 Test");

            MessageBox.Show("Done GPU2 Run Image Loading Buffer");
        }
        private void frmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            //BackRoundTasks.CancelAsync();
            //System.Threading.Thread.Sleep(2000); don't work
            CancelAsync = true;
            Thread.Sleep(1000);
            closeControl();
        }
        private void closeControl()
        {
            if (control != null)
            {
                control.Dispose();
                control = null;
            }

            int i = 0;
            foreach (IIMageFifo item in arrayOfViDi2IIamge)
            {
                if (item.iimage != null)
                {
                    item.iimage.Dispose();
                    arrayOfViDi2IIamge[i].iimage = null;
                }

                i++;
            }
        }
        private void unloadBuffer()
        {
            int i = 0;
            foreach (IIMageFifo item in arrayOfViDi2IIamge)
            {
                if (item.iimage != null)
                {
                    item.iimage.Dispose();
                    arrayOfViDi2IIamge[i].iimage = null;
                }

                i++;
            }

        }
        private void btnTest6_Click(object sender, EventArgs e)
        {
            bool xPR403 = true;
            if (!xPR403)
            {
                string pathWhite = @"D:\Cognex_Endmill\White_5472x3648.jpg";
                string pathBlack = @"D:\Cognex_Endmill\Black_5472x3648.jpg";

                Bitmap bmpWhite = new Bitmap(pathWhite);

                Bitmap bmpBlack = new Bitmap(pathBlack);

                bmpWhite.Dispose();

                bmpBlack.Dispose();
            }
            else
            {
                string proj403Path = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\Resources\images\Iscar\PROJ_403\0001.jpg";

                Bitmap bmpProj403 = new Bitmap(proj403Path);

                bmpProj403.Dispose();
            }
        }
        private void btnTest7_Click(object sender, EventArgs e)
        {
            bool xForFifo = chkFifoMode.Checked;

            if (!xInitDone)
            {
                if (control != null)
                {
                    control.Dispose();
                    control = null;
                }

                string imagesPath00 = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";

                string imagesPath = "";
                int numImages2Load = -1;

                if (!chkLargeJPG.Checked)
                {
                    imagesPath = imagesPath00 + @"resources\images\Iscar";
                    numImages2Load = -1;
                }
                else
                {
                    imagesPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\Resources\images\Iscar\PROJ_403";
                    numImages2Load = 11;
                }



                //load test
                //ArrayOfByteArray
                loadIImageBuffer(imagesPath, numImages2Load);

                //
                if (!xForFifo)
                    OriginalAsGPU2NoMultiThreadingMMWithOuitput(arrayOfViDi2IIamge);
                else
                {
                    OriginalAsGPU2NoMultiThreadingMMWithOuitputForFifo(arrayOfViDi2IIamge);
                    btnTest8.Enabled = true;
                }

            }

            //  
            Application.DoEvents();

            //OriginalAsGPU2NoMultiThreadingMM(arrayOfViDi2IIamge);
            //TupleJobs tupleJobs = OriginalAsGPU2NoMultiThreadingMMWithOuitput(arrayOfViDi2IIamge);

            if (!xForFifo)
                runJobsOnly(gtupleJobs.jobs01, gtupleJobs.gpuId01, gtupleJobs.jobs02, gtupleJobs.gpuId02, gtupleJobs.StreamDict);
            else
                runJobsOnlyForFifo(gtupleJobsFifo.FifoJob01, gtupleJobsFifo.gpuId01, gtupleJobsFifo.FifoJob02, gtupleJobsFifo.gpuId02, gtupleJobsFifo.StreamDict);

            loadLog("End GPU2 Test");

            lstGPU2log.SelectedIndex = lstGPU2log.Items.Count - 1;


            MessageBox.Show("Done GPU2 Run Image Loading Buffer");

        }
        private void btnResetJobs_Click(object sender, EventArgs e)
        {
            xInitDone = false;
        }
        private void btnTest8_Click(object sender, EventArgs e)
        {

            //set item3 = false, works.
            gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob01[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob01[4] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob02[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob02[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob02[4] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            int iFileOut = (int)numImagesOut.Value;
            switch (iFileOut)
            {
                case 0:

                    break;
                case 1:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 2:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 3:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 4:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 5:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 6:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 7:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 8:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
            }

            //                        
            DateTime nowS = DateTime.Now;

            List<Dictionary<string, IMarking>> lstIMarking01 = ThreadAction01MMForFifo(gtupleJobsFifo.FifoJob01, gtupleJobsFifo.gpuId01, gtupleJobsFifo.FifoJob02, gtupleJobsFifo.gpuId02, gtupleJobsFifo.StreamDict);

            DateTime nowE = DateTime.Now;

            int hours = (nowE - nowS).Hours;
            int minutes = (nowE - nowS).Minutes;
            int seconds = (nowE - nowS).Seconds;
            int milliseconds = (nowE - nowS).Milliseconds;
            loadLog("Fifo " + (11 - iFileOut).ToString() + " images test Time: " + minutes.ToString() + ":" + seconds.ToString() + ":" + milliseconds.ToString() + ", Minutes:Seconds:Millisconds");
            //loadLog("");
            lstGPU2log.SelectedIndex = lstGPU2log.Items.Count - 1;


            //MessageBox.Show("Done Test 8");

        }
        //private async void btnBackgroundWorker_Click(object sender, EventArgs e)
        //{
        //    //clear contol if not null
        //    try
        //    {
        //        xInitDone = false;
        //        closeControl();

        //        BackRoundTasks.WorkerSupportsCancellation = true;
        //        var task = Task.Run(() => StartTaske1());
        //        await task;


        //        DateTime now = DateTime.Now;
        //        string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", now);
        //        loadLog(timenow14 + ": Start background Tasks");
        //    }
        //    catch (System.Exception ex) { }


        //}
        //private void btnBGWstop_Click(object sender, EventArgs e)
        //{
        //    BackRoundTasks.CancelAsync();

        //    DateTime now = DateTime.Now;
        //    string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", now);
        //    loadLog(timenow14 + ": End background Task");
        //}



        #endregion

        #region--------------------Utilities Methods ------------------------
        private void TakeOutFifo()
        {
            //set item3 = false, works.
            gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob01[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob01[4] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob02[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob02[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);
            gtupleJobsFifo.FifoJob02[4] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            int iFileOut = (int)numImagesOut.Value;
            switch (iFileOut)
            {
                case 0:

                    break;
                case 1:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 2:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 3:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 4:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 5:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);             
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 6:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 7:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
                case 8:
                    gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob01[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    gtupleJobsFifo.FifoJob02[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
                    break;
            }
        }
        private void TakeOutIIMageFifo()
        {
            //set item3 = false, works.
            IIMageFifo fgh = new IIMageFifo();

            //job01
            fgh = gtupleJobsFifo01.FifoJob01[0];
            fgh.xNewImage = true;
            gtupleJobsFifo01.FifoJob01[0]= fgh;  // = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            fgh = gtupleJobsFifo01.FifoJob01[1];
            fgh.xNewImage = true;
            gtupleJobsFifo01.FifoJob01[1] = fgh; // new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            fgh = gtupleJobsFifo01.FifoJob01[2];
            fgh.xNewImage = true;
            gtupleJobsFifo01.FifoJob01[2] = fgh;  // new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            fgh = gtupleJobsFifo01.FifoJob01[3];
            fgh.xNewImage = true;         
            gtupleJobsFifo01.FifoJob01[3] = fgh; //new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            fgh = gtupleJobsFifo01.FifoJob01[4];
            fgh.xNewImage = true;
            gtupleJobsFifo01.FifoJob01[4] = fgh; // new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            fgh = gtupleJobsFifo01.FifoJob01[5];
            fgh.xNewImage = true;
            gtupleJobsFifo01.FifoJob01[5] = fgh;

            //job02
            fgh = gtupleJobsFifo01.FifoJob02[0];
            fgh.xNewImage = true;
            gtupleJobsFifo01.FifoJob02[0] = fgh; //new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            fgh = gtupleJobsFifo01.FifoJob02[1];
            fgh.xNewImage = true;
            gtupleJobsFifo01.FifoJob02[1] = fgh;  // new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            fgh = gtupleJobsFifo01.FifoJob02[2];
            fgh.xNewImage = true;
            gtupleJobsFifo01.FifoJob02[2] = fgh; // Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            fgh = gtupleJobsFifo01.FifoJob02[3];
            fgh.xNewImage = true;
            gtupleJobsFifo01.FifoJob02[3] = fgh; // Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);

            fgh = gtupleJobsFifo01.FifoJob02[4];
            fgh.xNewImage = true;
            gtupleJobsFifo01.FifoJob02[4] = fgh; // Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, true);    
            

            //int iFileOut = (int)numImagesOut.Value;
            //switch (iFileOut)
            //{
            //    case 0:

            //        break;
            //    case 1:
            //        gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        break;
            //    case 2:
            //        gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        break;
            //    case 3:
            //        gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        break;
            //    case 4:
            //        gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        break;
            //    case 5:
            //        gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        break;
            //    case 6:
            //        gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        break;
            //    case 7:
            //        gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        break;
            //    case 8:
            //        gtupleJobsFifo.FifoJob01[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob01[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob01[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[0] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[1] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[2] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        gtupleJobsFifo.FifoJob02[3] = new Tuple<string, IIMageFifo, bool>(gtupleJobsFifo.FifoJob02[0].Item1, gtupleJobsFifo.FifoJob01[0].Item2, false);
            //        break;
            //}
        }
        private void TakeOutIIMageFifo01()
        {
            //job01
            for (int i = 0; i < 6;i++)
            {
                gtupleJobsFifo02.FifoJob01[i].xNewImage = true;
            }

            //job02
            for (int i = 0; i < 5; i++)
            {
                gtupleJobsFifo02.FifoJob02[i].xNewImage = true;
            }
        }
        private Models getModels(string INIpath)
        {
            Models models = new Models();
            
            IniFiles.IniFile AppliIni = IniFiles.IniFile.FromFile(INIpath);

            models.path = AppliIni["Models"] ["path"];
            models.model1FileName = AppliIni["Models"] [ "model1 name"];
            models.model2FileName = AppliIni["Models"] ["model2 name"];

            return models;
        }

        private void SaveModels(string INIpath)
        {
            

            IniFiles.IniFile AppliIni = IniFiles.IniFile.FromFile(INIpath);
            string smn1 = txtModel1.Text.Trim().Substring(txtModel1.Text.Trim().Length - 4, 4);
            if (smn1.ToUpper() == "vrws".ToUpper())
                gmodels.model1FileName = txtModel1.Text.Trim();
            else
            {
                MessageBox.Show("Enter model name with fie type, not saved");
                goto exitprocedure;
            }

            string smn2 = txtModel2.Text.Trim().Substring(txtModel2.Text.Trim().Length - 4, 4);
            if (smn2.ToUpper() == "vrws".ToUpper())
                gmodels.model2FileName = txtModel2.Text.Trim();
            else
            {
                MessageBox.Show("Enter model name with fie type, not saved");
                goto exitprocedure;
            }

            AppliIni["Models"]["model1 name"] = gmodels.model1FileName;
            AppliIni["Models"]["model2 name"] = gmodels.model2FileName;

        exitprocedure:;
            
        }



        #endregion

        #region--------------------Evaluation Engine Methods ------------------------

        Stream[] stream = new Stream[20];
        //IImageFactory
        private ImageCodecInfo jgpEncoder;
        private EncoderParameters myEncoderParameters;
        System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
        public IIMageFifo[] arrayOfViDi2IIamge = new IIMageFifo[20];
        private void btnTest4_Click(object sender, EventArgs e)
        {
            LoadImage();


        }
        private static void testll(string imgPath)
        {
            var libraryImage = new ViDi2.Local.LibraryImage(imgPath);
        }
        private void LoadImage()
        {
            List<int> GPUList = new List<int>();
            ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList); //must init control before any interface is useable

            control.InitializeComputeDevices(GpuMode.SingleDevicePerTool, GPUList);


            Dictionary<string, ViDi2.Runtime.IStream> StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();






            //imageBuffer[0]
            string imagesPath = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";
            // images for process
            string RedImagePath = imagesPath + "resources\\images\\Iscar\\220_06.35_041_1.jpg";


            //load test
            //ArrayOfByteArray

            string[] dd = Directory.GetFiles(imagesPath + @"resources\\images\\Iscar", "*.jpg");

            int i = 0;
            foreach (string item in dd)
            {
                Bitmap bmpDirect00 = new Bitmap(item);
                arrayOfViDi2IIamge[i].iimage = Bitmap2ViDi2ByteImage(bmpDirect00);
                i++;
            }



            string imgPath = RedImagePath;
            bool xExist = File.Exists(imgPath);
            var libraryImage = new ViDi2.Local.LibraryImage(imgPath);

            //try direct bmp
            Bitmap bmpDirect = new Bitmap(imgPath);

            Bitmap bmp = (Bitmap)libraryImage.Bitmap.Clone();

            ViDi2.IImage imagebb;
            ViDi2.ByteImage imagebbb;

            bool xAsProcedure = true;
            if (!xAsProcedure)
            {
                //---------------------------------Convert Microsoft Bitmap to ViDi Byte Array---------------------------------
                //Microsoft
                MemoryStream ms0 = new MemoryStream();
                bmp.Save(ms0, System.Drawing.Imaging.ImageFormat.Jpeg);
                byte[] bmpBytes = ms0.ToArray();
                int lineWidth = (bmp.Width * 24 + 7) / 8; //ok, check agenst line 1531 libraryImage         

                //ViDi 
                int colorChannels = 3;
                ViDi2.ByteImage byteImage = new ByteImage(bmp.Width, bmp.Height, colorChannels, ImageChannelDepth.Depth8, bmpBytes, lineWidth);


                //--------------------------------------------------------------------------------------------------------------
                //final, works,13-12-2023, need to check why imagebb.Bitmap is under error?
                imagebb = byteImage;
            }
            else
            {
                imagebb = Bitmap2ViDi2ByteImage(bmpDirect);   //bmp); 

                //Bmp2IImage bmp2IImage = new Bmp2IImage();
                //imagebb = bmp2IImage.Bitmap2ViDi2ByteImage(bmpDirect);
                //object o = imagebb.Lock;
                //IImage ii = o.

                int bytes = Math.Abs(16416) * bmp.Height;
                //59885622, 59885568
                //GC.KeepAlive(bmp2IImage);
            }

            string wsPath = "C:\\ProgramData\\Cognex\\VisionPro Deep Learning\\2.1\\Examples\\Resources\\runtime\\Iscar\\\\Proj_403_23042023_152921.vrws";

            string gpuHDM = "default/red_HDM_20M_5472x3648/0";
            control.Workspaces.Add("test1", wsPath, gpuHDM);
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();

            ISample sample0 = control.Workspaces["test1"].Streams["default"].CreateSample();

            using (ISample sample = control.Workspaces["test1"].Streams["default"].CreateSample(imagebb)) //(ISample sample = StreamDict[""].CreateSample(imagebb))
            {
                //sample.AddImage(img1);
                // process all tools on stream with specific gpu(gpuId)
                sample.Process(null, new List<int>()); //{ gpuId });
                lstIMarking.Add(sample.Markings); //Yoav 29-112023
            }


            if (imagebb != null)
                imagebb.Dispose();

            if (bmp != null)
                bmp.Dispose();

            //if (bmp1 != null)
            //    bmp1.Dispose();
            if (control != null)
                control.Dispose();

        }
        public void loadIImageBuffer(string imagesPath, int numImages2Load)
        {
            //load test
            //ArrayOfByteArray

            string[] dd = Directory.GetFiles(imagesPath, "*.jpg");

            //int i = 0;
            int iNumToLoad = 0;
            if (numImages2Load < 0)
                iNumToLoad = dd.GetLength(0);
            else
                iNumToLoad = numImages2Load;

            for (int i = 0; i < iNumToLoad; i++)
            {
                Bitmap bmpDirect00 = new Bitmap(dd[i]);
                arrayOfViDi2IIamge[i].iimage = Bitmap2ViDi2ByteImage(bmpDirect00);
                arrayOfViDi2IIamge[i].imageName = Path.GetFileNameWithoutExtension(dd[i]);
                arrayOfViDi2IIamge[i].imageIndex = i;
                //i++;
            }
        }
        public void loadIImageBufferAsnc(string imagesPath, int numImages2Load)
        {
            //load test
            //ArrayOfByteArray
            int iLoaded = 0;
            string[] dd = Directory.GetFiles(imagesPath, "*.jpg");

            //int i = 0;
            int iNumToLoad = 0;
            if (numImages2Load < 0)
                iNumToLoad = dd.GetLength(0);
            else
                iNumToLoad = numImages2Load;

            for (int i = 0; i < iNumToLoad; i++)
            {
                if (!arrayOfViDi2IIamge[i].xNewImage)
                {
                    Bitmap bmpDirect00 = new Bitmap(dd[i]);
                    arrayOfViDi2IIamge[i].iimage = Bitmap2ViDi2ByteImage(bmpDirect00);
                    arrayOfViDi2IIamge[i].imageName = Path.GetFileNameWithoutExtension(dd[i]);
                    arrayOfViDi2IIamge[i].imageIndex = i;
                    arrayOfViDi2IIamge[i].xNewImage = true;

                    iLoaded++;
                }
                //i++;
            }
        }
        private Image SaveToStream(Image img)
        {
            Bitmap bmp = new Bitmap(img);
            var ms = new MemoryStream();

            bmp.Save(ms, jgpEncoder, myEncoderParameters);

            //Do whatever you need to do with the image
            //e.g.
            img = Image.FromStream(ms);

            return img;
        }
        private ViDi2.ByteImage Bitmap2ViDi2ByteImage00(Bitmap bmp)
        {
            MemoryStream ms0 = new MemoryStream();
            bmp.Save(ms0, System.Drawing.Imaging.ImageFormat.Bmp);
            byte[] bmpBytes = ms0.ToArray();
            int lineWidth = (bmp.Width * 24 + 7) / 8; //ok, check agenst line 1531 libraryImage. for 24bppRgb pixel format       

            //PixelFormat pf = bmp.PixelFormat;

            //ViDi 
            int colorChannels = 3;
            ViDi2.ByteImage byteImage = new ByteImage(bmp.Width, bmp.Height, colorChannels, ImageChannelDepth.Depth8, bmpBytes, lineWidth);

            return byteImage;
        }//communication between microsoft to cognex
        private ViDi2.ByteImage Bitmap2ViDi2ByteImage(Bitmap bmp)
        {
            //Bitmap2ViDi2ByteImage(Bitmap bmp) works good, 18-02-2024
            bmp.RotateFlip(RotateFlipType.Rotate180FlipX); //needs this because next step rotate 180 deg.

            MemoryStream ms0 = new MemoryStream();
            bmp.Save(ms0, System.Drawing.Imaging.ImageFormat.Bmp);
            byte[] bmpBytes = ms0.ToArray();
            int lineWidth = (bmp.Width * 24 + 7) / 8; //ok, check agenst line 1531 libraryImage. for 24bppRgb pixel format       

            //ViDi 
            int colorChannels = 3;
            ViDi2.ByteImage byteImage = new ViDi2.ByteImage(bmp.Width, bmp.Height, colorChannels, ViDi2.ImageChannelDepth.Depth8, bmpBytes, lineWidth);

            //https://stackoverflow.com/questions/10924711/rotate-image-using-rotateflip-in-c-sharp

            return byteImage;
        }
        private ImageCodecInfo GetEncoder(System.Drawing.Imaging.ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
        private void ClearBufferStatus()
        {
            for (int i = 0; i < arrayOfViDi2IIamge.GetLength(0); i++)
            {
                arrayOfViDi2IIamge[i].xNewImage = false;
            }
        }
        #endregion

               

        public class CurrentState
        {

            public string Msg;  //can be error message or end of operation message
            public bool ErrOK;
            public string AutoCycleLogItemsAdd;
            public short[] regs = new short[4];
            public int MarkedBins;
            public int TaskNumber;
            public bool NextStation;
            public int CurrentStation;
            public List<Dictionary<string, IMarking>> MarkingsList;
            public MultiResuls multiResuls;

        }

               
        private long TimerMS
        {
            //units of milliseconds
            get { return (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond); }

        }
                       
        #region--------------------Utilities Methods ------------------------        
        public void Sleep(int timeMS)
        {
            System.Threading.Thread.Sleep(timeMS);
        }

        private void TimedDoEvent(long pause)
        {
            //time in milliseconds
            long Ltime = TimerMS;
            //Application.DoEvents();
            do
            {
                //Application.DoEvents();
            } while (Ltime + pause > TimerMS);
        }


        #endregion
        bool xContinue = true;
        #region------------------Backround tasks Methods --------------------       


        //public void BackRoundTasks_DoWork(object sender, DoWorkEventArgs e)
        //{
        //    return;
        //    // This event handler is where the actual work is done.
        //    // This method runs on the background thread.

        //    // Get the BackgroundWorker object that raised this event.
        //    System.ComponentModel.BackgroundWorker worker;
        //    worker = (System.ComponentModel.BackgroundWorker)sender;

        //    // Get the Words object and call the main method.
        //    //BKW_Def bkw = (BKW_Def)e.Argument;
        //    bkw = (BKW_Def)e.Argument;
        //    //sget input parameters of worker
        //    string TaskToDo = bkw.ParamsIN.sTasklName;
        //    int ParamIN1 = bkw.ParamsIN.iParam1;
        //    long ParamIN2 = bkw.ParamsIN.lParam2;
        //    float ParamIN3 = bkw.ParamsIN.fParam3;

        //    //run local method doing the requested task using input parameters
        //    //bkw.RunMachineTasks(worker, e);
        //    //RunMachineTasks(worker, e);
        //    //RunEvaluationTasks(worker, e);
        //    //RunEvaluationTasksAsync(bkw.ParamsIN.sTasklName, e);
        //}        
        //public void RunEvaluationTasks(BackgroundWorker worker, DoWorkEventArgs e)
        //{
        //    return;
        //    //task1 load images and create jobs on Fifo
        //    //task2 make first run
        //    //task3 run evaluator with Fifo
        //    //task4  
        //    //task5  
        //    //task6  
        //    //task7  
        //    //task8  


        //    // Initialize the variables.
        //    CurrentState state = new CurrentState();


        //    bool xError = false;

        //    switch (bkw.ParamsIN.sTasklName)
        //    {

        //        case "Task1":    //load Fifo
        //            invy.ClearListBox(lstMarking);
        //            invy.ClearListBox(lstGPU2log);


        //            state.TaskNumber = 1;
        //            //string imagesPath00 = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";

        //            string imagesPath = "";
        //            int numImages2Load = -1;


        //            //imagesPath = imagesPath00 + @"resources\images\Iscar";
        //            imagesPath = @"S:\A03 - Tools\A03-HW-M70 Pressing.Systems\Public\SC End Mills Inspection\Yoav\PROJ_403_Test_Images";
        //            numImages2Load = -1;
        //            //loadIImageBuffer(imagesPath, numImages2Load);


        //            //state.Msg = "loadIImageBuffer() state:" + state.Msg;
        //            state.Msg = "loadIImageBuffer() done successfully";  // Images Path: "; // + imagesPath;
        //            xError = false;



        //            break;

        //        case "Task2":   //Initialized run with Fifo
        //            try
        //            {


        //            xError = false;
        //            state.TaskNumber = 2;
        //            //bool ReadOK = false;
        //            state.Msg = "";
        //            //worker.ReportProgress(1, state);
        //            OriginalAsGPU2NoMultiThreadingMMWithOuitputForFifoBW01(arrayOfViDi2IIamge,ref worker, ref state);                    
        //            state.Msg = "Finish Successfully OriginalAsGPU2NoMultiThreadingMMWithOuitputForFifo(), Initialized run with Fifo";

        //                //worker.ReportProgress(100, state);
        //            }
        //            catch(ViDi2.Exception e1)
        //            {
        //                MessageBox.Show("Error Initializing gpu: " + e1.Message);
        //                //state.Msg = "Fifo-evaluation canceled";
        //                //worker.ReportProgress(iNumEvaluationCycles, state);
        //                state.Msg = e1.Message;
        //                xError = true;
        //            }

        //            break;
        //        case "Task3":  //run Fifo-Evaluator
        //            state.TaskNumber = 3;
        //            //-----------Start main marking loop------------------

        //            bool ExitFore = false;
        //            bool ErrOK = false;
        //            bool exit = false;


        //            //
        //            //state.Msg = "Start ThreadAction01MMForFifo()";
        //            state.Msg = "Start ThreadAction02MMForFifo()";
        //            worker.ReportProgress(0, state);
        //            int inumCycles = 10000;
        //            bool xContinue = true;
        //            int idleCount = 0;
        //            int idleCountMax = 0;
        //            bool xAllDone = false;
        //            int itotalImages = 32; //number of models x number of images 16x2
        //            iLastLoadingPicIndex = 0; // -1;
        //            int itotalreceived = 0;
        //            int imgCountL = 0;
        //            int iNewToProcess = 0;
        //            int itotalreceivedDebug = 0;
        //            bool xReport2Automaion = false;

        //            //for reinit bgw, don't work
        //            //int imgCount = Convert.ToInt32(inv.gettxt(frmBeckhoff.mFormBeckhoffDefInstance.lblCount));
        //            //itotalreceived = imgCount;

        //            try
        //            {

        //                while (!exit)
        //                {


        //                    int imgCount = Convert.ToInt32(inv.gettxt(frmBeckhoff.mFormBeckhoffDefInstance.lblCount));
        //                    if (itotalreceived < imgCount) {imgCountL = imgCount - itotalreceived; itotalreceivedDebug = itotalreceived; itotalreceived = imgCount;}                         
        //                    if (imgCountL > 0 && !xAllDone) // ener to evaluation
        //                    {
        //                        if (!xReport2Automaion)
        //                        { xReport2Automaion = true; 
        //                            inv.settxt(lblIndicatorStartStop, "run"); 
        //                            inv.set(lblIndicatorStartStop, "BackColor", Color.Blue);
        //                            invy.ClearListBox(lstMarking);
        //                            invy.ClearListBox(lstGPU2log);
        //                        }
        //                        //----vision system will reset as a sign for new cycle----
        //                        //inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.lblCount, "0");

        //                        //loadIImageBufferAsncPic(imgCount);
        //                        loadIImageBufferAsnc01Pic(imgCountL);//load images to buffer
        //                        iNewToProcess = imgCountL;
        //                        imgCountL = 0;


        //                        //if (xLoading)
        //                        //    System.Threading.Thread.Sleep(100);
        //                        //update number of images to process
        //                        //TakeOutIIMageFifo01(); no needs, done in ImageBufferToJobs 
        //                        //new 08/01/24 only text for now
        //                        //ImageBufferToJobs(arrayOfViDi2IIamge); //divide by two
        //                        ImageBufferToJobsMult(arrayOfViDi2IIamge); //multiply   build the job


        //                        //with saperate task for each job,model
        //                        var taskJob01 = Task.Run(() =>
        //                        EvaluateJob01(gtupleJobsFifo02.FifoJob01, gtupleJobsFifo.gpuId01, gtupleJobsFifo.StreamDict));
        //                        //
        //                        //clearImagexNew(gtupleJobsFifo02.FifoJob01);
        //                        //SetEvaldone(gtupleJobsFifo02.FifoJob01);  //15/01/24, only one set of images for both job1 and job2,
        //                        //set evaluaion done bit only after job2 done

        //                        var taskJob02 = Task.Run(() =>
        //                        EvaluateJob02(gtupleJobsFifo02.FifoJob02, gtupleJobsFifo.gpuId02, gtupleJobsFifo.StreamDict));
        //                        //
        //                        //clearImagexNew(gtupleJobsFifo02.FifoJob02);
        //                        SetEvaldone(gtupleJobsFifo02.FifoJob02);

        //                        int job1NumDone = CheckIfAllEvaldone(gtupleJobsFifo02.FifoJob01);
        //                        int job2NumDone = CheckIfAllEvaldone(gtupleJobsFifo02.FifoJob02);

        //                        if (itotalImages == job1NumDone + job2NumDone)
        //                        {
        //                            xAllDone = true;
        //                            //inv.settxt(lblIndicatorStartStop, "done");
        //                            //inv.set(lblIndicatorStartStop, "BackColor", Color.LimeGreen);
        //                        }

        //                        //with global job
        //                        Thread.Sleep(2);
        //                        var threads = new List<Task>();

        //                        threads.Add(taskJob01);
        //                        threads.Add(taskJob02);
        //                        // wait for all tasks to finish
        //                        Task.WaitAll(threads.ToArray());

        //                        List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();
        //                        List<Dictionary<string, IMarking>> lstIMarking01 = taskJob01.Result;
        //                        List<Dictionary<string, IMarking>> lstIMarking02 = taskJob02.Result;
        //                        lstIMarking.AddRange(lstIMarking01);
        //                        lstIMarking.AddRange(lstIMarking02);

        //                        //-------------------new 15/01/24----------------
        //                        MultiResuls multiResuls = new MultiResuls();
        //                        multiResuls.markings = lstIMarking;
        //                        multiResuls.imagesNames = new string[iNewToProcess*2]; // gtupleJobsFifo02.FifoJob01.Count() + gtupleJobsFifo02.FifoJob02.Count()];
        //                        //iNewToProcess

        //                        //job 1
        //                        int imgIndex = 0;
        //                        int imgIndex01 = 0;
        //                        int inumProcessed = gtupleJobsFifo02.FifoJob01.Count();
        //                        foreach (IIMageFifo item in gtupleJobsFifo02.FifoJob01)
        //                        {
        //                            if (imgIndex >= inumProcessed - iNewToProcess)
        //                            {
        //                                if (item.iimage != null)
        //                                {
        //                                    multiResuls.imagesNames[imgIndex01] = item.imageName;
        //                                    multiResuls.imagesNames[imgIndex01 + 1] = item.imageName;
        //                                    imgIndex01++;
        //                                }
        //                            }

        //                            imgIndex++;
        //                        }

        //                        //job 2
        //                        //imgIndex = 0;
        //                        //foreach (IIMageFifo item in gtupleJobsFifo02.FifoJob02)
        //                        //{
        //                        //    if (item.iimage != null)
        //                        //    {
        //                        //        multiResuls.imagesNames[imgIndex] = item.imageName;
        //                        //        imgIndex++;
        //                        //    }
        //                        //}

        //                        //state.MarkingsList = lstIMarking;
        //                        state.multiResuls = multiResuls;


        //                        threads[0].Dispose();
        //                        threads[1].Dispose();
        //                        //threads.Remove(null);
        //                        threads.Clear();

        //                        //return lstIMarking;
        //                        //Dictionary<string, IMarking> mm = lstIMarking01[0];
        //                        //IImageInfo ii = mm["red_HDM_20M_5472x3648"].ImageInfo;


        //                        //reporting
        //                        iNumEvaluationCycles++;
        //                        if (iNumEvaluationCycles > inumCycles)
        //                        {
        //                            if (!xContinue)
        //                            {
        //                                iNumEvaluationCycles = 1;
        //                                state.Msg = "Fifo-evaluation Finished " + inumCycles.ToString() + " cycles";
        //                                exit = true;
        //                            }
        //                            else
        //                            {
        //                                iNumEvaluationCycles = 1;
        //                                state.Msg = "Fifo-evaluation Finished " + inumCycles.ToString() + "Cycles";
        //                                worker.ReportProgress(iNumEvaluationCycles, state);
        //                            }
        //                        }
        //                        else
        //                        {
        //                            //standard reporting

        //                            string NumImages = lstIMarking.Count.ToString();
        //                            state.Msg = "Evaluation Cycle: " + iNumEvaluationCycles.ToString() + ", Number Of Images Evaluated: " + NumImages;
        //                            worker.ReportProgress(iNumEvaluationCycles, state);
        //                            //delay
        //                            System.Threading.Thread.Sleep(50);


        //                        }

        //                        //end background task
        //                        if (BackRoundTasks.CancellationPending)
        //                        {
        //                            inv.settxt(lblIndicatorStartStop, "canceled");
        //                            inv.set(lblIndicatorStartStop, "BackColor", Color.Red);
        //                            state.Msg = "Fifo-evaluation canceled";
        //                            worker.ReportProgress(iNumEvaluationCycles, state);
        //                            exit = true;
        //                        }

        //                        idleCountMax = 0;

        //                    }
        //                    else
        //                    {
        //                        //goto excBypass;
        //                        //idle freqeuncy

        //                        System.Threading.Thread.Sleep(50);
        //                        if (idleCountMax < 20)
        //                        {
        //                            if (idleCount > 18)
        //                            {
        //                                idleCount = 0;
        //                                //DateTime nowS = DateTime.Now;
        //                                //string timenow14 = string.Format("{0:dd-MM-yy HH:mm:ss}", nowS);
        //                                state.Msg = "Evaluation Idle, Waiting For Next Batch";
        //                                inv.settxt(lblIndicatorStartStop, "done");
        //                                inv.set(lblIndicatorStartStop, "BackColor", Color.LimeGreen);
        //                                worker.ReportProgress(iNumEvaluationCycles, state);

        //                                idleCountMax++;
        //                            }
        //                            else
        //                            {
        //                                idleCount++;
        //                            }


        //                        }

        //                        //check for new cycle
        //                        int imgCountZero = Convert.ToInt32(inv.gettxt(frmBeckhoff.mFormBeckhoffDefInstance.lblCount));
        //                        if(imgCountZero == 0)
        //                        {
        //                            ClearBufferStatus();
        //                            clearNewAndDoneAll(gtupleJobsFifo02.FifoJob01);
        //                            clearNewAndDoneAll(gtupleJobsFifo02.FifoJob02);
        //                            for(int i=0;i<20; i++)
        //                            {
        //                                loadingQueue.loadPic[i] = false;
        //                            }
        //                            iLastLoadingPicIndex = 0;  // -1;
        //                            itotalreceived = 0;
        //                            xAllDone = false;
        //                            iLastLoadingPicIndexTrue = 0;
        //                            xReport2Automaion = false;


        //                        }

        //                        //end background task
        //                        if (BackRoundTasks.CancellationPending)
        //                        {
        //                            state.Msg = "Fifo-evaluation canceled";
        //                            worker.ReportProgress(iNumEvaluationCycles, state);
        //                            exit = true;
        //                        }

        //                    }
        //                }

        //            }
        //            catch(System.Exception e1)
        //            {
        //                string errmsg = e1.Message;

        //                xError = true;
        //                state.Msg = "Fifo-evaluation Error. Exit Fifo Evaluation";
        //            }

        //             break;

        //        case "Task4":  //not used
        //            state.TaskNumber = 4;



        //            state.Msg = "";                                        

        //            break;

        //        case "Task5":  //not used
        //            state.TaskNumber = 5;


        //            state.Msg = "";


        //            break;

        //        case "Task6":  //not used
        //            state.TaskNumber = 6;

        //            //if (worker.CancellationPending) { state.Msg = "User aborted cycle! Marking station: " + CstationNumber; e.Cancel = true; goto ExitSub; }


        //            ErrOK = false;
        //            state.Msg = "";


        //            break;

        //        case "Task7":   //not used
        //            state.TaskNumber = 7;




        //            break;

        //        case "Task8":  //not used
        //            state.TaskNumber = 8;


        //            break;

        //        default:
        //            state.TaskNumber = 0;
        //            break;

        //    }

        //ExitSub:;

        //    //error
        //    if (xError)
        //    {
        //        //2 = error exit auto cycle, error message is state.Msg
        //        state.ErrOK = false;
        //        worker.ReportProgress(2, state);
        //        System.Threading.Thread.Sleep(1000);

        //    }            

        //    //no error
        //    if (!xError)
        //    {
        //        //1 return from bkw with NO error
        //        state.ErrOK = true;

        //        worker.ReportProgress(4, state);
        //        System.Threading.Thread.Sleep(1000);
        //    }

        //    state.CurrentStation = CstationNumber + 1;

        //    //for testing only
        //    System.Threading.Thread.Sleep(1000);

        //    //set results for event worker complete
        //    e.Result = state;

        //    //system will exit this procedure raising the event 'RunWorkerComplete'


        //} //end RunMachineTasks
        public async Task<bool>  RunEvaluationTasksAsync(string TaskToDo, int ParamIN1, long ParamIN2, float ParamIN3,  CurrentState state)
        {

            //task1 load images and create jobs on Fifo
            //task2 make first run
            //task3 run evaluator with Fifo
            //task4  
            //task5  
            //task6  
            //task7  
            //task8  

            CurrentState State = state;
            // Initialize the variables.
            
            bool xError = false;

            switch (TaskToDo)
            {

                case "Task1":    //load Fifo
                    invy.ClearListBox(lstMarking);
                    invy.ClearListBox(lstGPU2log);


                    state.TaskNumber = 1;
                    //string imagesPath00 = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";

                    string imagesPath = "";
                    int numImages2Load = -1;


                    //imagesPath = imagesPath00 + @"resources\images\Iscar";
                    imagesPath = @"S:\A03 - Tools\A03-HW-M70 Pressing.Systems\Public\SC End Mills Inspection\Yoav\PROJ_403_Test_Images";
                    numImages2Load = -1;
                    //loadIImageBuffer(imagesPath, numImages2Load);


                    //state.Msg = "loadIImageBuffer() state:" + state.Msg;
                    state.Msg = "loadIImageBuffer() done successfully";  // Images Path: "; // + imagesPath;
                    xError = false;



                    break;

                case "Task2":   //Initialized run with Fifo
                    try
                    {


                        xError = false;
                        state.TaskNumber = 2;
                        //bool ReadOK = false;
                        state.Msg = "";
                        //worker.ReportProgress(1, state);
                        Task.Run(()=>ProgressChangedAsync(state));
                        
                        State = state;
                        var task1 = Task.Run(() => OriginalAsGPU2NoMultiThreadingMMWithOuitputForFifoBW01(arrayOfViDi2IIamge, State));
                        await task1;
                        State = task1.Result.state;
                        State.Msg = "Finish Successfully OriginalAsGPU2NoMultiThreadingMMWithOuitputForFifo(), Initialized run with Fifo";

                        //worker.ReportProgress(100, state);
                        Task.Run(() => ProgressChangedAsync(State));
                        
                    }
                    catch (ViDi2.Exception e1)
                    {
                        MessageBox.Show("Error Initializing gpu: " + e1.Message);
                        //state.Msg = "Fifo-evaluation canceled";
                        //worker.ReportProgress(iNumEvaluationCycles, state);
                        Task.Run(() => ProgressChangedAsync(state));
                       
                        state.Msg = e1.Message;
                        xError = true;
                    }

                    break;
                case "Task3":  //run Fifo-Evaluator
                    state.TaskNumber = 3;
                    //-----------Start main marking loop------------------

                    bool ExitFore = false;
                    bool ErrOK = false;
                    bool exit = false;


                    //
                    //state.Msg = "Start ThreadAction01MMForFifo()";
                    state.Msg = "Start ThreadAction02MMForFifo()";
                    //worker.ReportProgress(0, state);
                    Task.Run(() => ProgressChangedAsync(state));
                    
                    int inumCycles = 10000;
                    //bool xContinue = true;
                    int idleCount = 0;
                    int idleCountMax = 0;
                    bool xAllDone = false;
                    int itotalImages = 32; //number of models x number of images 16x2
                    iLastLoadingPicIndex = 0; // -1;
                    int itotalreceived = 0;
                    int imgCountL = 0;
                    int iNewToProcess = 0;
                    int itotalreceivedDebug = 0;
                    bool xReport2Automaion = false;

                    //for reinit bgw, don't work
                    //int imgCount = Convert.ToInt32(inv.gettxt(frmBeckhoff.mFormBeckhoffDefInstance.lblCount));
                    //itotalreceived = imgCount;

                    try
                    {

                        while (!exit)
                        {

                            Thread.Sleep(2);
                            int imgCount = Convert.ToInt32(inv.gettxt(frmBeckhoff.mFormBeckhoffDefInstance.lblCount));
                            if (itotalreceived < imgCount) { imgCountL = imgCount - itotalreceived; itotalreceivedDebug = itotalreceived; itotalreceived = imgCount; }
                            if (imgCountL > 0 && !xAllDone) // ener to evaluation
                            {
                                if (!xReport2Automaion)
                                {
                                    xReport2Automaion = true;
                                    inv.settxt(lblIndicatorStartStop, "run");
                                    inv.set(lblIndicatorStartStop, "BackColor", Color.Blue);
                                    invy.ClearListBox(lstMarking);
                                    invy.ClearListBox(lstGPU2log);
                                }
                                state.Msg = "Image number.......... "+ imgCount.ToString();
                                Task.Run(() => ProgressChangedAsync(state));
                                //----vision system will reset as a sign for new cycle----
                                //inv.settxt(frmBeckhoff.mFormBeckhoffDefInstance.lblCount, "0");

                                //loadIImageBufferAsncPic(imgCount);
                                int err = loadIImageBufferAsnc01Pic(imgCountL);//load images to buffer
                                if(err > 0)
                                {
                                    state.Msg = "Evaluation Idle, Waiting For Next Batch";
                                    inv.settxt(lblIndicatorStartStop, "ERROR");
                                    inv.set(lblIndicatorStartStop, "BackColor", Color.Red);
                                    Task.Run(() => ProgressChangedAsync(state));
                                    return false;
                                }
                                iNewToProcess = imgCountL;
                                imgCountL = 0;


                                //if (xLoading)
                                //    System.Threading.Thread.Sleep(100);
                                //update number of images to process
                                //TakeOutIIMageFifo01(); no needs, done in ImageBufferToJobs 
                                //new 08/01/24 only text for now
                                //ImageBufferToJobs(arrayOfViDi2IIamge); //divide by two
                                ImageBufferToJobsMult(arrayOfViDi2IIamge); //multiply   build the job


                                //with saperate task for each job,model
                                var taskJob01 = Task.Run(() =>
                                EvaluateJob01(gtupleJobsFifo02.FifoJob01, gtupleJobsFifo.gpuId01, gtupleJobsFifo.StreamDict));
                                //
                                //clearImagexNew(gtupleJobsFifo02.FifoJob01);
                                //SetEvaldone(gtupleJobsFifo02.FifoJob01);  //15/01/24, only one set of images for both job1 and job2,
                                //set evaluaion done bit only after job2 done

                                var taskJob02 = Task.Run(() =>
                                EvaluateJob02(gtupleJobsFifo02.FifoJob02, gtupleJobsFifo.gpuId02, gtupleJobsFifo.StreamDict));
                                //
                                //clearImagexNew(gtupleJobsFifo02.FifoJob02);
                                SetEvaldone(gtupleJobsFifo02.FifoJob02);

                                int job1NumDone = CheckIfAllEvaldone(gtupleJobsFifo02.FifoJob01);
                                int job2NumDone = CheckIfAllEvaldone(gtupleJobsFifo02.FifoJob02);

                                if (itotalImages == job1NumDone + job2NumDone)
                                {
                                    xAllDone = true;
                                    //inv.settxt(lblIndicatorStartStop, "done");
                                    //inv.set(lblIndicatorStartStop, "BackColor", Color.LimeGreen);
                                }

                                //with global job
                                Thread.Sleep(2);
                                var TaskList = new List<Task>();

                                TaskList.Add(taskJob01);
                                TaskList.Add(taskJob02);
                                // wait for all tasks to finish
                                Task.WaitAll(TaskList.ToArray());

                                List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();
                                List<Dictionary<string, IMarking>> lstIMarking01 = taskJob01.Result;
                                List<Dictionary<string, IMarking>> lstIMarking02 = taskJob02.Result;
                                lstIMarking.AddRange(lstIMarking01);
                                lstIMarking.AddRange(lstIMarking02);

                                //-------------------new 15/01/24----------------
                                MultiResuls multiResuls = new MultiResuls();
                                multiResuls.markings = lstIMarking;
                                multiResuls.imagesNames = new string[iNewToProcess * 2]; // gtupleJobsFifo02.FifoJob01.Count() + gtupleJobsFifo02.FifoJob02.Count()];
                                //iNewToProcess

                                //job 1
                                int imgIndex = 0;
                                int imgIndex01 = 0;
                                int inumProcessed = gtupleJobsFifo02.FifoJob01.Count();
                                foreach (IIMageFifo item in gtupleJobsFifo02.FifoJob01)
                                {
                                    Thread.Sleep(1);
                                    if (imgIndex >= inumProcessed - iNewToProcess)
                                    {
                                        if (item.iimage != null)
                                        {
                                            multiResuls.imagesNames[imgIndex01] = item.imageName;
                                            multiResuls.imagesNames[imgIndex01 + 1] = item.imageName;
                                            imgIndex01++;
                                        }
                                    }

                                    imgIndex++;
                                }

                                state.multiResuls = multiResuls;

                                //reporting
                                iNumEvaluationCycles++;
                                if (iNumEvaluationCycles > inumCycles)
                                {
                                    if (!xContinue)
                                    {
                                        iNumEvaluationCycles = 1;
                                        state.Msg = "Fifo-evaluation Finished " + inumCycles.ToString() + " cycles";
                                        exit = true;
                                    }
                                    else
                                    {
                                        iNumEvaluationCycles = 1;
                                        state.Msg = "Fifo-evaluation Finished " + inumCycles.ToString() + "Cycles";
                                        
                                        Task.Run(() => ProgressChangedAsync(state));
                                        
                                    }
                                }
                                else
                                {
                                    //standard reporting

                                    string NumImages = lstIMarking.Count.ToString();
                                    state.Msg = "Evaluation Cycle: " + iNumEvaluationCycles.ToString() + ", Number Of Images Evaluated: " + NumImages;
                                    Task.Run(() => ProgressChangedAsync(state));
                                    
                                    //delay
                                    System.Threading.Thread.Sleep(50);


                                }

                                //end background task
                                //if (BackRoundTasks.CancellationPending)
                                //{
                                //    inv.settxt(lblIndicatorStartStop, "canceled");
                                //    inv.set(lblIndicatorStartStop, "BackColor", Color.Red);
                                //    state.Msg = "Fifo-evaluation canceled";
                                //    //worker.ReportProgress(iNumEvaluationCycles, state);
                                //    ProgressChangedAsync(state);
                                //    exit = true;
                                //}

                                idleCountMax = 0;

                            }
                            else
                            {
                                //goto excBypass;
                                //idle freqeuncy

                                System.Threading.Thread.Sleep(20);
                                if (idleCountMax < 20)
                                {
                                    if (idleCount > 18)
                                    {
                                        idleCount = 0;
                                        //DateTime nowS = DateTime.Now;
                                        //string timenow14 = string.Format("{0:dd-MM-yy HH:mm:ss}", nowS);
                                        state.Msg = "Evaluation Idle, Waiting For Next Batch";
                                        inv.settxt(lblIndicatorStartStop, "done");
                                        inv.set(lblIndicatorStartStop, "BackColor", Color.LimeGreen);
                                        //worker.ReportProgress(iNumEvaluationCycles, state);
                                        if (CancelAsync) return true;
                                        Task.Run(() => ProgressChangedAsync(state));
                                        

                                        idleCountMax++;
                                    }
                                    else
                                    {
                                        idleCount++;
                                    }


                                }

                                //check for new cycle
                                int imgCountZero = Convert.ToInt32(inv.gettxt(frmBeckhoff.mFormBeckhoffDefInstance.lblCount));
                                if (imgCountZero == 0)
                                {
                                    ClearBufferStatus();
                                    if (!(gtupleJobsFifo02.FifoJob01 is null))
                                        clearNewAndDoneAll(gtupleJobsFifo02.FifoJob01);
                                    if (!(gtupleJobsFifo02.FifoJob02 is null))
                                        clearNewAndDoneAll(gtupleJobsFifo02.FifoJob02);
                                    for (int i = 0; i < 20; i++)
                                    {
                                        loadingQueue.loadPic[i] = false;
                                    }
                                    iLastLoadingPicIndex = 0;  // -1;
                                    itotalreceived = 0;
                                    xAllDone = false;
                                    iLastLoadingPicIndexTrue = 0;
                                    xReport2Automaion = false;


                                }

                                //end background task
                                //if (BackRoundTasks.CancellationPending)
                                //{
                                //    state.Msg = "Fifo-evaluation canceled";
                                //    //worker.ReportProgress(iNumEvaluationCycles, state);
                                //    ProgressChangedAsync(state);
                                //    exit = true;
                                //}

                            }
                        }
                       
                    }
                    catch (System.Exception e1)
                    {
                        string errmsg = e1.Message;

                        xError = true;
                        state.Msg = "Fifo-evaluation Error. Exit Fifo Evaluation";
                        Task.Run(() => ProgressChangedAsync(state));
                    }

                    break;

                case "Task4":  //not used
                    state.TaskNumber = 4;



                    state.Msg = "";

                    break;

                case "Task5":  //not used
                    state.TaskNumber = 5;


                    state.Msg = "";


                    break;

                case "Task6":  //not used
                    state.TaskNumber = 6;

                    //if (worker.CancellationPending) { state.Msg = "User aborted cycle! Marking station: " + CstationNumber; e.Cancel = true; goto ExitSub; }


                    ErrOK = false;
                    state.Msg = "";


                    break;

                case "Task7":   //not used
                    state.TaskNumber = 7;




                    break;

                case "Task8":  //not used
                    state.TaskNumber = 8;


                    break;

                default:
                    state.TaskNumber = 0;
                    break;

            }

        ExitSub:

            //error
            if (xError)
            {
                //2 = error exit auto cycle, error message is state.Msg
                state.ErrOK = false;
                //worker.ReportProgress(2, state);
                Task.Run(() => ProgressChangedAsync(state));
                
                System.Threading.Thread.Sleep(100);

            }

            //no error
            if (!xError)
            {
                //1 return from bkw with NO error
                state.ErrOK = true;

                //worker.ReportProgress(4, state);
                Task.Run(() => ProgressChangedAsync(state));
                
                Thread.Sleep(100);
            }

            state.CurrentStation = CstationNumber + 1;

            //for testing only
            System.Threading.Thread.Sleep(100);
            //bool CancelAsync = false;
            var task = Task.Run(() => BackRoundTasks_RunWorkerCompletedAsync(state, CancelAsync));
            await task;
            ;
            return true;
            //set results for event worker complete
            //e.Result = state;

            //system will exit this procedure raising the event 'RunWorkerComplete'


        } //end RunMachineTasks
        private void clearImagexNew(IIMageFifo[] fifoJob01)
        {
            foreach (IIMageFifo item in fifoJob01)
            {
                arrayOfViDi2IIamge[item.imageIndex].xNewImage = false;
            }
        }
        private void clearNewAndDoneAll(IIMageFifo[] fifoJob01)
        {
            try
            {
                foreach (IIMageFifo item in fifoJob01)
                {
                    arrayOfViDi2IIamge[item.imageIndex].xNewImage = false;
                    arrayOfViDi2IIamge[item.imageIndex].xEvaluationDone = false;
                }
            }
            catch (System.Exception ex) { }
        }
        private void SetEvaldone(IIMageFifo[] fifoJob01)
        {            
            foreach (IIMageFifo item in fifoJob01)
            {
                if (arrayOfViDi2IIamge[item.imageIndex].xNewImage && !arrayOfViDi2IIamge[item.imageIndex].xEvaluationDone)               
                      arrayOfViDi2IIamge[item.imageIndex].xEvaluationDone = true;
                
            }
        }
        private int[] SetEvaldone01(IIMageFifo[] fifoJob01)
        {
            int[] indexesLastRun = new int[0];
            foreach (IIMageFifo item in fifoJob01)
            {
                if (arrayOfViDi2IIamge[item.imageIndex].xNewImage && !arrayOfViDi2IIamge[item.imageIndex].xEvaluationDone)
                {
                    arrayOfViDi2IIamge[item.imageIndex].xEvaluationDone = true;
                }
            }

            return indexesLastRun;
        }
        private int CheckIfAllEvaldone(IIMageFifo[] fifoJob01)
        {
            int iNumDone = 0;
            foreach (IIMageFifo item in fifoJob01)
            {

                if (arrayOfViDi2IIamge[item.imageIndex].xNewImage && arrayOfViDi2IIamge[item.imageIndex].xEvaluationDone)
                    iNumDone++;
            }

            return iNumDone;
        }
        private void ImageBufferToJobs(IIMageFifo[] arrayOfViDi2IIamge)
        {
            //numberOfImagesToProcess = f(arrayOfViDi2IIamge)
            //newJob01 = Floor(numberOfImagesToProcess/2)
            //newJob02 = numberOfImagesToProcess = newJob01
            //gtupleJobsFifo02.FifoJob01 = newJob01;
            //gtupleJobsFifo02.FifoJob02 = newJob02;

            int numberOfImagesToProcess = numToProcess(arrayOfViDi2IIamge);

            int numForJob2 = numberOfImagesToProcess / 2;
            int numForJob1 = numberOfImagesToProcess - numForJob2;

            IIMageFifo[] newJob01 = new IIMageFifo[numForJob1];
            IIMageFifo[] newJob02 = new IIMageFifo[numForJob2];

            //sort by xNewImage = ture
            //arrayOfViDi2IIamge[13].xNewImage = false; testing only
            //arrayOfViDi2IIamge[13].imageIndex = 0;    testing only
            int[] listIndex = findInsertsOnaLine(arrayOfViDi2IIamge);

            //copy to job1            
            for(int i=0;i< numForJob1;i++)
            {
                newJob01[i] = arrayOfViDi2IIamge[listIndex[i]];

                //add jpu name
                newJob01[i].gpuName   = "HDM-Red-0";                
            }

            //copy to job2            
            for (int i = 0; i < numForJob2; i++)
            {
                newJob02[i] = arrayOfViDi2IIamge[listIndex[numForJob1 + i]];

                //add jpu and image information
                newJob02[i].gpuName = "HDM-Red-1";
            }

            gtupleJobsFifo02.FifoJob01 = newJob01;
            gtupleJobsFifo02.FifoJob02 = newJob02;

        }
        private void ImageBufferToJobsMult(IIMageFifo[] arrayOfViDi2IIamge)
        {
            //numberOfImagesToProcess = f(arrayOfViDi2IIamge)
            //newJob01 = Floor(numberOfImagesToProcess/2)
            //newJob02 = numberOfImagesToProcess = newJob01
            //gtupleJobsFifo02.FifoJob01 = newJob01;
            //gtupleJobsFifo02.FifoJob02 = newJob02;

            int numberOfImagesToProcess = numToProcess(arrayOfViDi2IIamge);

            //int numForJob2 = numberOfImagesToProcess / 2;
            //int numForJob1 = numberOfImagesToProcess - numForJob2;

            IIMageFifo[] newJob01 = new IIMageFifo[numberOfImagesToProcess];
            IIMageFifo[] newJob02 = new IIMageFifo[numberOfImagesToProcess];

            //sort by xNewImage = ture
            //arrayOfViDi2IIamge[13].xNewImage = false; testing only
            //arrayOfViDi2IIamge[13].imageIndex = 0;    testing only
            int[] listIndex = findInsertsOnaLine(arrayOfViDi2IIamge);

            //copy to job1            
            for (int i = 0; i < numberOfImagesToProcess; i++)
            {
                newJob01[i] = arrayOfViDi2IIamge[listIndex[i]];

                //add jpu name
                newJob01[i].gpuName = "HDM-Red-0";
            }

            //copy to job2            
            for (int i = 0; i < numberOfImagesToProcess; i++)
            {
                newJob02[i] = arrayOfViDi2IIamge[listIndex[i]];

                //add jpu and image information
                newJob02[i].gpuName = "HDM-Red-1";
            }

            gtupleJobsFifo02.FifoJob01 = newJob01;

            //if(!xSingleGpu)
            gtupleJobsFifo02.FifoJob02 = newJob02;

        }
        private int numToProcess(IIMageFifo[] arrayOfViDi2IIamge)
        {
            int numberToProcess = 0;


            foreach(IIMageFifo item in arrayOfViDi2IIamge)
            {
                if (item.xNewImage)
                    numberToProcess++;
            }






            return numberToProcess;
        }
        private int[] findInsertsOnaLine(IIMageFifo[] List2Sort)
        {
            

            int[] iSetOut = null;
            var trueState = (from n1 in List2Sort

                             where ( n1.xNewImage == true)

                             select n1.imageIndex).ToList();

            iSetOut = trueState.ToArray(); //indexes from SortedList1 


            return iSetOut;

        }
        #endregion

        private void copyToClipBoardTSM1_Click(object sender, EventArgs e)
        {
            if (this.lstGPU2log.SelectedItem != null)
            {
                //select all
                int iItems = lstGPU2log.Items.Count;
                for (int i = 0; i < iItems; i++)
                {
                    lstGPU2log.SelectedIndex = i;

                }

                //copy to clip board
                Clipboard.Clear();
                //lstJsonsLblInfo
                int iSelectedNum = lstGPU2log.SelectedItems.Count;
                string sss = "";
                //string[] sSelectedItems = new string[iSelectedNum];
                if (iSelectedNum > 0)
                {
                    foreach (string item in lstGPU2log.SelectedItems)
                    {
                        sss = sss + item + "\r\n";

                    }

                    Clipboard.SetText(sss);
                }
                else
                {
                    MessageBox.Show("Select a row!");
                }

                //unselect
                lstGPU2log.ClearSelected();
            }
            else
            {
                MessageBox.Show("Nothing Selected!");
            }
           
        }
        private void logToClipBoard(ref ListBox lsBox)
        {
            if (lsBox.SelectedItem != null)
            {
                //select all
                int iItems = lsBox.Items.Count;
                for (int i = 0; i < iItems; i++)
                {
                    lsBox.SelectedIndex = i;

                }

                //copy to clip board
                Clipboard.Clear();
                //lstJsonsLblInfo
                int iSelectedNum = lsBox.SelectedItems.Count;
                string sss = "";
                //string[] sSelectedItems = new string[iSelectedNum];
                if (iSelectedNum > 0)
                {
                    foreach (string item in lsBox.SelectedItems)
                    {
                        sss = sss + item + "\r\n";

                    }

                    Clipboard.SetText(sss);
                }
                else
                {
                    MessageBox.Show("Select a row!");
                }

                //unselect
                lstGPU2log.ClearSelected();
            }
            else
            {
                MessageBox.Show("Nothing Selected!");
            }
        }
        private void clearLogTSMI1_Click(object sender, EventArgs e)
        {
            this.lstGPU2log.Items.Clear();
        }
        private void btnTest9_Click(object sender, EventArgs e)
        {
            //string imagesPath00 = @"C:\ProgramData\Cognex\VisionPro Deep Learning\2.1\Examples\";
            xLoading = true;
            string imagesPath = "";
            int numImages2Load = -1;


            //imagesPath = imagesPath00 + @"resources\images\Iscar";

            imagesPath = @"S:\A03 - Tools\A03-HW-M70 Pressing.Systems\Public\SC End Mills Inspection\Yoav\PROJ_403_Test_Images";

            numImages2Load = -1;
            loadIImageBufferAsnc(imagesPath, numImages2Load);

            xLoading = false;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            lblCopyOfLblCount.Text = frmBeckhoff.mFormBeckhoffDefInstance.lblCount.Text;
        }
        private void btnGetROI_Click(object sender, EventArgs e)
        {
            try
            {


                roiFromVision.X      = Convert.ToSingle(frmBeckhoff.mFormBeckhoffDefInstance.txtRectPointX.Text.Trim());
                roiFromVision.Y      = Convert.ToSingle(frmBeckhoff.mFormBeckhoffDefInstance.txtRectPointY.Text.Trim());
                roiFromVision.Width  = Convert.ToSingle(frmBeckhoff.mFormBeckhoffDefInstance.txtSearchAreaWidth.Text.Trim());
                roiFromVision.Height = Convert.ToSingle(frmBeckhoff.mFormBeckhoffDefInstance.txtSearchAreaHeight.Text.Trim());

                //remove roi before image taking to get the full 20 MGP image
                frmBeckhoff.mFormBeckhoffDefInstance.chkUseSearchArea.Checked = false;

            }
            catch(System.Exception e1)
            {
                MessageBox.Show("btnGetROI_Click(), Error: " + e1.Message);
            }

            //txtRectPointX
            //txtRectPointY

            //txtSearchAreaWidth
            //txtSearchAreaHeight
        }
        private void btnModelsSetPath_Click(object sender, EventArgs e)
        {

        }
        private void btnSaveModelsToINI_Click(object sender, EventArgs e)
        {
            string INIpath = Application.StartupPath + @"\Data\Models.ini";
            SaveModels(INIpath);
        }
        private void copyToClipBoardTSM2_Click(object sender, EventArgs e)
        {
            if (lstMarking.SelectedItem != null)
            {
                //select all
                int iItems = lstMarking.Items.Count;
                for (int i = 0; i < iItems; i++)
                {
                    lstMarking.SelectedIndex = i;

                }

                //copy to clip board
                Clipboard.Clear();
                //lstJsonsLblInfo
                int iSelectedNum = lstMarking.SelectedItems.Count;
                string sss = "";
                //string[] sSelectedItems = new string[iSelectedNum];
                if (iSelectedNum > 0)
                {
                    foreach (string item in lstMarking.SelectedItems)
                    {
                        sss = sss + item + "\r\n";

                    }

                    Clipboard.SetText(sss);
                }
                else
                {
                    MessageBox.Show("Select a row!");
                }

                //unselect
                lstMarking.ClearSelected();
            }
            else
            {
                MessageBox.Show("Nothing Selected!");
            }
        }
        private void cms1LogList_Opening(object sender, CancelEventArgs e)
        {

        }

        private void chkFifoMode_CheckStateChanged(object sender, EventArgs e)
        {
            
        }

        private void chkSingleGpu_CheckedChanged(object sender, EventArgs e)
        {
            if(chkSingleGpu.Checked)
                xSingleGpu = true;
            else
                xSingleGpu = false;
        }

        private async void btnBackgroundWorker_Click(object sender, EventArgs e)
        {
            try
            {
               inv.set(btnBackgroundWorker, "Enabled", false);
               var task=Task.Run(()=>StartTaske1());
               await task;
               inv.set(btnBackgroundWorker, "Enabled", true);
            }
            catch (System.Exception ex) { inv.set(btnBackgroundWorker, "Enabled", true); };
        }

        private void lstGPU2log_DoubleClick(object sender, EventArgs e)
        {
            invy.ClearListBox(lstGPU2log);
        }

        private void lstMarking_DoubleClick(object sender, EventArgs e)
        {
            invy.ClearListBox(lstMarking);
        }
        bool CancelAsync = false;
        private void btnBGWstop_Click(object sender, EventArgs e)
        {
            //BackRoundTasks.CancelAsync();

            DateTime now = DateTime.Now;
            string timenow14 = string.Format("{0:dd-MM-yy,HH:mm:ss}", now);
            loadLog(timenow14 + ": End background Task");
            xContinue = false;
            CancelAsync = true;
        }
    }

    //The class EventArg -> necesary to do corect the job    
    public class WFEventArg : EventArgs //System.EventArgs, is the class for classes containing event data!
    {

        #region Class level def.
        public readonly string TheString;
        #endregion

        //constructor
        public WFEventArg(string s)
        {
            TheString = s;
        }

    }


}

    class test1IImageFactory : IImageFactory
    {
        public IImage Load(Stream stream, ViDi2.ImageFormat hint = ViDi2.ImageFormat.PNG)
        {
           
            throw new NotImplementedException();
        }
    }
//}
