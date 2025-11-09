using BackroundWork;
using BeckhoffBasler;
using Emgu.CV.Flann;
using Google.Protobuf.WellKnownTypes;
using IniFiles;
using INIgetset;
//using Modbusrun;
using Inspection;
using Inspection;
using Newtonsoft.Json;
using SuaKITEvaluatorBatch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.ServiceModel.Channels;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using TwinCAT.TypeSystem;
using ViDi;
using ViDi.NET;
using ViDi2;
using ViDi2.Runtime;
using ViDi2.UI;
using ViDi2.UI.ViewModels;
using static RuntimeMultiGPU2.frmMain;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolBar;
using Encoder = System.Drawing.Imaging.Encoder;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using InvalidOperationException = System.InvalidOperationException;
using SystemColors = System.Drawing.SystemColors;




//using ViDi2.VisionPro;



namespace RuntimeMultiGPU2
{
    public partial class frmMain : Form
    {
        private bool m_bLayoutCalled = false;
        private DateTime m_dt;
        //public static frmBeckhoff mFrmBeckhoff;

        //private frmBeckhoff frmBeckhoff;
        //private Queue<PictureBoxEventArgs> eventQueue;
        private bool isProcessing;
        //constructor
        private Queue<Tuple<Image, PictureBox>> imageQueue2 = new Queue<Tuple<Image, PictureBox>>();
        public Queue<Image> imageQueue1 = new Queue<Image>();
        public struct MyEventArg
        {
            public string txt;
        }
        public struct CustomEventArgInt
        {
            public int iint;
        }
        public class CustomEventArgIntRef : EventArgs
        {
            public int Value { get; set;}
        }


        public event EventHandler<MyEventArg> ListClicked;
        public delegate void EventHendler(object sender, MyEventArg e);
        public MyEventArg MyE = new MyEventArg();

        public event EventHandler<CustomEventArgInt> onExposureChangedFromCatalogue;
        public delegate void CustomEventHandler(object sender, CustomEventArgInt e);
        public CustomEventArgInt myCustomEventArgInt = new CustomEventArgInt();

        public event EventHandler<CustomEventArgIntRef> onExposureChangedFromBeckofForm;
        public delegate void CustomEventHandlerRef(object sender, CustomEventArgIntRef e);
        public CustomEventArgIntRef myCustomEventArgIntRef = new CustomEventArgIntRef();


        private bool bIsOperatorMode = false;
        public bool bDefectFoundInTopInspection=false;
        public bool bDefectFoundInFrontInspection = false;

        public void RegisterOperatorAllowed(params System.Windows.Forms.Control[] controls)
        {
            foreach (var c in controls)
            {
                if (c != null) _operatorAllowed.Add(c);
            }
        }
        private void ApplyRecursive(System.Windows.Forms.Control parent)
        {
            foreach (System.Windows.Forms.Control c in parent.Controls)
            {
                // Tool/menu/status strips stay enabled so user can switch modes
                if (IsAlwaysEnabled(c))
                {
                    // Still traverse children in case there are embedded controls
                    if (c.HasChildren) ApplyRecursive(c);
                    continue;
                }

                // Keep container controls enabled to avoid blocking allowed children
                if (IsContainer(c))
                {
                    if (c.HasChildren) ApplyRecursive(c);
                    continue;
                }

                // Labels should stay enabled (for readability)
                if (c is Label)
                {
                    if (c.HasChildren) ApplyRecursive(c);
                    continue;
                }

                bool enable = (!bIsOperatorMode || _operatorAllowed.Contains(c));

                c.Enabled = enable;

                if (c.HasChildren) ApplyRecursive(c);
            }
        }

        private static bool IsAlwaysEnabled(System.Windows.Forms.Control c) =>
            c is ToolStrip || c is MenuStrip || c is StatusStrip;

        private static bool IsContainer(System.Windows.Forms.Control c) =>
            c is Panel ||
            c is GroupBox ||
            c is TabControl ||
            c is TabPage ||
            c is TableLayoutPanel ||
            c is FlowLayoutPanel ||
            c is SplitContainer ||
            c is SplitterPanel;



        // Call this once (e.g., in Form.Load) to wire all TextBoxes under 'root'
        public static void AttachValidateEventToTextBoxes(System.Windows.Forms.Control root)
        {
            foreach (System.Windows.Forms.Control c in root.Controls)
            {
                if (c is System.Windows.Forms.TextBox tb)
                {
                    if (tb.Name == "txtListBox1")
                        continue;
                    tb.KeyPress -= OnKeyPressNumeric;
                    tb.KeyPress -= OnKeyPressIntegerNumeric;
                    if(tb.Name.Contains("txt") && tb.Name.Contains("Lower"))
                        tb.KeyPress += OnKeyPressNumeric;
                    else
                        tb.KeyPress += OnKeyPressIntegerNumeric;

                    tb.Validating -= OnValidatingNumeric;
                    tb.Validating += OnValidatingNumeric;
                }

                if (c.HasChildren)
                    AttachValidateEventToTextBoxes(c); // recurse into child containers
            }
        }

        private static void OnKeyPressNumeric(object sender, KeyPressEventArgs e)
        {
            HandleNumeric(sender, e);
        }

        private static void OnKeyPressIntegerNumeric(object sender, KeyPressEventArgs e)
        {
            HandleNumeric(sender, e, true);
        }

        private static void HandleNumeric(object sender, KeyPressEventArgs e, bool bIsInteger = false)
        {
            var tb = (System.Windows.Forms.TextBox)sender;

            // Allow control keys (Backspace, etc.)
            if (char.IsControl(e.KeyChar))
                return;

            string dec = "."; // CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string neg = "-"; // CultureInfo.CurrentCulture.NumberFormat.NegativeSign;

            // Allow digits
            if (char.IsDigit(e.KeyChar))
                return;

            string ch = e.KeyChar.ToString();

            // Allow one decimal separator
            if (!bIsInteger)
            {
                if (ch == dec)
                {
                    bool hasDec = tb.Text.Contains(dec);
                    bool selectionHasDec = tb.SelectedText.Contains(dec);
                    if (hasDec && !selectionHasDec)
                        e.Handled = true; // second separator not allowed
                    return;
                }
            }

            // Allow a single leading minus (or replacing the whole text)
            if (ch == neg)
            {
                bool hasNeg = tb.Text.Contains(neg) && !tb.SelectedText.Contains(neg);
                bool caretAtStart = tb.SelectionStart == 0;
                bool replacingAll = tb.SelectionLength == tb.TextLength;
                if (hasNeg || !(caretAtStart || replacingAll))
                    e.Handled = true;
                return;
            }

            // Block everything else (letters, spaces, etc.)
            e.Handled = true;
        }

        private static void OnValidatingNumeric(object sender, CancelEventArgs e)
        {
            var tb = (System.Windows.Forms.TextBox)sender;

            // Allow empty as valid (remove this if you want to require a number)
            if (string.IsNullOrWhiteSpace(tb.Text))
                return;

            // Final check on focus change (catches paste cases like "3.3.3" or "4-")
            if (!decimal.TryParse(tb.Text/*, NumberStyles.Number, CultureInfo.CurrentCulture*/, out _))
            {
                e.Cancel = true; // keep focus in the textbox
                tb.SelectAll();
                try { System.Media.SystemSounds.Beep.Play(); } catch { /* ignore */ }
            }
        }




        //NPNP
        //DONT use both flags as true
        public enum eCognexROIType
        {
                eUseWholeImageAsROI = 1,
                eUseWholeImageMinus400pixlesAsROI=2,
                UseAsBeforeGeographicROI=-1
        }
        public eCognexROIType m_eCognexROIType = eCognexROIType.eUseWholeImageAsROI;
        //public enum bUseWholeImageAsROI = true;
        //public bool bUseWholeImageMinus400pixlesAsROI = true;
        double m_dTotalFrontCognexTime=0;
        string m_sTotalFrontCognexTime = "";

        public int _eSnapShotStrategy = -1;

        HashSet<string> sInferenceResults = new HashSet<string>();
        bool bDataInControlsChanged = false;
        bool bIsCatalogueNumberChanging = false;
        bool bRunPrepareOnLoad = true;

        //Policy: Run only one .Process() method per brain
        bool bRunProcessOnceForAllROI = true;

        bool bWasProcessRunForPeels = false;
        public bool LoadedBackup = false;
        public string PartNumber = "";

        //public static frmFront frmNewFront;
        //public static frmFront frmFrontInspect1;
        //public static frmMain frmmain = new frmMain();
        public frmMain()
        {
            InitializeComponent();
            
            StartImageProcessingTask();
            //frmNewFront = new frmFront();
            //frmNewFront.Show();
            //backoff.PictureBoxImageChanged += FrmBeckhoff_PictureBoxImageChanged;
            //eventQueue = new Queue<PictureBoxEventArgs>();
            //isProcessing = false;

            //// Show frmBeckhoff
            //backoff.Show();

        }
        

        //public void AddImageToQueue(Image img, PictureBox pictureBox)
        //{
        //    if (img != null && pictureBox != null)
        //    {
        //        imageQueue2.Enqueue(new Tuple<Image, PictureBox>(img, pictureBox));
        //        Console.WriteLine("Image added to the queue. Queue size: " + imageQueue2.Count);

        //        // Trigger the image processing if the queue is not empty
        //        if (imageQueue2.Count == 1)
        //        {
        //            ProcessNextImageInQueue();
        //        }
        //    }
        //    else
        //    {
        //        Console.WriteLine("Image or PictureBox is null. Cannot enqueue.");
        //    }
        //}

        //private void ProcessNextImageInQueue()
        //{
        //    if (imageQueue.Count > 0)
        //    {
        //        // Dequeue the image and its PictureBox
        //        var imgAndPictureBox = imageQueue2.Dequeue();
        //        PictureBox pictureBox = imgAndPictureBox.Item2;
        //        Console.WriteLine("Processing image...");

        //        // Create PictureBoxEventArgs using the PictureBox
        //        PictureBoxEventArgs e = new PictureBoxEventArgs(pictureBox);

        //        // Call the actual processing method with the PictureBoxEventArgs
        //        HandlePictureBoxImageChanged(e);

        //        // After processing, call the function again if there are more images in the queue
        //        if (imageQueue.Count > 0)
        //        {
        //            ProcessNextImageInQueue();
        //        }
        //    }
        //}

        private void ProcessNewImage(Image img)
        {
            // Implement your image processing logic here
            Console.WriteLine("Processing image...");
            // For example, display the image in a PictureBox or perform other tasks
        }

        public void SomeEventTrigger()
        {
            // Simulate an image capture or another event that updates a picture box
            int pictureBoxIndex = 0; // Index of the picture box to update
            Image newImage = Image.FromFile("path_to_your_image.jpg"); // Load or capture your image here
                                                                       //backoff.OnPictureBoxImageChange(pictureBoxIndex, newImage);
        }

        private void OpenFrmBeckhoff()
        {
            //frmBeckhoff beckhoffForm = new frmBeckhoff();
            //beckhoffForm.Show();
        }

        public static IImage BitmapToViDiImage(Bitmap bitmap)
        {
            // Create an instance of the ViDiImage
            //ViDiImage vidiImage = new ViDiImage();
            // Lock the bitmap's bits
            //BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            //                                        ImageLockMode.ReadOnly,
            //                                        PixelFormat.Format24bppRgb);
            //ViDi2.IImage img = new ViDi2.FormsImage(bitmap);
            //img.Dispose();
            //try
            //{
            //    // Create a byte array to hold the pixel data
            //    int bytes = Math.Abs(bitmapData.Stride) * bitmap.Height;
            //    byte[] rgbValues = new byte[bytes];

            //    // Copy the RGB values into the array
            //    Marshal.Copy(bitmapData.Scan0, rgbValues, 0, bytes);

            //    // Set the pixel data and properties in the ViImage
            //   // img.SetPixels(rgbValues, bitmap.Width, bitmap.Height, bitmapData.Stride);
            //}
            //finally
            //{
            //    // Unlock the bits
            //    bitmap.UnlockBits(bitmapData);
            //}

            MemoryStream ms0 = new MemoryStream();
            //bitmap.Save(ms0, System.Drawing.Imaging.ImageFormat.Jpeg);
            bitmap.Save(ms0, System.Drawing.Imaging.ImageFormat.Bmp);
            byte[] bmpBytes = ms0.ToArray();
            int lineWidth = (bitmap.Width * 24 + 7) / 8; //ok, check agenst line 1531 libraryImage         
            
            //ViDi 
            int colorChannels = 3;
            ViDi2.ByteImage byteImage = new ByteImage(bitmap.Width, bitmap.Height, colorChannels, ImageChannelDepth.Depth8, bmpBytes, lineWidth);
            



            return byteImage;
        }



        public static IImage ConvertBitmapToViDiImage(Bitmap bitmap)
        {
            // Ensure the bitmap is not null
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap), "Bitmap cannot be null.");
            }

            // Create a ViDi2.FormsImage from the bitmap
            IImage vidiImage = new FormsImage(bitmap);

            // Dispose the bitmap if you don't need it anymore
            // bitmap.Dispose(); // Uncomment if you want to dispose of bitmap

            return vidiImage;
        }

        //private void FrmBeckhoff_PictureBoxImageChanged(object sender, PictureBoxEventArgs e)
        //{
        //    lock (eventQueue)
        //    {
        //        eventQueue.Enqueue(e);
        //    }
        //    ProcessQueue();
        //}
        //private void ProcessQueue()
        //{
        //    if (isProcessing)
        //        return;

        //    isProcessing = true;

        //    while (true)
        //    {
        //        PictureBoxEventArgs eventArgs;
        //        lock (eventQueue)
        //        {
        //            if (eventQueue.Count == 0)
        //            {
        //                isProcessing = false;
        //                return;
        //            }
        //            eventArgs = eventQueue.Dequeue();
        //        }

        //        // Handle the event
        //        HandlePictureBoxImageChanged(eventArgs);
        //    }
        //}

        //private void HandlePictureBoxImageChanged(PictureBoxEventArgs e)
        //{
        //    listBox1.Items.Clear();
        //    string iniFileName = CmbCatNum.Text + ".ini";
        //    string iniFilePath = System.Windows.Forms.Application.StartupPath + @"\Data\DataBase\" + iniFileName;
        //    List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();
        //    ViDi2.Runtime.IRedHighDetailParameters hdRedParamPeels = grunTimeWorkapace.StreamDict[Model2Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
        //    ViDi2.Runtime.IRedHighDetailParameters hdRedParamsBreake = grunTimeWorkapace.StreamDict[Model1Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
        //    System.Windows.Rect ROIrect = new System.Windows.Rect();
        //    //if (chkAutoROI.Checked)
        //    //ROIrect = GetDeticatedRoiFromList(Convert.ToInt32(0));
        //    Bitmap image =(Bitmap) e.PictureBox.Image;
        //    var convImage1 = ConvertBitmapToViDiImage(image);
        //    var convImage = BitmapToViDiImage(image);
        //    //set fructions threshold
        //    SetThreshold(hdRedParamsBreake, EndData.FracLowerThreshold, EndData.FracLowerThreshold);
        //    //set peels threshold
        //    SetThreshold(hdRedParamPeels, EndData.PeelLowerThreshold, EndData.PeelUpperThreshold);
        //    Rectangle ImageDimensions = new Rectangle(0, 0, image.Width, image.Height);
        //    //apply roi


        //    //add if setup-need to chec frac or peels
        //    ISample samp1 = grunTimeWorkapace.StreamDict[Model1Name].Process(convImage);
        //    ISample samp2 = grunTimeWorkapace.StreamDict[Model2Name].Process(convImage);

        //    Dictionary<string, IMarking> mark = samp1.Markings;
        //    Dictionary<string, IMarking> mark2 = samp2.Markings;

        //    Dictionary<string, IMarking>.KeyCollection MarkKey = mark.Keys;
        //    Dictionary<string, IMarking>.KeyCollection MarkKey2 = mark2.Keys;

        //    IMarking TryM = mark["red_HDM_20M_5472x3648"];
        //    IMarking TryM2 = mark2["red_HDM_20M_5472x3648"];

        //    ViDi2.IView View = TryM.Views[0];// mm.Marking.Views[0];
        //    ViDi2.IView View2 = TryM2.Views[0];

        //    ViDi2.IRedView redview = (ViDi2.IRedView)View;
        //    ViDi2.IRedView redview2 = (ViDi2.IRedView)View2;

        //    RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
        //    RegionFound[] regionFound2 = new RegionFound[redview2.Regions.Count];

        //    ViDi2.Runtime.IRedTool tool = (ViDi2.Runtime.IRedTool)grunTimeWorkapace.StreamDict[Model1Name].Tools.First();
        //    ViDi2.Runtime.IRedTool tool2 = (ViDi2.Runtime.IRedTool)grunTimeWorkapace.StreamDict[Model2Name].Tools.First();

        //    var knownClasses = tool.KnownClasses;
        //    var knownClasses2 = tool2.KnownClasses;

        //    string className = knownClasses[0];
        //    string className2 = knownClasses2[0];
        //    string[] s = className.Split('_');
        //    string[] s2 = className2.Split('_');
        //    string cn = "";
        //    string cn2 = "";

        //    int index2 = 0;
        //    int Iindex = 0;
        //    resArr.resIndex = 0;
        //    resArr.resInfo.ShowRes = new string[20];
        //    int index = 0;
        //    if (EndData.Roi1 == "true")
        //    {
        //        ROIrect.X = EndData.roiPosX1;
        //        ROIrect.Y = EndData.roiPosY1;
        //        ROIrect.Width = EndData.roiWidth1;
        //        ROIrect.Height = EndData.roiHeight1;
        //        if (EndData.IsFractions1 == "true")
        //        {
        //            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1);
        //            samp1.Process();
        //            getRagion(samp1, Model1Name, index, Iindex, regionFound);

        //            //add founds to list.
        //        }
        //        if (EndData.IsPeels1 == "true")
        //        {
        //            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2);
        //            samp2.Process();
        //            getRagion(samp2, Model2Name, index2, Iindex, regionFound2);
        //            //add founds to list.
        //        }
        //    }
        //    if (EndData.Roi2 == "true")
        //    {
        //        ROIrect.X = EndData.roiPosX2;
        //        ROIrect.Y = EndData.roiPosY2;
        //        ROIrect.Width = EndData.roiWidth2;
        //        ROIrect.Height = EndData.roiHeight2;
        //        if (EndData.IsFractions2 == "true")
        //        {
        //            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1);
        //            samp1.Process();
        //            getRagion(samp1, Model1Name, index, Iindex, regionFound);
        //            //add founds to list.
        //        }
        //        if (EndData.IsPeels2 == "true")
        //        {
        //            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2);
        //            samp2.Process();
        //            getRagion(samp2, Model2Name, index2, Iindex, regionFound2);
        //            //add founds to list.
        //        }
        //    }
        //    if (EndData.Roi3 == "true")
        //    {
        //        ROIrect.X = EndData.roiPosX3;
        //        ROIrect.Y = EndData.roiPosY3;
        //        ROIrect.Width = EndData.roiWidth3;
        //        ROIrect.Height = EndData.roiHeight3;
        //        if (EndData.IsFractions3 == "true")
        //        {
        //            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1);
        //            samp1.Process();
        //            getRagion(samp1, Model1Name, index, Iindex, regionFound);
        //            //add founds to list.
        //        }
        //        if (EndData.IsPeels3 == "true")
        //        {
        //            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2);
        //            samp2.Process();
        //            getRagion(samp2, Model2Name, index2, Iindex, regionFound2);
        //            //add founds to list.
        //        }
        //    }
        //    Console.WriteLine($"PictureBox {e.PictureBox.Name} image changed.");
        //}

        public struct EndmillParameters
        {
            public int CatalogNo;
            public int BladesNo;
            public int Diameter;
            public int Length;
            public float FracLowerThreshold;
            public float FracUpperThreshold;
            public float PeelLowerThreshold;
            public float PeelUpperThreshold;
            public int roiPosX1;
            public int roiPosY1;
            public int roiWidth1;
            public int roiHeight1;
            public float roiAngle1;
            public float roiRatio1;
            public int roiPosX2;
            public int roiPosY2;
            public int roiWidth2;
            public int roiHeight2;
            public float roiAngle2;
            public float roiRatio2;
            public int roiPosX3;
            public int roiPosY3;
            public int roiWidth3;
            public int roiHeight3;
            public float roiAngle3;
            public float roiRatio3;
            public string ImagePath;
            public string IsFractions1;
            public string IsPeels1;
            public string IsFractions2;
            public string IsPeels2;
            public string IsFractions3;
            public string IsPeels3;
            public string Roi1;
            public string Roi2;
            public string Roi3;
            public float FracLowerArea;
            public float FracUpperArea;
            public float PeelLowerArea;
            public float PeelUpperArea;


        }

        public struct DefectToRect
        {
            public RegionFound regions;
            public int DefectId;
            public string DefectInfo;
        }

        public struct RunTimeWorkapace
        {
            public int gpuId01;
            public int gpuId02;
            public Dictionary<string, ViDi2.Runtime.IStream> StreamDict;
            public bool xNoError;
        }

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
            public double X0;
            public double Y0;
            public double H;
            public double W;


        }

        public struct Models
        {
            public string path;
            public string model1FileName;
            public string model2FileName;
        }

        public struct ResInfo
        {
            public string[] ShowRes;
            public int DefectId;

        }

        public struct ResArr
        {
            public ResInfo resInfo;
            public int resIndex;
        }
        private ConcurrentQueue<string> imageQueue = new ConcurrentQueue<string>(); // Queue to hold image 
        private CancellationTokenSource cancellationTokenSource;
        private Task processingTask;
        ViDi2.Runtime.IWorkspace workspace;
        ViDi2.Runtime.IStream stream;
        private ViDi2.Runtime.IControl control;
        private ViDi2.Runtime.IWorkspaceList rwsl;
        private string INIPath = sDirpath + @"Data\INI.ini";
        RunTimeWorkapace grunTimeWorkapace = new RunTimeWorkapace();
        private System.Windows.Rect currentROI = new System.Windows.Rect();
        string Model1Name = "Brake";
        string Model2Name = "Peel";
        string Model1NameFront = "BrakeFront";
        //string Model2NameFront = "PeelFront";
        string GImagePath;
        private Models gmodels = new Models();


        // Set the path to your JSON file
        //static public string JassonPath = @"C:\Users\inspmachha\Desktop\setUpApplication - Copy\projSampaleViewer\bin\x64\Debug\Data\DataBase\EndmillsData.Jason";
        //static 
        public static string JassonPath = AppDomain.CurrentDomain.BaseDirectory + @"..\..\..\..\setUpApplication\projSampaleViewer\bin\x64\Debug\Data\DataBase\EndmillsData.Jason";
        public  string FrontPath = @"C:\Project\Cam2BaslerML\Cam2BaslerML\bin\Debug";
        public string sJassonPath = JassonPath;
        string wsName;
        public DefectToRect[] defectToRect;
        ResArr resArr = new ResArr();
        RegionFound[] BreakeRegions;
        RegionFound[] PealRegions;
        JassonClass JassonDataClass = new JassonClass();
        BeckhoffBasler.Endmill EndData = new BeckhoffBasler.Endmill();
        BlockingCollection<System.Drawing.Image> imageQueueN = new BlockingCollection<System.Drawing.Image>();
        //NPNP
        private List<BeckhoffBasler.Endmill> endmills;
        //private List<EndmillData> endmills;

        public string[] SnapFile = new string[16];
        public struct IIMageFifo
        {
            public bool xNewImage;
            public int imageIndex;
            public string imageName;
            public ViDi2.IImage iimage;
            public string gpuName;
            public bool xEvaluationDone;
        }
        public IIMageFifo[] arrayOfViDi2IIamge = new IIMageFifo[16];
        //public Image[] ImageFile = new Image[16];
        public bool StopCycle = false;
        //public bool SaveOnDisk = true;
        public Image[] SnapImage = new Image[16];
        public MemoryStream[] StreamImage = new MemoryStream[16];
        public Stream[] MstreamBr = new Stream[16];
        public Stream[] MstreamPl = new Stream[16];
        public System.Drawing.Bitmap[] SnapBitmap = new Bitmap[16];
        public int numBufferSize = 0;
        public bool UseMemory = false;

        public ViDi2.ByteImage Bitmap2ViDi2ByteImage(Bitmap bmp)
        {
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

        public event PropertyChangedEventHandler PropertyChanged;
        public IList<ViDi2.Runtime.IWorkspace> Workspaces => Control.Workspaces.ToList();

        public ViDi2.Runtime.IStream StreamAll
        {
            get { return stream; }
            set
            {
                stream = value;
                RaisePropertyChanged(nameof(StreamAll));
            }
        }
       

        public ViDi2.Runtime.IWorkspace Workspace
        {
            get { return workspace; }
            set
            {
                workspace = value;
                StreamAll = workspace.Streams.First();
                RaisePropertyChanged(nameof(Workspace));
            }
        }

        public ViDi2.Runtime.IControl Control
        {
            get { return control; }
            set
            {
                control = value;
                RaisePropertyChanged(nameof(Control));
                RaisePropertyChanged(nameof(Workspaces));  //;
                RaisePropertyChanged(nameof(Stream));
            }
        }
        private void RaisePropertyChanged(string prop)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
        private void initProporties()
        {
            ViDi2.Runtime.IStream stream = StreamAll;
            ViDi2.Runtime.IWorkspace workspace = Workspace;
            ViDi2.Runtime.IControl control = Control;
            IList<ViDi2.Runtime.IWorkspace> ws = Workspaces;
        }

        private void loadModels()
        {
            try
            {
                string modelPath = @"C:\Users\inspmachha\Desktop\final models";
                IniFileClass AppliIni = new IniFileClass(INIPath);
                modelPath = AppliIni.ReadValue("Last Model", "Full path", "");

                if (true)
                {
                    using (var fs = new System.IO.FileStream(modelPath, System.IO.FileMode.Open, FileAccess.Read))
                    {
                        System.Windows.Forms.Application.DoEvents();

                        //Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                        string[] s0 = modelPath.Split('\\');

                        if (Workspace != null)
                            if (Workspace.UniqueName == s0[s0.GetLength(0) - 1].Substring(0, s0[s0.GetLength(0) - 1].Length - 5)) { System.Windows.Forms.MessageBox.Show("This Model Is Already Loaded!"); goto exitProcedure; }

                        if (Workspace != null)
                            if (Workspace.IsOpen)
                                Workspace.Close();

                        bool xSingleLoad = false;
                        if (xSingleLoad)
                        {
                            Workspace = control.Workspaces.Add(Path.GetFileNameWithoutExtension(modelPath), fs);
                        }

                        rwsl = control.Workspaces;
                        Workspace = rwsl[rwsl.Names[0]];

                        string[] s = new string[0];
                        if (Workspace.Parameters.Description != "")
                            s = Workspace.Parameters.Description.Split(' ');
                        if (s.GetLength(0) > 0)
                        {
                            float f = System.Convert.ToSingle(s[s.GetLength(0) - 1]);
                        }

                    }
                }

                RaisePropertyChanged(nameof(Workspaces));

                stream = Workspace.Streams.First();

                string streamName = stream.Name;
                ViDi2.Runtime.ITool tool = Workspace.Streams[streamName].Tools.First();

                //class name display
                ViDi2.Runtime.IRedTool tool1 = (ViDi2.Runtime.IRedTool)stream.Tools.First();
                var knownClasses = tool1.KnownClasses;
                if (knownClasses.Count > 0)
                {
                    string className = knownClasses[0];
                }

                //will activate event on lst
                string fn = Path.GetFileName(modelPath);
                //int index = this.lstModels.Items.IndexOf(fn);
                //this.lstModels.SelectedIndex = index;

            }
            catch (ViDi2.Exception e)
            {
                System.Windows.Forms.MessageBox.Show("loadModel(), Error: " + e.Message);
            }

        exitProcedure:;

        }
        public static string sDirpath;
        bool bUse2GPUs = true;
        //bool bTest4Workspaces_Test1stWorkspacePair = true;

        private void frmMain_Load(object sender, EventArgs e)
        {
            inv.set(this, "DoubleBuffered", true);
            sDirpath = AppDomain.CurrentDomain.BaseDirectory;
            //C:\Project\4.2.2025\InspectSolution\runTimeApp 18.03.25 DArrayP\BeckhoffBasler\bin\Debug
            //"C:\\Project\\4.2.2025\\InspectSolution\\BeckhoffBaslerTasks\\BeckhoffBasler\\bin\\Debug\\"
            StringBuilder sb = new StringBuilder(sDirpath);
            sb.Replace("BeckhoffBaslerTasks", @"runTimeApp 18.03.25 DArrayP");
            sDirpath = sb.ToString();
            EndData = JassonDataClass.getJassonParameters("Emdmill1");
            btnStartEval.Enabled = false;
            string INIpath = sDirpath + @"Data\Models.ini";
            INIPath = sDirpath + @"Data\INI.ini";

            gmodels = getModels(INIpath);
            var control = new ViDi2.Runtime.Local.Control(ViDi2.GpuMode.Deferred);
            //var control = new ViDi2.Runtime.Local.Control(ViDi2.GpuMode.SingleDevicePerTool);
            // Initializes all CUDA devices
            control.InitializeComputeDevices(ViDi2.GpuMode.SingleDevicePerTool, new List<int>() { });
            // Turns off optimized GPU memory since high-detail mode doesn't support it
            var computeDevices = control.ComputeDevices;
            control.OptimizedGPUMemory(0);//0
            this.Control = control;
            var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();
            string gpuID = "default/red_HDM_20M_5472x3648/0";
            string wsName = Model1Name;
            //string wsPath = sDirpath + @"Data\final models\Proj_021_201223_104500_21122023_104445.vrws";
            string wsPath = sDirpath + @"Data\final models\TopEdge3_8_25WithNewImages.vrws";
            
            string wsName2 = Model2Name;
            //NPNP
            //string wsPath2 = sDirpath + @"\Data\final models\WS_Proj_022_261223_111400_261223_183645.vrws";
            //string wsPath2 = sDirpath + @"\Data\final models\TopTrainingPeels_30_6_25_Labeled34Images.vrws";
            //string wsPath2 = sDirpath + @"\Data\final models\TopTrainingPeels_09_07_25_77ImagesLabeled.vrws";
            string wsPath2 = sDirpath + @"\Data\final models\TopTrainingPeels_31_07_25_77ImagesLabeledYellowTrayAndSupriseBoxRemovedWhiteUndistinct.vrws";

            string wsNameFront = Model1NameFront;
            //string wsPathFront = sDirpath + @"Data\final models\FrontCSInspectionFullArea3Lightings.vrws";
            string wsPathFront = sDirpath + @"Data\final models\FrontCSInspectionFullArea3LightingsWith002DefectQuick.vrws";
            //string wsName2front = Model2NameFront;
            string wsPath2Front = sDirpath + @"\Data\final models\FrontCSInspectionFullArea3Lightings.vrws";

            if (bUse2GPUs)
            {
                StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName+"1", control.Workspaces.Add(wsName+"1", wsPath, "default/Analyze/1").Streams["default"]);
                //StreamDict.Add(wsName + "2", control.Workspaces.Add(wsName + "2", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "3", control.Workspaces.Add(wsName + "3", wsPath, "default/Analyze/1").Streams["default"]);
                //StreamDict.Add(wsName + "4", control.Workspaces.Add(wsName + "4", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "5", control.Workspaces.Add(wsName + "5", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "6", control.Workspaces.Add(wsName + "6", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "7", control.Workspaces.Add(wsName + "7", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "8", control.Workspaces.Add(wsName + "8", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "9", control.Workspaces.Add(wsName + "9", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "10", control.Workspaces.Add(wsName + "10", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "11", control.Workspaces.Add(wsName + "11", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "12", control.Workspaces.Add(wsName + "12", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "13", control.Workspaces.Add(wsName + "13", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "14", control.Workspaces.Add(wsName + "14", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "15", control.Workspaces.Add(wsName + "15", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "16", control.Workspaces.Add(wsName + "16", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "17", control.Workspaces.Add(wsName + "17", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "18", control.Workspaces.Add(wsName + "18", wsPath, "default/Analyze/0").Streams["default"]);
                //StreamDict.Add(wsName + "19", control.Workspaces.Add(wsName + "19", wsPath, "default/Analyze/0").Streams["default"]);
                StreamDict.Add(wsName2, control.Workspaces.Add(wsName2, wsPath2, "default/Analyze/1").Streams["default"]);
            }
            else
            {
                StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath, gpuID).Streams["default"]);
                StreamDict.Add(wsName2, control.Workspaces.Add(wsName2, wsPath2, gpuID).Streams["default"]);
            }

            StreamDict.Add(wsNameFront, control.Workspaces.Add(wsNameFront, wsPathFront, gpuID).Streams["default"]);
            //StreamDict.Add(wsName2front, control.Workspaces.Add(wsName2front, wsPath2Front, gpuID).Streams["default"]);

            grunTimeWorkapace.gpuId01 = 0;
            grunTimeWorkapace.StreamDict = StreamDict;
            string jsonContent = File.ReadAllText(JassonPath);
            List<Dictionary<string, object>> endmillData = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonContent);
            List<string> endmillNames = new List<string>();

            foreach (var data in endmillData)
            {
                if (data.ContainsKey("EndmillName"))
                {
                    string endmillName = data["EndmillName"].ToString();
                    endmillNames.Add(endmillName);
                }
            }
            LoadEndmills();
            LoadEndmills1();
            endmillNames.Sort();
            CmbCatNum.DataSource = endmillNames;
            CmbCatNum1.DataSource = endmillNames;
            CmbCatNumText = CmbCatNum.Text;
            CmbCatNumText1 = CmbCatNum1.Text;
            
            //frmFrontInspect1 = new frmFront();
            initProporties();
            //loadModels();

            this.Text = this.Text + " Version " + Assembly.GetExecutingAssembly().GetName().Version?.ToString();

            PrepairEval(new WpfImage(sDirpath + "snap8.jpg"), true);
            PrepairEval(new WpfImage(sDirpath + "snap8.jpg"), false);
            PrepairEvalFront(new WpfImage(sDirpath + "snap8.jpg"), true);
            //PrepairEvalFront(new WpfImage(@"C:\Project\4.2.2025\InspectSolution\runTimeApp 18.03.25 DArrayP\BeckhoffBasler\bin\Debug\snap8.jpg"), true);
            AttachChangeEventToTextAndCheckBoxes(this.Controls);
            RegisterOperatorAllowed(btnOperatorTechnician);
            AttachValidateEventToTextBoxes(this);


        }



        private bool EvaluateImage(int gpuId, string wsName, int num, bool bIsFractionsBrain)
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            string ls, rs;
            string imgPath;
            int iterCount = 1;

            ls = (gpuId == 0) ? ("ENTER : " + wsName) : "...";
            rs = (gpuId == 0) ? "..." : ("ENTER : " + wsName);
            Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')}");

            sw.Restart();
            {
                WpfImage image = null;
                //NPNP ask SHURA the image may not here yet
                while (imageCleS[num] == null)
                {
                    Thread.Sleep(20);
                }
                AddList("Start EvaluateImage " + (num + 1).ToString()+ " Fractions=" + bIsFractionsBrain.ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                image = imageCleS[num];

                if (bIsFractionsBrain)
                {
                    ViDi2.Runtime.IRedHighDetailParameters hdRedParamsBreake = grunTimeWorkapace.StreamDict[Model1Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                    //Filter only for area. Score will be filtered via threshold
                    string filter = "area>= " + txtFractionLowerArea.Text;// + " and score>=" + txtFractionScore.Text.Trim();
                    hdRedParamsBreake.RegionFilter = filter;

                    //always pass 1 as higher threshold
                    SetThreshold(hdRedParamsBreake, EndData.FracLowerThreshold, 1);

                }
                else
                {
                    ViDi2.Runtime.IRedHighDetailParameters hdRedParamPeels = grunTimeWorkapace.StreamDict[Model2Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                    //Filter only for area. Score will be filtered via threshold
                    string filter = "area>= " + txtPeelLowerArea.Text;// + " and score>=" + txtFractionScore.Text.Trim();
                    hdRedParamPeels.RegionFilter = filter;

                    //always pass 1 as higher threshold
                    SetThreshold(hdRedParamPeels, EndData.PeelLowerThreshold, 1);
                }




                //byte[] bytes = StreamImage[num].ToArray();
                //StreamImage[num].Seek(0, System.IO.SeekOrigin.Begin);
                // MstreamBr[i] = new MemoryStream(bytes);
                //MstreamPl[i] = new MemoryStream(bytes);

                //    }
                //}
                //if (UseMemory)
                //{

                //    if (imageCle != null && MstreamBr[i] != null)
                //    {

                //imageCle[i] = new ViDi2.UI.WpfImage(MstreamBr[i]);


                byte[] bytes = StreamImage[num].ToArray();
                StreamImage[num].Seek(0, System.IO.SeekOrigin.Begin);
                MemoryStream MstreamBr = new MemoryStream(bytes);
                image = new WpfImage(MstreamBr);

                //image = new WpfImage(StreamImage[num]);
                //image = new WpfImage(@"C:\Rejects\7271583_5667916\1 25-09-11 10-55-13\snap15.jpg");
                using (ISample sample = grunTimeWorkapace.StreamDict[wsName].CreateSample(image))
                {
                    // process all tools on stream with specific gpu(gpuId)
                    sample.Process(null, new List<int>() { gpuId });

                    if (bIsFractionsBrain)
                    {
                        if (EndData.Roi1 == "true")
                            getRegion(sample, Model1Name, regionFound, num, 1);
                        if (EndData.Roi2 == "true")
                            getRegion(sample, Model1Name, regionFound, num, 2);
                        if (EndData.Roi3 == "true")
                            getRegion(sample, Model1Name, regionFound, num, 3);
                    }
                    else
                    {
                        if (EndData.Roi1 == "true")
                            getRegion(sample, Model2Name, regionFound2, num, 1);
                        if (EndData.Roi2 == "true")
                            getRegion(sample, Model2Name, regionFound2, num, 2);
                        if (EndData.Roi3 == "true")
                            getRegion(sample, Model2Name, regionFound2, num, 3);
                    }
                }
                sw.Stop();

                ls = (gpuId == 0) ? ("EXIT  : " + wsName) : "...";
                rs = (gpuId == 0) ? "..." : ("EXIT  : " + wsName);
                Console.WriteLine($" 0 : {ls.PadRight(24, ' ')} | 1 : {rs.PadRight(24, ' ')} => {iterCount} images in {sw.ElapsedMilliseconds} ms");
            }
            Console.WriteLine($" 0 : {((gpuId == 0) ? "TERMINATE" : "...").PadRight(24, ' ')} | 1 : {((gpuId == 0) ? "..." : "TERMINATE").PadRight(24, ' ')}");
            AddList("Fini EvaluateImage " + (num + 1).ToString() + " Fractions=" + bIsFractionsBrain.ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
            return true;
        }


        public Models getModels(string INIpath)
        {
            Models models = new Models();
            
            IniFile AppliIni = IniFile.FromFile(INIpath);

            models.path = AppliIni["Models"]["path"];
            models.model1FileName = AppliIni["Models"]["model1 name"];
            models.model2FileName = AppliIni["Models"]["model2 name"];

            return models;
        }

        public void ApplyROIRectFrontFractions(Rectangle ImageDimensions, ViDi2.IManualRegionOfInterest redROI01)
        {

            redROI01.Parameters.Units = ViDi2.UnitsMode.Pixel;
            redROI01.Parameters.Offset = new ViDi2.Point(0, 0);
            redROI01.Parameters.Size = new ViDi2.Size(ImageDimensions.Width, ImageDimensions.Height);
            redROI01.Parameters.Units = ViDi2.UnitsMode.Pixel;
        }

        public void ApplyROIRect(bool xNoSave, Rectangle ImageDimensions, bool xAutoROIU, System.Windows.Rect rect, ISample mySamp, int roi,
            ViDi2.IManualRegionOfInterest redROI01)
        {

            //NPNP
            //use the whole image as ROI. Later use a constant ROI and use the same for training and inference ONCE per brain
            //the "ROIs" in the GUI will be used as post process
            if (m_eCognexROIType==eCognexROIType.eUseWholeImageAsROI)
                return;
            else if (m_eCognexROIType == eCognexROIType.eUseWholeImageMinus400pixlesAsROI)
            {
                redROI01.Parameters.Units = ViDi2.UnitsMode.Pixel;
                redROI01.Parameters.Offset = new ViDi2.Point(0, 400);
                redROI01.Parameters.Size = new ViDi2.Size(ImageDimensions.Width, ImageDimensions.Height-800);
                return;
            }
            //ViDi2.IManualRegionOfInterest redROI01;
            //this if exists only to allow compatibility to other calls to ApplyROIRect
            //those calls however may be working incorrectly as they use StreamAll that always point to fractions' stream
            //in init and is not updated. These other calls should be fixed too only the else should exist
            //if (redROI0 == null)
            //    redROI01 = (ViDi2.IManualRegionOfInterest)StreamAll.Tools.First().RegionOfInterest;
            //else
            //    redROI01 = redROI0;

            redROI01.Parameters.Units = ViDi2.UnitsMode.Pixel;
            if (!chkFullImg.Checked)
            {
                if (roi == 1)
                {
                    double ROIXpos = 0;
                    double ROIYpos = 0;
                    double ROIwidth = 0;
                    double ROIheight = 0;
                    double ROIangle = 0;
                    if (!xAutoROIU)
                    {
                        ROIXpos = EndData.roiPosX1;
                        ROIYpos = EndData.roiPosY1;
                        ROIwidth = EndData.roiWidth1;
                        ROIheight = EndData.roiHeight1;
                        ROIangle = EndData.roiAngle1;
                    }
                    else //auto-mode
                    {
                        ROIXpos = rect.X;
                        ROIYpos = rect.Y;
                        ROIwidth = rect.Width;
                        ROIheight = rect.Height;
                        ROIangle = 0;
                    }

                    redROI01.Parameters.Offset = new ViDi2.Point(ROIXpos, ROIYpos);
                    redROI01.Parameters.Size = new ViDi2.Size(ROIwidth, ROIheight);

                    currentROI.X = redROI01.Parameters.Offset.X;
                    currentROI.Y = redROI01.Parameters.Offset.Y;

                    currentROI.Width = redROI01.Parameters.Size.Width;
                    currentROI.Height = redROI01.Parameters.Size.Height;

                    ViDi2.Size size = redROI01.Parameters.Scale;


                    size.Height = 1;
                    size.Width = 1;

                    redROI01.Parameters.Scale = size; //tested, size = roi scale with respect to image;
                    size.Width = currentROI.Width;
                    size.Height = currentROI.Height;

                    if (currentROI.Height != 1 && currentROI.Width != 1 && (xNoSave))
                    {
                        IniFileClass AppliIni = new IniFileClass(INIPath);
                        AppliIni.WriteValue("roi", "x", currentROI.X.ToString());
                        AppliIni.WriteValue("roi", "y", currentROI.Y.ToString());
                        AppliIni.WriteValue("roi", "width", currentROI.Width.ToString());
                        AppliIni.WriteValue("roi", "height", currentROI.Height.ToString());

                        AppliIni.WriteValue("roi", "used", chkUsePPevaluationROI.Checked.ToString());
                    }

                }

                if (roi == 2)
                {
                    double ROIXpos1 = 0;
                    double ROIYpos1 = 0;
                    double ROIwidth1 = 0;
                    double ROIheight1 = 0;
                    double ROIangle1 = 0;

                    if (!xAutoROIU)
                    {
                        ROIXpos1 = EndData.roiPosX2;
                        ROIYpos1 = EndData.roiPosY2;
                        ROIwidth1 = EndData.roiWidth2;
                        ROIheight1 = EndData.roiHeight2;
                        ROIangle1 = EndData.roiAngle2;
                    }
                    else //auto-mode
                    {
                        ROIXpos1 = rect.X;
                        ROIYpos1 = rect.Y;
                        ROIwidth1 = rect.Width;
                        ROIheight1 = rect.Height;
                        ROIangle1 = 0;
                    }

                    redROI01.Parameters.Offset = new ViDi2.Point(ROIXpos1, ROIYpos1);
                    redROI01.Parameters.Size = new ViDi2.Size(ROIwidth1, ROIheight1);

                    currentROI.X = redROI01.Parameters.Offset.X;
                    currentROI.Y = redROI01.Parameters.Offset.Y;

                    currentROI.Width = redROI01.Parameters.Size.Width;
                    currentROI.Height = redROI01.Parameters.Size.Height;

                    ViDi2.Size size = redROI01.Parameters.Scale;

                    size.Height = 1;
                    size.Width = 1;

                    redROI01.Parameters.Scale = size; //tested, size = roi scale with respect to image;

                    if (currentROI.Height != 1 && currentROI.Width != 1 && (xNoSave))
                    {
                        IniFileClass AppliIni = new IniFileClass(INIPath);
                        AppliIni.WriteValue("roi", "x", currentROI.X.ToString());
                        AppliIni.WriteValue("roi", "y", currentROI.Y.ToString());
                        AppliIni.WriteValue("roi", "width", currentROI.Width.ToString());
                        AppliIni.WriteValue("roi", "height", currentROI.Height.ToString());

                        AppliIni.WriteValue("roi", "used", chkUsePPevaluationROI.Checked.ToString());
                    }
                }

                if (roi == 3)
                {
                    double ROIXpos2 = 0;
                    double ROIYpos2 = 0;
                    double ROIwidth2 = 0;
                    double ROIheight2 = 0;
                    double ROIangle2 = 0;

                    if (!xAutoROIU)
                    {
                        ROIXpos2 = EndData.roiPosX3;
                        ROIYpos2 = EndData.roiPosY3;
                        ROIwidth2 = EndData.roiWidth3;
                        ROIheight2 = EndData.roiHeight3;
                        ROIangle2 = EndData.roiAngle3;
                    }
                    else //auto-mode
                    {
                        ROIXpos2 = rect.X;
                        ROIYpos2 = rect.Y;
                        ROIwidth2 = rect.Width;
                        ROIheight2 = rect.Height;
                        ROIangle2 = 0;
                    }

                    redROI01.Parameters.Offset = new ViDi2.Point(ROIXpos2, ROIYpos2);
                    redROI01.Parameters.Size = new ViDi2.Size(ROIwidth2, ROIheight2);

                    currentROI.X = redROI01.Parameters.Offset.X;
                    currentROI.Y = redROI01.Parameters.Offset.Y;

                    currentROI.Width = redROI01.Parameters.Size.Width;
                    currentROI.Height = redROI01.Parameters.Size.Height;

                    ViDi2.Size size = redROI01.Parameters.Scale;

                    size.Height = 1;
                    size.Width = 1;

                    redROI01.Parameters.Scale = size; //tested, size = roi scale with respect to image;

                    if (currentROI.Height != 1 && currentROI.Width != 1 && (xNoSave))
                    {
                        IniFileClass AppliIni = new IniFileClass(INIPath);
                        AppliIni.WriteValue("roi", "x", currentROI.X.ToString());
                        AppliIni.WriteValue("roi", "y", currentROI.Y.ToString());
                        AppliIni.WriteValue("roi", "width", currentROI.Width.ToString());
                        AppliIni.WriteValue("roi", "height", currentROI.Height.ToString());
                        AppliIni.WriteValue("roi", "used", chkUsePPevaluationROI.Checked.ToString());
                    }
                }
            }
            else
            {
                if (!(currentROI.Height == 1 && currentROI.Width == 1))
                {
                    IniFileClass AppliIni = new IniFileClass(INIPath);
                    AppliIni.WriteValue("roi", "used", chkUsePPevaluationROI.Checked.ToString());
                }
            }
           // redROI01 = (ViDi2.IManualRegionOfInterest)Stream.Tools.First().RegionOfInterest;
        }
        //There can only be **ONE** ROI and it should
        public bool IsRegionInDefectRoi(ViDi2.IRegion region, int iDefectROINumber)
        {
            //    return true;
            int iDefectROIWidth = 0;
            int iDefectROIHeight = 0;
            int iDefectROIRightMostX = 0;
            int iDefectROILeftMostX = 0;
            int iDefectROITopY = 0;
            int iDefectROIBottomY = 0;

            switch (iDefectROINumber)
            {
                case 1:
                    iDefectROIWidth = EndData.roiWidth1;
                    iDefectROIHeight = EndData.roiHeight1;
                    iDefectROIRightMostX = EndData.roiPosX1 + EndData.roiWidth1;
                    iDefectROILeftMostX = EndData.roiPosX1;
                    iDefectROITopY = EndData.roiPosY1;
                    iDefectROIBottomY = EndData.roiPosY1 + EndData.roiHeight1;
                    break;

                case 2:
                    iDefectROIWidth = EndData.roiWidth2;
                    iDefectROIHeight = EndData.roiHeight2;
                    iDefectROIRightMostX = EndData.roiPosX2 + EndData.roiWidth2;
                    iDefectROILeftMostX = EndData.roiPosX2;
                    iDefectROITopY = EndData.roiPosY2;
                    iDefectROIBottomY = EndData.roiPosY2 + EndData.roiHeight2;
                    break;

                case 3:
                    iDefectROIWidth = EndData.roiWidth3;
                    iDefectROIHeight = EndData.roiHeight3;
                    iDefectROIRightMostX = EndData.roiPosX3 + EndData.roiWidth3;
                    iDefectROILeftMostX = EndData.roiPosX3;
                    iDefectROITopY = EndData.roiPosY3;
                    iDefectROIBottomY = EndData.roiPosY3 + EndData.roiHeight3;
                    break;

                default:
                    System.Windows.MessageBox.Show("Invalid defect ROI number: " + iDefectROINumber);
                    break;
            }

            // Check if the region is within the defect ROI.
            // Even if PART of the region is within the defect ROI, it is considered valid.
            if (region.Center.X + region.Width / 2 > iDefectROILeftMostX &&
                region.Center.X - region.Width / 2 < iDefectROIRightMostX &&
                region.Center.Y + region.Height / 2 > iDefectROITopY &&
                region.Center.Y - region.Height / 2 < iDefectROIBottomY)
                return true;
            return false;
        }



        public void ApplyROIRectCognex(bool xNoSave, Rectangle ImageDimensions, bool xAutoROIU, System.Windows.Rect rect, ISample mySamp, int roi)
        {
            ViDi2.IManualRegionOfInterest redROI01 = (ViDi2.IManualRegionOfInterest)StreamAll.Tools.First().RegionOfInterest;
            redROI01.Parameters.Units = ViDi2.UnitsMode.Pixel;
            if (!chkFullImg.Checked)
            {

                redROI01.Parameters.Offset = new ViDi2.Point(rect.X, rect.Y);
                redROI01.Parameters.Size = new ViDi2.Size(rect.Width, rect.Height);

                currentROI.X = redROI01.Parameters.Offset.X;
                currentROI.Y = redROI01.Parameters.Offset.Y;

                currentROI.Width = redROI01.Parameters.Size.Width;
                currentROI.Height = redROI01.Parameters.Size.Height;

                ViDi2.Size size = redROI01.Parameters.Scale;


                //NPNP
                //are those lines needed?  Can't we put it straight to redROI01.Parameters.Scale ???
                size.Height = 1;
                size.Width = 1;

                redROI01.Parameters.Scale = size; //tested, size = roi scale with respect to image;
                size.Width = currentROI.Width;
                size.Height = currentROI.Height;

            }

        }
        // redROI01 = (ViDi2.IManualRegionOfInterest)Stream.Tools.First().RegionOfInterest;

        EndmillParameters GetIniInfo(string iniFilePath, string endmillName)
        {
            EndmillParameters myParam = new EndmillParameters();
            using (StreamReader reader = new StreamReader(iniFilePath))
            {
                Dictionary<string, Dictionary<string, string>> iniData = new Dictionary<string, Dictionary<string, string>>();
                string line;
                string currentSection = null;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2);
                        iniData[currentSection] = new Dictionary<string, string>();
                    }
                    if (!string.IsNullOrEmpty(currentSection) && line.Contains("="))
                    {
                        string[] parts = line.Split(new char[] { '=' }, 2);
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();
                        iniData[currentSection][key] = value;
                    }
                }

                // Retrieve the BladesNo of the specified Endmill
                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("CatalogNO"))
                {
                    if (int.TryParse(iniData[endmillName]["CatalogNO"], out int Catalog))
                    {
                        myParam.CatalogNo = Catalog;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("BladesNo"))
                {
                    if (int.TryParse(iniData[endmillName]["BladesNo"], out int bladesNo))
                    {
                        myParam.BladesNo = bladesNo;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("Diameter"))
                {
                    if (int.TryParse(iniData[endmillName]["Diameter"], out int diam))
                    {
                        myParam.Diameter = diam;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("Length"))
                {
                    if (int.TryParse(iniData[endmillName]["Length"], out int Len))
                    {
                        myParam.Length = Len;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("FracLowerThreshold"))
                {
                    if (float.TryParse(iniData[endmillName]["FracLowerThreshold"], out float low))
                    {
                        myParam.FracLowerThreshold = low;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("FracUpperThreshold"))
                {
                    if (float.TryParse(iniData[endmillName]["FracUpperThreshold"], out float upp))
                    {
                        myParam.FracUpperThreshold = upp;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("PeelLowerThreshold"))
                {
                    if (float.TryParse(iniData[endmillName]["PeelLowerThreshold"], out float low))
                    {
                        myParam.PeelLowerThreshold = low;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("PeelUpperThreshold"))
                {
                    if (float.TryParse(iniData[endmillName]["PeelUpperThreshold"], out float upp))
                    {
                        myParam.PeelUpperThreshold = upp;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiPosX1"))
                {
                    if (int.TryParse(iniData[endmillName]["roiPosX1"], out int posX1))
                    {
                        myParam.roiPosX1 = posX1;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiPosY1"))
                {
                    if (int.TryParse(iniData[endmillName]["roiPosY1"], out int posY1))
                    {
                        myParam.roiPosY1 = posY1;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiWidth1"))
                {
                    if (int.TryParse(iniData[endmillName]["roiWidth1"], out int width1))
                    {
                        myParam.roiWidth1 = width1;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiHeight1"))
                {
                    if (int.TryParse(iniData[endmillName]["roiHeight1"], out int height1))
                    {
                        myParam.roiHeight1 = height1;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiAngle1"))
                {
                    if (float.TryParse(iniData[endmillName]["roiAngle1"], out float angle1))
                    {
                        myParam.roiAngle1 = angle1;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiRatio1"))
                {
                    if (float.TryParse(iniData[endmillName]["roiRatio1"], out float ratio1))
                    {
                        myParam.roiRatio1 = ratio1;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiPosX2"))
                {
                    if (int.TryParse(iniData[endmillName]["roiPosX2"], out int posX2))
                    {
                        myParam.roiPosX2 = posX2;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiPosY2"))
                {
                    if (int.TryParse(iniData[endmillName]["roiPosY2"], out int posY2))
                    {
                        myParam.roiPosY2 = posY2;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiWidth2"))
                {
                    if (int.TryParse(iniData[endmillName]["roiWidth2"], out int width2))
                    {
                        myParam.roiWidth2 = width2;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiHeight2"))
                {
                    if (int.TryParse(iniData[endmillName]["roiHeight2"], out int height2))
                    {
                        myParam.roiHeight2 = height2;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiAngle2"))
                {
                    if (float.TryParse(iniData[endmillName]["roiAngle2"], out float angle2))
                    {
                        myParam.roiAngle2 = angle2;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiRatio2"))
                {
                    if (float.TryParse(iniData[endmillName]["roiRatio2"], out float ratio2))
                    {
                        myParam.roiRatio2 = ratio2;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiPosX3"))
                {
                    if (int.TryParse(iniData[endmillName]["roiPosX3"], out int posX3))
                    {
                        myParam.roiPosX3 = posX3;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiPosY3"))
                {
                    if (int.TryParse(iniData[endmillName]["roiPosY3"], out int posY3))
                    {
                        myParam.roiPosY3 = posY3;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiWidth3"))
                {
                    if (int.TryParse(iniData[endmillName]["roiWidth3"], out int width3))
                    {
                        myParam.roiWidth3 = width3;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiHeight3"))
                {
                    if (int.TryParse(iniData[endmillName]["roiHeight3"], out int height3))
                    {
                        myParam.roiHeight3 = height3;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiAngle3"))
                {
                    if (float.TryParse(iniData[endmillName]["roiAngle3"], out float angle3))
                    {
                        myParam.roiAngle3 = angle3;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("roiRatio3"))
                {
                    if (float.TryParse(iniData[endmillName]["roiRatio3"], out float ratio3))
                    {
                        myParam.roiRatio3 = ratio3;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("ImagePath"))
                {
                    string ImagePath = iniData[endmillName]["ImagePath"];
                    myParam.ImagePath = ImagePath;
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("IsFractions1"))
                {
                    string IsFractions1 = iniData[endmillName]["IsFractions1"];
                    myParam.IsFractions1 = IsFractions1;
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("IsPeels1"))
                {
                    string IsPeels1 = iniData[endmillName]["IsPeels1"];
                    myParam.IsPeels1 = IsPeels1;
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("IsFractions2"))
                {
                    string IsFractions2 = iniData[endmillName]["IsFractions2"];
                    myParam.IsFractions2 = IsFractions2;
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("IsPeels2"))
                {
                    string IsPeels2 = iniData[endmillName]["IsPeels2"];
                    myParam.IsPeels2 = IsPeels2;
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("IsFractions3"))
                {
                    string IsFractions3 = iniData[endmillName]["IsFractions3"];
                    myParam.IsFractions3 = IsFractions3;
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("IsPeels3"))
                {
                    string IsPeels3 = iniData[endmillName]["IsPeels3"];
                    myParam.IsPeels3 = IsPeels3;
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("Roi1"))
                {
                    string roi1 = iniData[endmillName]["Roi1"];
                    myParam.Roi1 = roi1;
                }
                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("Roi2"))
                {
                    string roi2 = iniData[endmillName]["Roi2"];
                    myParam.Roi2 = roi2;
                }
                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("Roi3"))
                {
                    string roi3 = iniData[endmillName]["Roi3"];
                    myParam.Roi3 = roi3;
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("FracLowerArea"))
                {
                    if (float.TryParse(iniData[endmillName]["FracLowerArea"], out float low))
                    {
                        myParam.FracLowerArea = low;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("FracUpperArea"))
                {
                    if (float.TryParse(iniData[endmillName]["FracUpperArea"], out float upp))
                    {
                        myParam.FracUpperArea = upp;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("PeelLowerArea"))
                {
                    if (float.TryParse(iniData[endmillName]["PeelLowerArea"], out float low))
                    {
                        myParam.PeelLowerArea = low;
                    }
                }

                if (iniData.ContainsKey(endmillName) && iniData[endmillName].ContainsKey("PeelUpperArea"))
                {
                    if (float.TryParse(iniData[endmillName]["PeelUpperArea"], out float upp))
                    {
                        myParam.PeelUpperArea = upp;
                    }
                }
            }

            return myParam;
        }

        private void btnShoePeel_Click(object sender, EventArgs e)
        {
            if (CmbCatNum.Text == "")
            {
                System.Windows.Forms.MessageBox.Show("Please Choose Endmill");
                goto exitProcedure;
            }
            modelToRun(1);
            string iniFileName = CmbCatNum.Text + ".ini";
            string iniFilePath = sDirpath + @"Data\DataBase\" + iniFileName;
            EndmillParameters parameters = new EndmillParameters();
            parameters.BladesNo = GetIniInfo(iniFilePath, CmbCatNum.Text).BladesNo;
            parameters.Diameter = GetIniInfo(iniFilePath, CmbCatNum.Text).Diameter;
            parameters.Length = GetIniInfo(iniFilePath, CmbCatNum.Text).Length;
            parameters.FracLowerThreshold = GetIniInfo(iniFilePath, CmbCatNum.Text).FracLowerThreshold;
            parameters.FracUpperThreshold = GetIniInfo(iniFilePath, CmbCatNum.Text).FracUpperThreshold;
            parameters.PeelLowerThreshold = GetIniInfo(iniFilePath, CmbCatNum.Text).PeelLowerThreshold;
            parameters.PeelUpperThreshold = GetIniInfo(iniFilePath, CmbCatNum.Text).PeelUpperThreshold;
            parameters.roiPosX1 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiPosX1;
            parameters.roiPosY1 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiPosY1;
            parameters.roiWidth1 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiWidth1;
            parameters.roiHeight1 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiHeight1;
            parameters.roiAngle1 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiAngle1;
            parameters.roiRatio1 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiRatio1;
            parameters.roiPosX2 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiPosX2;
            parameters.roiPosY2 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiPosY2;
            parameters.roiWidth2 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiWidth2;
            parameters.roiHeight2 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiHeight2;
            parameters.roiAngle2 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiAngle2;
            parameters.roiRatio2 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiRatio2;
            parameters.roiPosX3 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiPosX3;
            parameters.roiPosY3 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiPosY3;
            parameters.roiWidth3 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiWidth3;
            parameters.roiHeight3 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiHeight3;
            parameters.roiAngle3 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiAngle3;
            parameters.roiRatio3 = GetIniInfo(iniFilePath, CmbCatNum.Text).roiRatio3;
            parameters.CatalogNo = GetIniInfo(iniFilePath, CmbCatNum.Text).CatalogNo;
            parameters.ImagePath = GetIniInfo(iniFilePath, CmbCatNum.Text).ImagePath;
            parameters.IsFractions1 = GetIniInfo(iniFilePath, CmbCatNum.Text).IsFractions1;
            parameters.IsPeels1 = GetIniInfo(iniFilePath, CmbCatNum.Text).IsPeels1;
            parameters.IsFractions2 = GetIniInfo(iniFilePath, CmbCatNum.Text).IsFractions2;
            parameters.IsPeels2 = GetIniInfo(iniFilePath, CmbCatNum.Text).IsPeels2;
            parameters.IsFractions3 = GetIniInfo(iniFilePath, CmbCatNum.Text).IsFractions3;
            parameters.IsPeels3 = GetIniInfo(iniFilePath, CmbCatNum.Text).IsPeels3;
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();
            ViDi2.Runtime.IRedHighDetailParameters hdRedParamPeels = grunTimeWorkapace.StreamDict[Model2Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
            System.Windows.Rect ROIrect = new System.Windows.Rect();
            if (chkAutoROI.Checked)
                ROIrect = GetDeticatedRoiFromList(Convert.ToInt32(0));
            var image = new ViDi2.UI.WpfImage(parameters.ImagePath);   // imagePath);
            Rectangle ImageDimensions = new Rectangle(0, 0, image.Width, image.Height);
            if (checkedListBox1.SelectedItem == "Use ROI")
            {
                if (!chkROI1.Checked && !chkROI2.Checked && !chkROI3.Checked)
                {
                    System.Windows.Forms.MessageBox.Show("Click At Least One ROI Area ROI#1, ROI#2 OR ROI#3 Or check Full Image");
                    goto exitProcedure;
                }
                //ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect);
            }
            /*set the wanred threshold in the tool properties*/
            SetThreshold(hdRedParamPeels, parameters.PeelLowerThreshold, parameters.PeelUpperThreshold);
            string modelName = "WS_Proj_022_261223_111400_261223_183645.vrws";
            Bitmap bmp = new Bitmap(parameters.ImagePath);
            IImage imageToProc = new ViDi2.Local.LibraryImage(parameters.ImagePath);
            ISample samples1 = ImageEvaloation(grunTimeWorkapace, imageToProc, "Peel");
            ISample samples2 = ImageEvaloation(grunTimeWorkapace, imageToProc, "Brake");
            //ProcessImg(image, samples1, grunTimeWorkapace, modelName, hdRedParamPeels);// apply my process
            List<Dictionary<string, IMarking>> tmplst;
            double dur1;
            double dur2;
            List<string> mark1 = new List<string>();
            using (ISample sample = samples1)   //img1))
            {
                sample.Process(null, new List<int>() { 0 });
                Dictionary<string, IMarking>.ValueCollection values1 = sample.Markings.Values;
                lstIMarking.Add(sample.Markings);
                dur1 = values1.First().Duration;
                for (int i = 0; i < sample.Markings.Count; i++)
                {
                    //string str = values1.First().Views.First();
                }
                
            }

            using (ISample sample = samples2)   //img1))
            {
                sample.Process(null, new List<int>() { 0 });
                Dictionary<string, IMarking>.ValueCollection values2 = sample.Markings.Values;
                lstIMarking.Add(sample.Markings);
                dur2 = values2.First().Duration;
            }
            lblDuration.Text = (dur1 + dur2).ToString();
        //show defect data in the list
        //measure time
        exitProcedure:;
        }

        string[] GetFile(string path)
        {
            string[] s = new string[59];
            int iNumOfParams = 0;
            int iNumOfParams01 = 0;
            int totalNumLines = 0;

            try
            {
                //get number of lines
                using (FileStream fs = File.Open(@path, FileMode.Open))
                {
                    //get total number of lines
                    StreamReader sr = new StreamReader(fs, System.Text.UTF8Encoding.UTF8);
                    while (!sr.EndOfStream)
                    {
                        string ss = sr.ReadLine();
                        iNumOfParams = iNumOfParams + 1;
                    }

                    fs.Close();
                }

                totalNumLines = iNumOfParams + 3;
                s = new string[totalNumLines];

                using (FileStream fs = File.Open(@path, FileMode.Open))
                {
                    StreamReader sr = new StreamReader(fs, System.Text.UTF8Encoding.UTF8);
                    while (!sr.EndOfStream)
                    {
                        string ss = sr.ReadLine();
                        s[iNumOfParams01] = ss;
                        iNumOfParams01 = iNumOfParams01 + 1;
                    }

                    fs.Close();
                    s[totalNumLines - 2] = "OK";
                    s[totalNumLines - 1] = iNumOfParams.ToString();
                }

            }
            catch (System.Exception e)
            {
                if (totalNumLines > 2)
                {
                    s[totalNumLines - 2] = "ERROR";
                }
                else
                {
                    totalNumLines = 3;
                    s = new string[totalNumLines];
                    s[totalNumLines - 2] = "ERROR";
                }

                System.Windows.Forms.MessageBox.Show("GetFile(), Error Getting File : " + e.Message, "Error!");
            }

            return s;
        }

        private System.Windows.Rect GetDeticatedRoiFromList(int ImageIndex)
        {
            System.Windows.Rect rect = new System.Windows.Rect();

            string roiFile = sDirpath + @"Data\ROI Batch\ROIbatch.dat";
            string[] ddROIs = GetFile(roiFile);
            string[] ddROIsDataOnly = new string[ddROIs.GetLength(0) - 3];
            Array.Copy(ddROIs, ddROIsDataOnly, ddROIs.GetLength(0) - 3);

            List<string> lst = new List<string>();
            lst.AddRange(ddROIsDataOnly);

            string result = "";
            List<string> result01 = new List<string>();

            if (ImageIndex < 10)
            {
                result = lst.FirstOrDefault(s => s.Substring(0, 2) == (ImageIndex.ToString() + ","));
                result01 = lst.FindAll(s => s.Substring(0, 2) == (ImageIndex.ToString() + ","));
            }
            else if (ImageIndex > 9 && ImageIndex < 100)
            {
                result = lst.FirstOrDefault(s => s.Substring(0, 3) == (ImageIndex.ToString() + ","));

                result01 = lst.FindAll(s => s.Substring(0, 3) == (ImageIndex.ToString() + ","));

            }
            else if (ImageIndex > 99 && ImageIndex < 1000)
            {
                result = lst.FirstOrDefault(s => s.Substring(0, 4) == (ImageIndex.ToString() + ","));
                result01 = lst.FindAll(s => s.Substring(0, 4) == (ImageIndex.ToString() + ","));
            }
            if (result01.Count > 1)
            {
                //display input dialog for selection of VP to copy as
                System.Windows.Forms.ComboBox list01 = new System.Windows.Forms.ComboBox();
                list01.Items.AddRange(result01.ToArray());

                int iSize = 400;
                string selectedROI = " ";
                ContextMenuStrip contextMenuStrip13 = new ContextMenuStrip();
                bool xNoCancelButton = true;
                DialogResult dr1 = ShowInputDialog(ref selectedROI, list01, "Select roi to use", Color.GreenYellow, iSize, ref contextMenuStrip13, xNoCancelButton);

                if (dr1 == DialogResult.Cancel) { goto exitProcedure; }
                else
                {
                    //continue
                    result = selectedROI;
                }
            }

            string[] sRect = result.Split(',');

            rect.X = Convert.ToDouble(sRect[1]);
            rect.Y = Convert.ToDouble(sRect[2]);
            rect.Width = Convert.ToDouble(sRect[3]);
            rect.Height = Convert.ToDouble(sRect[4]);

        exitProcedure:;
            return rect;

        }

        private static DialogResult ShowInputDialog(ref string input, System.Windows.Forms.ComboBox cmbList, string sHeader, Color color, int iSize, ref ContextMenuStrip cms, bool xNoCancelButton)
        {
            System.Drawing.Size size = new System.Drawing.Size(iSize, 240);  //original = 200, 70

            Form inputBox = new Form();

            inputBox.ControlBox = false;

            inputBox.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            inputBox.ClientSize = size;
            inputBox.Text = sHeader; // "Select Number Of Rules Areas";

            bool xUseTextBox = false;
            if (xUseTextBox)
            {
                System.Windows.Forms.TextBox textBox = new System.Windows.Forms.TextBox();

                int textBoxHeight = 40;
                textBox.Size = new System.Drawing.Size(size.Width - 10, textBoxHeight);
                textBox.Location = new System.Drawing.Point(5, 5);
                textBox.Text = input;
                var font = textBox.Font;

                System.Drawing.Font f = new System.Drawing.Font(font.Name.ToString(), 10.0f);
                textBox.Font = f;

                inputBox.Controls.Add(textBox);
                inputBox.Controls.Add(textBox);
            }
            else
            {
                System.Windows.Forms.Label textBox = new System.Windows.Forms.Label();

                int textBoxHeight = 40;
                textBox.Size = new System.Drawing.Size(size.Width - 10, textBoxHeight);
                textBox.Location = new System.Drawing.Point(5, 5);
                textBox.Text = input;
                var font = textBox.Font;

                System.Drawing.Font f = new System.Drawing.Font(font.Name.ToString(), 10.0f);
                textBox.Font = f;
                textBox.Text = sHeader; // "Select Number Of Rules Areas";
                textBox.TextAlign = ContentAlignment.MiddleCenter;
                textBox.BackColor = color;  //Color.Green;
                inputBox.Controls.Add(textBox);
            }

            //add listbox
            System.Windows.Forms.ComboBox lst = new System.Windows.Forms.ComboBox();
            object[] obj = new object[cmbList.Items.Count];
            cmbList.Items.CopyTo(obj, 0);
            lst.Items.AddRange(obj);
            lst.Text = sHeader; // "Select Number Of Rules Areas";

            lst.Size = new System.Drawing.Size(size.Width - 200, size.Height - 100);
            lst.Location = new System.Drawing.Point(10, 70);
            lst.DropDownStyle = ComboBoxStyle.DropDownList;
            var font1 = lst.Font;
            System.Drawing.Font f1 = new System.Drawing.Font(font1.Name.ToString(), 10.0f);
            lst.Font = f1;
            lst.ContextMenuStrip = cms;

            inputBox.Controls.Add(lst);

            System.Windows.Forms.Button okButton = new System.Windows.Forms.Button();
            okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            okButton.Name = "okButton";
            okButton.Size = new System.Drawing.Size(75, 23);
            okButton.Text = "&OK";

            int btnLocY = size.Height - 40;
            okButton.Location = new System.Drawing.Point(size.Width - 80 - 80, btnLocY);
            inputBox.Controls.Add(okButton);

            System.Windows.Forms.Button cancelButton = new System.Windows.Forms.Button();
            if (!xNoCancelButton)
            {
                cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                cancelButton.Name = "cancelButton";
                cancelButton.Size = new System.Drawing.Size(75, 23);
                cancelButton.Text = "&Cancel";
                cancelButton.Location = new System.Drawing.Point(size.Width - 80, btnLocY);
                inputBox.Controls.Add(cancelButton);
            }

            inputBox.AcceptButton = okButton;
            inputBox.CancelButton = cancelButton;

            DialogResult result = inputBox.ShowDialog();

            input = lst.Text;
            return result;
        }


        public ISample ImageEvaloation(RunTimeWorkapace runTimeWorkapace, IImage iimage, string BrakeOrPeel)
        {
            ISample sample = runTimeWorkapace.StreamDict[BrakeOrPeel].CreateSample(iimage);
            return sample;
        }

        public void SetThreshold(ViDi2.Runtime.IRedHighDetailParameters hdRedParams, double lower1, double upper1)
        {
            bool xNotRedTool = false;
            if (hdRedParams == null) { xNotRedTool = true; }

            if (!xNotRedTool)
            {
                ViDi2.Interval interval = new ViDi2.Interval(lower1, upper1);
                hdRedParams.Threshold = interval;
            }

        exitProcedure:;
        }

        public void modelToRun(int modelIndex)
        {
            Workspace = rwsl[rwsl.Names[modelIndex]];

            stream = Workspace.Streams.First();
            string streamName = stream.Name;  // Workspace.Streams.First().Name;
            ViDi2.Runtime.ITool tool = Workspace.Streams[streamName].Tools.First();
            //class name display
            ViDi2.Runtime.IRedTool tool1 = (ViDi2.Runtime.IRedTool)stream.Tools.First();
            var knownClasses = tool1.KnownClasses;
            if (knownClasses.Count > 0)
            {
                string className = knownClasses[0];
            }

            //get thresholds tool1                
            IRedHighDetailParameters hdRedParams = StreamAll.Tools.First().ParametersBase as IRedHighDetailParameters;

            ViDi2.Interval interval = hdRedParams.Threshold;

        }

        private void ApplyROIRect(string roiNo, Dictionary<string, ViDi2.Runtime.IStream> StreamDict, string wsName, ViDi2.IImage image)
        {
            ViDi2.IManualRegionOfInterest redROI02 = (ViDi2.IManualRegionOfInterest)StreamAll.Tools.First().RegionOfInterest;
            ViDi2.IManualRegionOfInterest redROI01 = (ViDi2.IManualRegionOfInterest)StreamDict[wsName].CreateSample(image);
            redROI01.Parameters.Units = ViDi2.UnitsMode.Pixel;
            if (roiNo == "roi1")
            {
                double ROIXpos = EndData.roiPosX1;
                double ROIYpos = EndData.roiPosY1;
                double ROIwidth = EndData.roiWidth1;
                double ROIheight = EndData.roiHeight1;
                double ROIangle = EndData.roiAngle1;

                redROI01.Parameters.Offset = new ViDi2.Point(ROIXpos, ROIYpos);
                redROI01.Parameters.Size = new ViDi2.Size(ROIwidth, ROIheight);

                EndData.roiPosX1 = (int)redROI01.Parameters.Offset.X;
                EndData.roiPosY1 = (int)redROI01.Parameters.Offset.Y;
                EndData.roiWidth1 = (int)redROI01.Parameters.Size.Width;
                EndData.roiHeight1 = (int)redROI01.Parameters.Size.Height;

                ViDi2.Size size = redROI01.Parameters.Scale;
                size.Height = 1;
                size.Width = 1;
                redROI01.Parameters.Scale = size;
            }

            if (roiNo == "roi2")
            {
                double ROIXpos = EndData.roiPosX2;
                double ROIYpos = EndData.roiPosY2;
                double ROIwidth = EndData.roiWidth2;
                double ROIheight = EndData.roiHeight2;
                double ROIangle = EndData.roiAngle2;

                redROI01.Parameters.Offset = new ViDi2.Point(ROIXpos, ROIYpos);
                redROI01.Parameters.Size = new ViDi2.Size(ROIwidth, ROIheight);

                EndData.roiPosX2 = (int)redROI01.Parameters.Offset.X;
                EndData.roiPosY2 = (int)redROI01.Parameters.Offset.Y;
                EndData.roiWidth2 = (int)redROI01.Parameters.Size.Width;
                EndData.roiHeight2 = (int)redROI01.Parameters.Size.Height;

                ViDi2.Size size = redROI01.Parameters.Scale;
                size.Height = 1;
                size.Width = 1;
                redROI01.Parameters.Scale = size;
            }

            if (roiNo == "roi3")
            {
                double ROIXpos = EndData.roiPosX3;
                double ROIYpos = EndData.roiPosY3;
                double ROIwidth = EndData.roiWidth3;
                double ROIheight = EndData.roiHeight3;
                double ROIangle = EndData.roiAngle3;

                redROI01.Parameters.Offset = new ViDi2.Point(ROIXpos, ROIYpos);
                redROI01.Parameters.Size = new ViDi2.Size(ROIwidth, ROIheight);

                EndData.roiPosX3 = (int)redROI01.Parameters.Offset.X;
                EndData.roiPosY3 = (int)redROI01.Parameters.Offset.Y;
                EndData.roiWidth3 = (int)redROI01.Parameters.Size.Width;
                EndData.roiHeight3 = (int)redROI01.Parameters.Size.Height;

                ViDi2.Size size = redROI01.Parameters.Scale;
                size.Height = 1;
                size.Width = 1;
                redROI01.Parameters.Scale = size;
            }
        }
        /*
         i want that instead of image from a folder,
        i will have an array the will dynamic fill with images.
        i need that when the array will contain at least 1 image the btnStartEval_Click 
        will work on all the images in the array,
        but after it worked on some image it will delete it from the array can you
        help me please?
         */
        
        private async void btnStartEval_Click(object sender, EventArgs e)
        {
            //Stopwatch sw = new Stopwatch();
            try
            {
                inv.set(btnStartEval, "Enabled", false);
                //for (int i = 0; i < SnapFile.Length; i++) SnapFile[i] = "";
                RegionFound1BrSave = new string[1];
                RegionFound1PlSave = new string[1];
                RegIndex = 0;
                RegIndex2 = 0;
                
                //int i = 0;
                string ss = "";
                for (int i = 0; i < SnapFile.Length; i++) if (SnapFile[i] != "") { ss = SnapFile[i]; break; }
                
                var task = Task.Run(() => startEval(ss, 0));
                await task;
                inv.set(btnStartEval, "Enabled", true);
            }
            catch(System.Exception ex) { inv.set(btnStartEval, "Enabled", true); }


        }
        public void imageCleRefresh()
        {
            try
            {
                imageCle = null;
                imageCle = new ViDi2.UI.WpfImage[numBufferSize];
                
                bmpCognex = null;
                bmpCognex = new Bitmap[numBufferSize];

                imageCleS = null;
                imageCleS = new ViDi2.UI.WpfImage[numBufferSize];

                MstreamBr = null;
                MstreamBr = new Stream[numBufferSize];

                MstreamPl = null;
                MstreamPl = new Stream[numBufferSize];

                StreamImage = null;
                StreamImage = new MemoryStream[numBufferSize];
            }
            catch (System.Exception ex) { }
        }
        public async Task<bool> InspectionCycle()
        {
            Stopwatch sw = new Stopwatch();
            Stopwatch sw1 = new Stopwatch();
            bool rep = false;

            try
            {
                bDefectFoundInTopInspection = false;
                //NPNP
                m_dTotalFrontCognexTime = 0;
                m_sTotalFrontCognexTime = "";
                sw.Restart();
                //this.Invoke((Action)(() => { listBox1.Items.Clear();}));
                //this.Invoke((Action)(() => { listBox1.Items.Add("----------Start Inspect Cycle"  + " ------------- //" + DateTime.Now.ToString("HH:mm:ss.fff")); }));
                AddList("----------Start Inspect Cycle" + " ------------- //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                inv.settxt(lblDuration, (0).ToString("0.000"));
                inv.settxt(lblCycleTime, (0).ToString("0.0"));
               
                StopCycle = false;
                int framemax = 0;

                //imageCle = null;
                //if (imageCle == null || imageCle.Length != numBufferSize) imageCle = new ViDi2.UI.WpfImage[numBufferSize];
                timeAll = 0;
                inv.set(txtListBox1,"BackColor",Color.LightGray);
                //bRunPrepareOnLoad = false;
                for (int i = 0; i < numBufferSize; i++)
                {

                    
                        while (imageCle == null ||
                            imageCle.Length <= i ||
                            imageCle[i] == null || (i < numBufferSize  && imageCle[i] == null) || SnapFile[i]=="")
                            //imageCle[i] == null || (i < numBufferSize - 1 && imageCle[i + 1] == null))
                        {
                            Thread.Sleep(10);
                            if (StopCycle)
                            {
                                AddList("Inspect stop" + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); return false; 
                            }
                       

                        }

                    if (StopCycle) { AddList("Inspect stop" + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); return false; }
                   
                    framemax = numBufferSize;
                    if (i > framemax-1) break;
                    
                    inv.settxt(lblTestNum, (i + 1).ToString());
                    
                    sw1.Restart();
                    
                    sInferenceResults.Clear();

                    if (!bUse2GPUs)
                    {
                        //bool bCheckPerformance = true;
                        //if (!bCheckPerformance)
                        //{
                        var task1 = Task.Run(() => startEvalFractions(SnapFile[i], i, null));
                        await task1;
                        if (!task1.Result) return false;

                        var task2 = Task.Run(() => startEvalPeels(SnapFile[i], i));
                        await task2;
                        if (!task2.Result) return false;
                        //}
                        //else
                        //{
                        //    int iDelay = 10;
                        //    var task1 = Task.Run(() => Thread.Sleep(iDelay));

                        //}


                    }
                    else
                    {
                        var InspetionTasks = new List<Task>();

                        //fractions Brain
                        //if (bTest4Workspaces_Test1stWorkspacePair)
                        //{
                            InspetionTasks.Add(Task.Factory.StartNew(() => EvaluateImage(0, grunTimeWorkapace.StreamDict.Keys.ToList()[0].ToString(), i, true/*, SnapFile[i]*/)));
                            Thread.Sleep(20);
                            //Peels Brain
                            InspetionTasks.Add(Task.Factory.StartNew(() => EvaluateImage(1, grunTimeWorkapace.StreamDict.Keys.ToList()[1].ToString(), i, false/*, SnapFile[i]*/)));
                        //}
                        //else
                        //{
                        //    InspetionTasks.Add(Task.Factory.StartNew(() => EvaluateImage(0, grunTimeWorkapace.StreamDict.Keys.ToList()[2].ToString(), i, true/*, SnapFile[i]*/)));
                        //    Thread.Sleep(20);
                        //    //Peels Brain
                        //    InspetionTasks.Add(Task.Factory.StartNew(() => EvaluateImage(1, grunTimeWorkapace.StreamDict.Keys.ToList()[3].ToString(), i, false/*, SnapFile[i]*/)));
                        //}
                        //bTest4Workspaces_Test1stWorkspacePair = !bTest4Workspaces_Test1stWorkspacePair;

                        Task.WaitAll(InspetionTasks.ToArray());
                    }

                    inv.settxt(lblDuration, (sw1.ElapsedMilliseconds / 1000.0).ToString("0.000"));
                    inv.settxt(lblCycleTime, (sw.ElapsedMilliseconds / 1000.0f).ToString("0.0"));
                    Thread.Sleep(2);
                    
                }
                
                //this.Invoke((Action)(() => { listBox1.Items.Add("----------Fini ALL--------------- "  + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); }));
                AddList("----------Fini ALL-------------- - "  + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                inv.set(txtListBox1, "BackColor", Color.White);
                sw.Stop();

                //System.Windows.Forms.MessageBox.Show(m_dTotalFrontCognexTime.ToString() + "="+ m_sTotalFrontCognexTime);
                inv.settxt(lblCycleTime, (sw.ElapsedMilliseconds / 1000.0f).ToString("0.0"));
                rep = true;
                return rep;
            }
            catch (System.Exception ex) { AddList("Vision stop" + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); return false; }


        }

        //ViDi2.Runtime.IRedTool tool;
        //ViDi2.Runtime.IRedTool tool2;
        private  void SavePicture(string fnamecopy)
        {
            try
            {
                if (chkShowImage.Checked)
                {
                    using (var file = new FileStream(fnamecopy, FileMode.Open, FileAccess.Read, FileShare.Inheritable))
                    {
                        pictureBoxInspect.Image = (Bitmap)Bitmap.FromStream(file).Clone(); // Image.FromFile(EndData.ImagePath);

                        file.Close();
                        file.Dispose();

                    }
                }
            }
            catch(System.Exception ex){ }
        }
        public ISample samp1;
        public ISample samp2;
        public ISample sampFront;
        public RegionFound[] regionFound;
        public RegionFound[] regionFound2;
        public RegionFound[] regionFoundFront;
        public bool PrepairEval(WpfImage image, bool isFract)
        {

            try
            {
                List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();
                

                if (isFract)
                {
                    ViDi2.Runtime.IRedHighDetailParameters hdRedParamsBreake = grunTimeWorkapace.StreamDict[Model1Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                    string filter = hdRedParamsBreake.RegionFilter;
                    //NPNP
                    //Filter only for area. Score will be filtered via threshold
                    filter = "area>= " + txtFractionLowerArea.Text;// + " and score>=" + txtFractionScore.Text.Trim();
                    hdRedParamsBreake.RegionFilter = filter;
                    //NPNP
                    //always pass 1 as higher threshold
                    SetThreshold(hdRedParamsBreake, EndData.FracLowerThreshold, 1 /*EndData.FracUpperThreshold*/);

                    //NPNP
                    var swNeta = Stopwatch.StartNew();
                    //if(SaveOnDisk) 
                        samp1 = grunTimeWorkapace.StreamDict[Model1Name].Process(image);
                    //else samp1 = grunTimeWorkapace.StreamDict[Model1Name].Process(imagecle[0]);
                    swNeta.Stop();
                    var lMilliSeconds = swNeta.ElapsedMilliseconds;



                    Dictionary<string, IMarking> mark = samp1.Markings;
                    Dictionary<string, IMarking>.KeyCollection MarkKey = mark.Keys;
                    //IMarking TryM = mark["red_HDM_20M_5472x3648"];
                    IMarking TryM = mark.Values.First();
                    ViDi2.IView View = TryM.Views[0];// mm.Marking.Views[0];
                    ViDi2.IRedView redview = (ViDi2.IRedView)View;
                    regionFound = new RegionFound[redview.Regions.Count];
                    ViDi2.Runtime.IRedTool tool = (ViDi2.Runtime.IRedTool)grunTimeWorkapace.StreamDict[Model1Name].Tools.First();

                    bool bDebug = true;
                    //if (bDebug)
                    //    File.AppendAllText(sDirpath + @"Performanc.log", $"{DateTime.Now} {TryM.Duration} {lMilliSeconds} {(isFract ? "Fractions" : "Peels")}" + Environment.NewLine);


                }
                else
                {
                    ViDi2.Runtime.IRedHighDetailParameters hdRedParamPeels = grunTimeWorkapace.StreamDict[Model2Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                    //NPNP
                    //Filter only for area. Score will be filtered via threshold
                    string filter1 = hdRedParamPeels.RegionFilter;
                    filter1 = "area>= " + txtPeelLowerArea.Text;// + " and score>=" + txtPeelScore.Text.Trim();
                    hdRedParamPeels.RegionFilter = filter1;
                    //NPNP
                    //always pass 1 as higher threshold
                    SetThreshold(hdRedParamPeels, EndData.PeelLowerThreshold, 1 /*EndData.PeelUpperThreshold*/);
                    //NPNP
                    var swNeta = Stopwatch.StartNew();
                    samp2 = grunTimeWorkapace.StreamDict[Model2Name].Process(image);
                    swNeta.Stop();
                    var lMilliSeconds = swNeta.ElapsedMilliseconds;

                    Dictionary<string, IMarking> mark2 = samp2.Markings;
                    Dictionary<string, IMarking>.KeyCollection MarkKey2 = mark2.Keys;
                    //NPNP
                    //IMarking TryM2 = mark2["red_HDM_20M_5472x3648"];
                    IMarking TryM2 = mark2.Values.First();
                    ViDi2.IView View2 = TryM2.Views[0];
                    ViDi2.IRedView redview2 = (ViDi2.IRedView)View2;
                    regionFound2 = new RegionFound[redview2.Regions.Count];
                    ViDi2.Runtime.IRedTool tool2 = (ViDi2.Runtime.IRedTool)grunTimeWorkapace.StreamDict[Model2Name].Tools.First();

                    bool bDebug = true;
                    //if (bDebug)
                    //    File.AppendAllText(sDirpath + @"Performanc.log", $"{DateTime.Now} {TryM2.Duration} {lMilliSeconds} {(isFract ? "Fractions" : "Peels")}" + Environment.NewLine);

                }
                //bRunPrepareOnLoad=true;
                return true;
            }
            catch (System.Exception ex) 
            { 
                return false; 
            }
        }
        public  WpfImage[] imageCle;
        public WpfImage[] imageCleS;

        //public ViDi2.IImage[] imagecle;
        //public  BitmapSource[] bmpS;
        public int nFrameMax = 0;
        public bool ImageCycle()
        {
            Stopwatch sw = new Stopwatch();
            bool rep = false;

            try
            {
                //return false;
                sw.Restart();
                //this.Invoke((Action)(() => { listBox1.Items.Clear(); }));
                inv.settxt(txtListBox1, "");
                lstStr = "";

                //this.Invoke((Action)(() => { listBox1.Items.Add("----------Start Image Cycle" + " ------------- //" + DateTime.Now.ToString("HH:mm:ss.fff")); }));
                AddList("----------Start Image Cycle" + " ------------- //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                StopCycle = false;
                int framemax = 0;
                
                string nfile = EndData.ImagePath;
                string fcopy = EndData.ImagePath.Substring(0, EndData.ImagePath.LastIndexOf("\\"));
                fcopy = fcopy + "\\COPY";
                string[] filenames = Directory.GetFiles(fcopy);
                foreach (string filename in filenames) File.Delete(filename);
                //imageCle = new ViDi2.UI.WpfImage[numBufferSize];
                for (int i = 0; i < numBufferSize; i++)
                {
                    Thread.Sleep(2);
                    //this.Invoke((Action)(() => { listBox1.Items.Add(" Wait Image " + (i + 1).ToString() + " from Camera 2" + "  //" + DateTime.Now.ToString("HH:mm:ss.fff")); }));
                    AddList(" Wait Image " + (i + 1).ToString() + " from Camera 2" + "  //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                    //while (SnapFile[i] == "" && (SnapFile[i + 1] == "" || i == numBufferSize - 1) && !StopCycle) Thread.Sleep(50);
                    //if (SaveOnDisk)
                    //{
                        while (SnapFile[i] == "" && !StopCycle) Thread.Sleep(50);
                    //}
                    //else
                    //{
                    //    //while (SnapBitmap[i] == null && !StopCycle) Thread.Sleep(50);
                    //    while (Mstream[i] == null && !StopCycle) Thread.Sleep(50);
                    //}
                    if (StopCycle) break;
                    //if (SaveOnDisk)
                    //{
                        string[] ss = SnapFile[i].Split(' ');
                        if (ss.Length > 1)
                        {
                            string[] sss = ss[1].Split('.');
                            framemax = int.Parse(sss[0]);
                        };
                        if (i > framemax - 1) break;
                    //}
                    //else
                    //{
                    //    framemax = nFrameMax;
                    //    if (i > framemax - 1) break;
                    //}
                    //save image
                    sw.Restart();

                    //if (SaveOnDisk)
                    //{

                            string imagefile = SnapFile[i];
                            if (imagefile == "") imagefile = EndData.ImagePath;
                            string fnamecopy = "";
                            fnamecopy = imagefile.Insert(imagefile.IndexOf("snap"), "COPY\\");
                            File.Copy(imagefile, fnamecopy, true);
                            imageCle[i] = new ViDi2.UI.WpfImage(fnamecopy);

                    //}
                    //else
                    //{
                    //    System.IO.Stream ms = new System.IO.MemoryStream();
                        
                    //    SnapBitmap[i].Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        
                    //    imageCle[i] = new ViDi2.UI.WpfImage(Mstream[i]);
                    //    //ms.Close();
                    //    //ms.Dispose();
                    //}
                    //end save
                    //this.Invoke((Action)(() => { listBox1.Items.Add("Fini Image " + (i+1).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); }));
                    AddList("Fini Image " + (i + 1).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                }

                AddList("----------Fini ALL--------------- " + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                sw.Stop();
                //inv.settxt(lblCycleTime, (sw.ElapsedMilliseconds / 1000.0f).ToString("0.0"));
                rep = true;
                return rep;
            }
            catch (System.Exception ex) { return rep; }


        }
        private static System.Drawing.Imaging.ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            var encoders = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders();
            return encoders.FirstOrDefault(t => t.MimeType == mimeType);
        }
        //private void SetJpgQuality()
        //{
        //    // Create an Encoder object based on the GUID for the Quality parameter category.
        //    System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
        //    // Create an EncoderParameters object. An EncoderParameters object has an array of EncoderParameter objects. 
        //    // In this case, there is only one EncoderParameter object in the array.
        //    myEncoderParameters = new System.Drawing.Imaging.EncoderParameters(1);
        //    System.Drawing.Imaging.EncoderParameter myEncoderParameter = new System.Drawing.Imaging.EncoderParameter(myEncoder, 99L); // quality=95% (long !) 
        //    myEncoderParameters.Param[0] = myEncoderParameter;
        //    ici = GetEncoderInfo("image/jpeg");
        //}
        public Bitmap[] bmpCognex;



        public static void CropToFile(string inputPath, string outputPath,
                              int x, int y, int width, int height,
                              string outputFormat, long? jpegQuality = null)
        {
            using (var src = (Bitmap)Image.FromFile(inputPath))
            {
                var rect = ClampRect(x, y, width, height, src.Width, src.Height);

                // Clone avoids resampling and keeps pixels exact.
                using (var cropped = src.Clone(rect, PixelFormat.Format32bppArgb))
                {
                    SaveWithFormat(cropped, outputPath, outputFormat, jpegQuality);
                }
            }
        }

        private static Rectangle ClampRect(int x, int y, int width, int height, int maxW, int maxH)
        {
            x = Clamp(x, 0, Math.Max(0, maxW - 1));
            y = Clamp(y, 0, Math.Max(0, maxH - 1));
            width = Math.Max(1, Math.Min(width, maxW - x));
            height = Math.Max(1, Math.Min(height, maxH - y));
            return new Rectangle(x, y, width, height);
        }

        private static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }

        private static void SaveWithFormat(Bitmap bmp, string path, string format, long? jpegQuality)
        {
            var f = (format ?? "").Trim().ToLowerInvariant();
            ImageCodecInfo codec;
            if (f == "jpg" || f == "jpeg") codec = GetCodec(ImageFormat.Jpeg);
            else if (f == "png") codec = GetCodec(ImageFormat.Png);
            else if (f == "bmp") codec = GetCodec(ImageFormat.Bmp);
            else if (f == "gif") codec = GetCodec(ImageFormat.Gif);
            else if (f == "tif" || f == "tiff") codec = GetCodec(ImageFormat.Tiff);
            else throw new NotSupportedException("Unsupported output format. Use png, jpeg, bmp, gif, or tiff.");

            if (codec.FormatID == ImageFormat.Jpeg.Guid)
            {
                using (var encParams = new EncoderParameters(1))
                {
                    long q = jpegQuality.HasValue ? Math.Max(1, Math.Min(100, jpegQuality.Value)) : 90;
                    encParams.Param[0] = new EncoderParameter(Encoder.Quality, q);
                    bmp.Save(path, codec, encParams);
                }
            }
            else
            {
                bmp.Save(path, codec, null);
            }
        }

        private static ImageCodecInfo GetCodec(ImageFormat fmt)
        {
            var encoders = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < encoders.Length; i++)
                if (encoders[i].FormatID == fmt.Guid) return encoders[i];
            throw new InvalidOperationException("Required image codec not found.");
        }




        public bool ImageFromFileTask()
        {
            Stopwatch sw = new Stopwatch();
            bool rep = false;

            try
            {
                AddList("-----------------Start ImageFromFileTask Cycle" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                for (int i = 0; i < numBufferSize; i++)
                {
                    sw.Restart();
                    Thread.Sleep(2);
                    StopCycle = false;
                    
                    while (SnapFile == null || SnapFile.Length < i + 1 ||
                        SnapFile[i] == null || SnapFile[i] == "" || StreamImage==null || StreamImage[i] == null) 
                    {
                        Thread.Sleep(5);
                        if (StopCycle)
                        {
                            AddList("Stop Add Image "  + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                            return false;
                        }
                    }
                    //save file
                    //using (FileStream file = new FileStream(SnapFile[i], FileMode.Create, FileAccess.Write, FileShare.Inheritable))
                    //{
                    //    byte[] bytes = StreamImage[i].ToArray();
                        
                        
                    //    file.Write(bytes, 0, bytes.Length);
                    //    file.Close();
                    //    file.Dispose();

                    //}

                    //save image
                    sw.Restart();
                    if (imageCle == null || imageCle.Length != numBufferSize) imageCle = new ViDi2.UI.WpfImage[numBufferSize];
                    if (UseMemory)
                    {
                        byte[] bytes = StreamImage[i].ToArray();
                        StreamImage[i].Seek(0, System.IO.SeekOrigin.Begin);
                        if (i >= 0 && i < MstreamBr.Length)
                        {
                            MstreamBr[i] = new MemoryStream(bytes);
                            MstreamPl[i] = new MemoryStream(bytes);

                        }
                    }
                    if (UseMemory)
                    {
                       
                        if (imageCle != null && MstreamBr[i] != null)
                        {
                                                      
                            imageCle[i] = new ViDi2.UI.WpfImage(MstreamBr[i]);
                        }

                    }
                    else
                    {
                        if (imageCle != null)
                        {

                            //NPNP 
                            bool bCrop = false;
                            string sCroppedFileName = "";
                            if (bCrop)
                            {
                                sCroppedFileName = SnapFile[i].Substring(0, SnapFile[i].LastIndexOf(".")) + "_Cropped.jpg";
                                CropToFile(SnapFile[i], sCroppedFileName, 0, 0, 5000, 1516, "jpeg", /*85*/100);
                            }

                            //copy file
                            //string imagefile = SnapFile[i];
                            string imagefile = (bCrop ? sCroppedFileName: SnapFile[i]) ;
                            if (imagefile == "") imagefile = EndData.ImagePath;
                            //string fnamecopy = "";
                            imageCle[i] = new ViDi2.UI.WpfImage(imagefile);
                            



                        }
                    }

                    if (imageCleS == null || imageCleS.Length != numBufferSize) imageCleS = new ViDi2.UI.WpfImage[numBufferSize];
                    if (UseMemory)
                    {
                        if (imageCleS != null && MstreamPl[i] != null)
                        {
                            imageCleS[i] = new ViDi2.UI.WpfImage(MstreamPl[i]);
                        }
                    }
                    else
                    {
                        if (imageCleS != null)
                        {
                            //copy file
                            string imagefile = SnapFile[i];
                            if (imagefile == "") imagefile = EndData.ImagePath;
                            //string fnamecopy = "";
                            imageCleS[i] = new ViDi2.UI.WpfImage(imagefile);
                        }
                    }

                    AddList("Fini Add Image " + (i + 1).ToString() + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));

                    sw.Stop();
                }

                AddList("Fini Add Images " + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                return true;
            }
            catch (System.Exception ex) { AddList("Error Add Image "  + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); ; return rep; }


        }
        public async Task<bool> ImageFromFileTask1(int num)
        {
            //Stopwatch sw = new Stopwatch();
            bool rep = false;

            try
            {
                AddList("Start ImageFromFileTask1 "+(num+1).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
               
                    //sw.Restart();
                    
                    //save file
                    using (FileStream file = new FileStream(SnapFile[num], FileMode.Create, FileAccess.Write, FileShare.Inheritable))
                    {
                        byte[] bytes = StreamImage[num].ToArray();


                        file.Write(bytes, 0, bytes.Length);
                        file.Close();
                        file.Dispose();

                    }

                    //save image
                    //sw.Restart();
                    if (imageCle == null || imageCle.Length != numBufferSize) imageCle = new ViDi2.UI.WpfImage[numBufferSize];
                    if (UseMemory)
                    {
                        byte[] bytes = StreamImage[num].ToArray();
                        StreamImage[num].Seek(0, System.IO.SeekOrigin.Begin);
                        if (num >= 0 && num < MstreamBr.Length)
                        {
                            MstreamBr[num] = new MemoryStream(bytes);
                            MstreamPl[num] = new MemoryStream(bytes);

                        }
                    }
                    if (UseMemory)
                    {

                        if (imageCle != null && MstreamBr[num] != null)
                        {

                            imageCle[num] = new ViDi2.UI.WpfImage(MstreamBr[num]);
                        }

                    }
                    else
                    {
                        if (imageCle != null)
                        {
                            //copy file
                            string imagefile = SnapFile[num];
                            if (imagefile == "") imagefile = EndData.ImagePath;
                            //string fnamecopy = "";
                            imageCle[num] = new ViDi2.UI.WpfImage(imagefile);




                        }
                    }

                    if (imageCleS == null || imageCleS.Length != numBufferSize) imageCleS = new ViDi2.UI.WpfImage[numBufferSize];
                    if (UseMemory)
                    {
                        if (imageCleS != null && MstreamPl[num] != null)
                        {
                            imageCleS[num] = new ViDi2.UI.WpfImage(MstreamPl[num]);
                        }
                    }
                    else
                    {
                        if (imageCleS != null)
                        {
                            //copy file
                            string imagefile = SnapFile[num];
                            if (imagefile == "") imagefile = EndData.ImagePath;
                            //string fnamecopy = "";
                            imageCleS[num] = new ViDi2.UI.WpfImage(imagefile);
                        }
                    }

                    AddList("Fini Add Image " + (num + 1).ToString() + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));

                    //sw.Stop();
                

                
                return true;
            }
            catch (System.Exception ex) { AddList("Error Add Image " + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); ; return rep; }


        }
        //public bool ImageFromBmpsTask()
        //{
        //    Stopwatch sw = new Stopwatch();
        //    bool rep = false;

        //    try
        //    {
        //        AddList("-----------------Start ImageFromFileTask Cycle" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
        //        if (bmpS == null || bmpS.Length != numBufferSize) bmpS = new BitmapSource[numBufferSize];
        //        for (int i = 0; i < numBufferSize; i++)
        //        {
        //            sw.Restart();
        //            Thread.Sleep(10);
        //            StopCycle = false;

        //            while (bmpS == null || bmpS.Length < i + 1 ||
        //                bmpS[i] == null)
        //            {
        //                Thread.Sleep(20);
        //                if (StopCycle)
        //                {
        //                    AddList("Stop Add Image " + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
        //                    return false;
        //                }
        //            }


        //            //save image
        //            sw.Restart();
        //            if (imageCleS == null || imageCleS.Length != numBufferSize) imageCleS = new ViDi2.UI.WpfImage[numBufferSize];

        //            if (imageCleS != null)
        //            {
        //                if (bmpS == null || bmpS.Length != numBufferSize) bmpS = new BitmapSource[numBufferSize];
        //                //imageCleS[i] = new ViDi2.UI.WpfImage(bmpS[i].Clone());

        //            }

        //            AddList("Fini Add Image " + (i + 1).ToString() + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));

        //            sw.Stop();
        //        }

        //        AddList("Fini Add Images " + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
        //        return true;
        //    }
        //    catch (System.Exception ex) { AddList("Error Add Image " + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); ; return rep; }


        //}
        //public bool ImageFromBmp(int num, Bitmap btm)
        //{
        //    //Stopwatch sw = new Stopwatch();
        //    bool rep = false;

        //    try
        //    {

        //        BitmapSource bitSrc = null;

        //        var hBitmap = btm.GetHbitmap();


        //            bitSrc = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
        //                hBitmap,
        //                IntPtr.Zero,
        //                Int32Rect.Empty,
        //                BitmapSizeOptions.FromEmptyOptions());
        //            //frmMain.bmpS[opt - 1] = bitSrc.Clone();
        //            this.Invoke(new Action(() => bmpS[num] = bitSrc.Clone()));

        //            bitSrc = null;
        //            if (imageCleS == null || imageCleS.Length != numBufferSize) imageCleS = new ViDi2.UI.WpfImage[numBufferSize];
        //            if (bmpS == null || bmpS.Length != numBufferSize) bmpS = new BitmapSource[numBufferSize];
        //            imageCleS[num] = new ViDi2.UI.WpfImage(bmpS[num].Clone());
        //            //frmMainInspect.ImageFromBmp(opt - 1, bitSrc);



        //        AddList("Fini Add Image " + (num + 1).ToString() + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
        //        return true;
        //    }
        //    catch (System.Exception ex) { AddList("Error Add Image " + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); ; return rep; }


        //}
        public bool ImageFromFile(int id)
        {
            Stopwatch sw = new Stopwatch();
            bool rep = false;

            try
            {
                //return false;
                sw.Restart();
                //this.Invoke((Action)(() => { listBox1.Items.Clear(); }));
                //this.Invoke((Action)(() => { listBox1.Items.Add("----------Start Image Cycle" + " ------------- //" + DateTime.Now.ToString("HH:mm:ss.fff")); }));
                StopCycle = false;
                int framemax = 0;

                   
                    while (SnapFile[id] == "" && !StopCycle) Thread.Sleep(20);
                    
                    if (StopCycle)
                    {
                        AddList("Stop Add Image " + (id + 1).ToString() + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                        return true;
                    }
                   
                    //save image
                    sw.Restart();
                if (imageCle == null || imageCle.Length != numBufferSize) imageCle = new ViDi2.UI.WpfImage[numBufferSize];
                //if (imagecle == null || imagecle.Length != numBufferSize) imagecle = new ViDi2.UI.WpfImage[numBufferSize];
                if ( imageCle != null)
                {
                        //copy file
                        string imagefile = SnapFile[id];
                        if (imagefile == "") imagefile = EndData.ImagePath;
                        string fnamecopy = "";
                        imageCle[id] = new ViDi2.UI.WpfImage(imagefile);
                 
                }
                
                AddList("Fini Add Image " + (id + 1).ToString() + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
           
                rep = true;
                return rep;
            }
            catch (System.Exception ex) { AddList("Error Add Image " + (id + 1).ToString() + " to Cognex" + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); ; return rep; }


        }
        public bool FileToImage(int n)
        {
            Stopwatch sw = new Stopwatch();
            bool rep = false;

            try
            {
                //return false;
                sw.Restart();
                //this.Invoke((Action)(() => { listBox1.Items.Clear(); }));
                inv.settxt(txtListBox1, "");
                lstStr = "";
                AddList("----------Start Image Cycle" + " ------------- //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                StopCycle = false;
                int framemax = 0;

                string nfile = EndData.ImagePath;
                string fcopy = EndData.ImagePath.Substring(0, EndData.ImagePath.LastIndexOf("\\"));
                fcopy = fcopy + "\\COPY";
                string[] filenames = Directory.GetFiles(fcopy);
                foreach (string filename in filenames) File.Delete(filename);
                //imageCle = new ViDi2.UI.WpfImage[numBufferSize];
                //for (int i = 0; i < numBufferSize; i++)
                //{
                    Thread.Sleep(2);
                    AddList(" Wait Image " + (n + 1).ToString() + " from Camera 2" + "  //" + DateTime.Now.ToString("HH:mm:ss.fff")); 
                //while (SnapFile[i] == "" && (SnapFile[i + 1] == "" || i == numBufferSize - 1) && !StopCycle) Thread.Sleep(50);
                if (SnapFile[n] == "") return false; 
                    //if (StopCycle) break;
                    string[] ss = SnapFile[n].Split(' ');
                    if (ss.Length > 1)
                    {
                        string[] sss = ss[1].Split('.');
                        framemax = int.Parse(sss[0]);
                    };
                    
                    //save image
                    sw.Restart();

                    //if (SaveOnDisk)
                    //{
                        //copy file
                        string imagefile = SnapFile[n];
                        if (imagefile == "") imagefile = EndData.ImagePath;
                        string fnamecopy = "";
                        fnamecopy = imagefile.Insert(imagefile.IndexOf("snap"), "COPY\\");
                        File.Copy(imagefile, fnamecopy, true);

                        imageCle[n] = new ViDi2.UI.WpfImage(fnamecopy);
                        //var task = Task.Run(() => SavePicture(fnamecopy));
                        //await task;

                    //}
                    //else
                    //{
                    //    //if (chkShowImage.Checked) pictureBoxInspect.Image = (Bitmap)SnapImage[num].Clone();
                    //}
                    //end save
                    //this.Invoke((Action)(() => { listBox1.Items.Add("Fini Image " + (n + 1).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); }));
                    AddList("Fini Image " + (n + 1).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                //}

                AddList("----------Fini ALL--------------- " + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                sw.Stop();
                //inv.settxt(lblCycleTime, (sw.ElapsedMilliseconds / 1000.0f).ToString("0.0"));
                rep = true;
                return rep;
            }
            catch (System.Exception ex) { return rep; }


        }
        public Rectangle PartRect = new Rectangle(0,0,0,0);

        public System.Collections.ObjectModel.ReadOnlyCollection<IRegion> RedViewRegions;

        public bool Infer(WpfImage image, bool isFractions)
        {

            try
            {
                List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();
                ISample sample;
                long lMilliSeconds = 0;
                if (isFractions)
                {

                    ViDi2.Runtime.IRedHighDetailParameters hdRedParamsFractions = grunTimeWorkapace.StreamDict[Model1Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                    SetThreshold(hdRedParamsFractions, EndData.FracLowerThreshold, EndData.FracUpperThreshold);
                    var swNeta = Stopwatch.StartNew();
                    sample = grunTimeWorkapace.StreamDict[Model1Name].Process(image);
                    swNeta.Stop();
                    lMilliSeconds = swNeta.ElapsedMilliseconds;

                    //sample = grunTimeWorkapace.StreamDict[Model1Name].CreateSample(image);
                }
                else
                {

                    ViDi2.Runtime.IRedHighDetailParameters hdRedParamPeels = grunTimeWorkapace.StreamDict[Model2Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                    SetThreshold(hdRedParamPeels, EndData.PeelLowerThreshold, EndData.PeelUpperThreshold);
                    var swNeta = Stopwatch.StartNew();
                    sample = grunTimeWorkapace.StreamDict[Model2Name].Process(image);
                    swNeta.Stop();
                    lMilliSeconds = swNeta.ElapsedMilliseconds;

                    //sample = grunTimeWorkapace.StreamDict[Model2Name].CreateSample(image);
                }
                Dictionary<string, IMarking> mark = sample.Markings;
                Dictionary<string, IMarking>.KeyCollection MarkKey = mark.Keys;
                IMarking TryM = mark.Values.First();
                bool bDebug = true;
                if (bDebug)
                    File.AppendAllText(sDirpath + @"Performanc.log", $"{DateTime.Now} {TryM.Duration} {lMilliSeconds} {(isFractions ? "Fractions" : "Peels")}" + Environment.NewLine);
                ViDi2.IView View = TryM.Views[0];// mm.Marking.Views[0];
                ViDi2.IRedView redview = (ViDi2.IRedView)View;
                RedViewRegions = (System.Collections.ObjectModel.ReadOnlyCollection<IRegion>)redview.Regions;

                return true;
            }
            catch (System.Exception ex)
            {
                return false;
            }
        }


        public void ReportInferenceResults(bool bISFractions, int iCycleNum,int iROI, ViDi2.IRedView redview)
        {
            string sReportInferenceResults = "";
            int iRegionNumber = 0;
            //double dY0CorrectedForGeographicROI = 0.0;

            string sReportInferenceResultsPerformance = "ReportInferenceResults";
            var swShowRegions1 = Stopwatch.StartNew();

            // sReportInferenceResultsPerformance += $"ReportInferenceResult Mark 1 {swShowRegions1.ElapsedTicks} \n";
            bool bDebug = true;
            if(bDebug)
                File.AppendAllText(sDirpath + @"Defects.log", DateTime.Now.ToString() + Environment.NewLine);

            // sReportInferenceResultsPerformance += $"ReportInferenceResult Mark 2 {swShowRegions1.ElapsedTicks} \n";

            foreach (ViDi2.IRegion item in redview.Regions)
            {
                // sReportInferenceResultsPerformance += $"ReportInferenceResult Mark 3 {swShowRegions1.ElapsedTicks} \n";
                bool bReportDefect = false;

                //if the image is cropped based on geographic ROI, report all defects found, as they are already cropped
                if (_eSnapShotStrategy== 2)
                {
                    bReportDefect = true;
                }
                else
                {
                    if (bISFractions)
                    {
                        //if (item.Area >= endmills[CmbCatNum.SelectedIndex].FracLowerArea &&
                        //    item.Score >= endmills[CmbCatNum.SelectedIndex].FracLowerThresholdFront)
                        {
                            //if we are checking results for ROI (1) and we asked for a fractions model for this ROI (1) && asked to use this ROI (1) && 
                            //it's contained in ROI (1)
                            if (iROI == 1 && chkFractions1.Checked && chkROI1.Checked && IsRegionInDefectRoi(item, iROI))
                                bReportDefect = true;
                            //if we are checking results for ROI (2) and ...
                            if (iROI == 2 && chkFractions2.Checked && chkROI2.Checked && IsRegionInDefectRoi(item, iROI))
                                bReportDefect = true;
                            //if we are checking results for ROI (3) and ...
                            if (iROI == 3 && chkFractions3.Checked && chkROI3.Checked && IsRegionInDefectRoi(item, iROI))
                                bReportDefect = true;
                        }
                    }
                    else
                    {
                        //if (item.Area >= endmills[CmbCatNum.SelectedIndex].PeelLowerArea &&
                        //    item.Score >= endmills[CmbCatNum.SelectedIndex].PeelLowerThresholdFront)
                        {
                            //if we are checking results for ROI (1) and we asked for a peels model for this ROI (1) && asked to use this ROI (1) && 
                            //it's contained in ROI (1)
                            if (iROI == 1 && chkPeels1.Checked && chkROI1.Checked && IsRegionInDefectRoi(item, iROI))
                                bReportDefect = true;
                            //if we are checking results for ROI (2) and ...
                            if (iROI == 2 && chkPeels2.Checked && chkROI2.Checked && IsRegionInDefectRoi(item, iROI))
                                bReportDefect = true;
                            //if we are checking results for ROI (3) and ...
                            if (iROI == 3 && chkPeels3.Checked && chkROI3.Checked && IsRegionInDefectRoi(item, iROI))
                                bReportDefect = true;
                        }
                    }
                }

                // sReportInferenceResultsPerformance += $"ReportInferenceResult Mark 4 {swShowRegions1.ElapsedTicks} \n";
                //dY0CorrectedForGeographicROI =
                //    item.Center.Y;// - (bCurrentSnapShotAOIGeographicROIBasedImages ? CalcHighestCroppingTop(100) : 0);

                //Write the results anyway for the log while debugging
                sReportInferenceResults = (iCycleNum + 1).ToString("00") + " " 
                    //+ iRegionNumber.ToString() 
                    + " ROI:" + iROI.ToString() + 
                    " " + (bISFractions ? "Break" : "Peels") + " Score=" + item.Score.ToString() + " " +
                    "Area=" + item.Area.ToString() + " " +
                    "Perimeter=" + item.Perimeter.ToString() + " " +
                    "Ourer=" + item.Outer.Count.ToString() + " " +
                    "X0=" + item.Center.X.ToString() + " " +
                    " Y0=" + item.Center.Y.ToString() + " " +
                    "H=" + item.Height.ToString() + " " +
                    "W=" + item.Width.ToString();
                iRegionNumber++;

                //NPNP 
                if (bReportDefect)
                {
                    bDefectFoundInTopInspection = true;
                    if (!sInferenceResults.Contains(sReportInferenceResults))
                    {
                        // sReportInferenceResultsPerformance += $"ReportInferenceResult Mark 5 {swShowRegions1.ElapsedTicks} \n";
                        sInferenceResults.Add(sReportInferenceResults);
                        // sReportInferenceResultsPerformance += $"ReportInferenceResult Mark 5.5 {swShowRegions1.ElapsedTicks} \n";
                        //NPNP Performance
                        //listBox1.Items.Add(sReportInferenceResults);

                        // sReportInferenceResultsPerformance += $"ReportInferenceResult Mark 6 {swShowRegions1.ElapsedTicks} \n";
                        AddList(sReportInferenceResults);
                        //fractions
                        if (bISFractions)
                        {
                            if (iROI == 1)
                            {
                                RegionFound1BrSave[RegionFound1BrSave.Length - 1] = sReportInferenceResults;
                                Array.Resize<String>(ref RegionFound1BrSave, RegionFound1BrSave.Length + 1);
                            }
                            else if (iROI == 2)
                            {
                                RegionFound2BrSave[RegionFound2BrSave.Length - 1] = sReportInferenceResults;
                                Array.Resize<String>(ref RegionFound2BrSave, RegionFound2BrSave.Length + 1);
                            }
                            else if (iROI == 3)
                            {
                                RegionFound3BrSave[RegionFound3BrSave.Length - 1] = sReportInferenceResults;
                                Array.Resize<String>(ref RegionFound3BrSave, RegionFound3BrSave.Length + 1);
                            }
                        }
                        //peels
                        else
                        {
                            if (iROI == 1)
                            {
                                RegionFound1PlSave[RegionFound1PlSave.Length - 1] = sReportInferenceResults;
                                Array.Resize<String>(ref RegionFound1PlSave, RegionFound1PlSave.Length + 1);
                            }
                            else if (iROI == 2)
                            {
                                RegionFound2PlSave[RegionFound2PlSave.Length - 1] = sReportInferenceResults;
                                Array.Resize<String>(ref RegionFound2PlSave, RegionFound2PlSave.Length + 1);
                            }
                            else if (iROI == 3)
                            {
                                RegionFound3PlSave[RegionFound3PlSave.Length - 1] = sReportInferenceResults;
                                Array.Resize<String>(ref RegionFound3PlSave, RegionFound3PlSave.Length + 1);
                            }

                        }
                        // sReportInferenceResultsPerformance += $"ReportInferenceResult Mark 7 {swShowRegions1.ElapsedTicks} \n";
                    }
                }
                //for debugging purposes write all the results to log
                if (bDebug)
                {
                    File.AppendAllText(sDirpath + @"Defects.log", sReportInferenceResults + (bReportDefect ? " Reported " : " NOT Reported ") + Environment.NewLine);
                }
            }
            //File.AppendAllText(sDirpath + @"ReportInferenceResult.log", sReportInferenceResultsPerformance + Environment.NewLine);

        }

        private void Infer4(WpfImage image, bool isFractions)
        {
            {
                Stopwatch sw = new Stopwatch();
                sw.Restart();

                //string ImagePath = Path.GetDirectoryName(txtImgPathLoad.Text.Trim());//txtImgPathLoad.Text.Trim();

                //string ImageName = Path.GetFileName(txtImgPathLoad.Text.Trim());
                //string fullPath = ImagePath + @"\" + ImageName;
                //if (!File.Exists(fullPath)) { System.Windows.Forms.MessageBox.Show("Image or Path Don't Exist!,\n\r" + fullPath); goto exitProcedure; }

                //peels
                string modelFileName = "Proj_021_201223_104500_21122023_104445";
                string modelName = modelFileName.Trim() + ".vrws";  // this.lstModels.SelectedItem.ToString();
                //var image = new ViDi2.UI.WpfImage(fullPath);   // imagePath);


                Rectangle ImageDimensions = new Rectangle(0, 0, image.Width, image.Height);
                //ApplyROISimple(ImageDimensions);

                //--------------------------set thresholds--------------------------------
                IRedHighDetailParameters hdRedParams;// = Stream.Tools.First().ParametersBase as IRedHighDetailParameters;

                //
                //ViDi2.Runtime.IRedTool hdRedTool = (ViDi2.Runtime.IRedTool)Stream.Tools.First();
                ViDi2.Runtime.IRedTool hdRedTool;
                if (isFractions)
                {
                    hdRedTool = (ViDi2.Runtime.IRedTool)grunTimeWorkapace.StreamDict[Model1Name].Tools.First();
                }
                else
                {
                    hdRedTool = (ViDi2.Runtime.IRedTool)grunTimeWorkapace.StreamDict[Model2Name].Tools.First();
                }
                hdRedParams = hdRedTool.ParametersBase as IRedHighDetailParameters;

                ViDi2.IManualRegionOfInterest redROI01 = (ViDi2.IManualRegionOfInterest)hdRedTool.RegionOfInterest;
                redROI01.Parameters.Units = ViDi2.UnitsMode.Pixel;

                redROI01.Parameters.Offset = new ViDi2.Point(0, 0);
                redROI01.Parameters.Size = new ViDi2.Size(ImageDimensions.Width, ImageDimensions.Height);


                //ViDi2.Interval interval = new ViDi2.Interval(0.2, 0.9);
                hdRedParams.Threshold = new ViDi2.Interval(0.2, 0.9)/*interval*/;

                //output filter                
                IRedHighDetailParameters outputrFilterRed01 = hdRedTool.ParametersBase as IRedHighDetailParameters;  // ViDi2.IRedRegionOfInterestParameters;
                string filter = outputrFilterRed01.RegionFilter;

                filter = "area>= 64 and score> 0.15";
                outputrFilterRed01.RegionFilter = filter;

                //-----------------------------------process image----------------------------------
                /*SampleViewerViewModel.Sample*/
                ISample sample = stream.Process(image);

                Dictionary<string, IMarking> mark = sample.Markings;
                Dictionary<string, IMarking>.KeyCollection MarkKey = mark.Keys;

                IMarking marking = mark.Values.First();

                //ViDi2.IMarking marking = sample.Markings[0];// SampleViewerViewModel.Marking;

                System.Collections.ObjectModel.ReadOnlyCollection<ViDi2.IView> views = marking.Views;

                double duration = marking.Duration; //process time
                double durationPostProcess = marking.DurationPostProcess;
                double durationProcessOnly = marking.DurationProcessOnly;
                ViDi2.IImageInfo imageinfo = marking.ImageInfo;
                IEnumerable<ViDi2.IImageInfo> imageinfos = marking.ImageInfos;

                IEnumerable<ViDi2.ISetInfo> setinfo = marking.Sets;

                duration = sw.ElapsedMilliseconds / 1000.0;
                lblDuration.Text = duration.ToString("0.0000");
                sw.Stop();
            }

        exitProcedure:;
        }


        //private void ApplyROISimple(Rectangle ImageDimensions)
        //{
        //    ViDi2.IManualRegionOfInterest redROI01 = (ViDi2.IManualRegionOfInterest)Stream.Tools.First().RegionOfInterest;
        //    redROI01.Parameters.Units = ViDi2.UnitsMode.Pixel;

        //    redROI01.Parameters.Offset = new ViDi2.Point(0, 0);
        //    redROI01.Parameters.Size = new ViDi2.Size(ImageDimensions.Width, ImageDimensions.Height);


        //}

        public async Task<bool> startEval(string imagefile, int num)
        {
            
            if (imagefile == "") imagefile = EndData.ImagePath;
            string fnamecopy = "";



            Stopwatch sw = new Stopwatch();
            try
            {
                //listBox1.Items.Clear();
                sw.Restart();
                string iniFileName = CmbCatNumText + ".ini";
                string iniFilePath = sDirpath + @"Data\DataBase\" + iniFileName;
                List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();
                ViDi2.Runtime.IRedHighDetailParameters hdRedParamPeels = grunTimeWorkapace.StreamDict[Model2Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                ViDi2.Runtime.IRedHighDetailParameters hdRedParamsBreake = grunTimeWorkapace.StreamDict[Model1Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                System.Windows.Rect ROIrect = new System.Windows.Rect();
                
                WpfImage image = null;
                //NPNP
                //imageCle = new WpfImage[8];
                //imageCle[0] = new WpfImage("C:\\Project\\4.2.2025\\InspectSolution\\BeckhoffBaslerTasks\\BeckhoffBasler\\bin\\Debug\\Images\\snap.jpg");
                if (imageCle[num] == null)
                {
                    return false;
                }
                image = imageCle[num];

                //NPNP
                if(chkFractions1.Checked || chkFractions2.Checked || chkFractions3.Checked)
                    Infer4(image,true);
                if (chkPeels1.Checked || chkPeels2.Checked || chkPeels3.Checked)
                    Infer4(image, false);
                return true;




                Rectangle ImageDimensions = new Rectangle(0, 0, image.Width, image.Height);
                string cn = "";
                string cn2 = "";

                int index2 = 0;
                int Iindex = 0;
                resArr.resIndex = 0;
                resArr.resInfo.ShowRes = new string[20];
                int index = 0;
                bool PrepairEvalBr = false;
                bool PrepairEvalPl = false;
                //apply roi



                //add if setup-need to chec frac or peels
                // RIO 1
                if ((EndData.IsFractions1 == "true" && EndData.Roi1 == "true") || (EndData.IsFractions2 == "true" && EndData.Roi2 == "true") ||
                    (EndData.IsFractions3 == "true" && EndData.Roi3 == "true"))
                {
                    var task1 = Task.Run(() => PrepairEval(image, true));
                    await task1;
                    if (!task1.Result)
                    {
                        StopCycle = true;
                        System.Windows.Forms.MessageBox.Show("Error Breaks Evaluation1! ", "ERROR", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
                    }
                    PrepairEvalBr = true;
                }


                    if (EndData.Roi1 == "true" || chkCutImage.Checked)
                    {
                    //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    if (chkAutoROI.Checked && PartRect.Width != 0 && PartRect.Height != 0)
                    {

                        
                        ROIrect.X = PartRect.X + 5;
                        ROIrect.Y = PartRect.Y - 15;
                        ROIrect.Width = PartRect.Width - 5;
                        //ROIrect.Height = Math.Abs((int)((PartRect.Height / 2.0f) * 3.0f * Math.Sin((3.14f / 180.0f) * (90.0f / (Single)numBufferSize))));
                        ROIrect.Height = 30 + Math.Abs((int)((PartRect.Height / 2.0f) - (PartRect.Height / 2.0f) * Math.Cos((3.14f / 180.0f) * (360.0f / (Single)numBufferSize))));

                    }
                    else
                    {
                        ROIrect.X = EndData.roiPosX1;
                        ROIrect.Y = EndData.roiPosY1;
                        ROIrect.Width = EndData.roiWidth1;
                        ROIrect.Height = EndData.roiHeight1;
                        RegIndex = 0;
                        RegIndex2 = 0;
                    }
                        if (EndData.IsFractions1 == "true")
                        {
                            AddList("BREAK1"); 
                            if(chkAutoROI.Checked)
                            {

                            }

                            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1, 1, 
                                (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model1Name].Tools.First().RegionOfInterest);
                        //NPNP
                        var swNeta = Stopwatch.StartNew();
                        samp1.Process();
                        swNeta.Stop();
                        var lMilliSeconds = swNeta.ElapsedMilliseconds;

                        getRagion(samp1, Model1Name, index, Iindex, regionFound, num, 1);

                            //add founds to list.
                        }
                        
                        //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    }

                if ((EndData.IsPeels1 == "true" && EndData.Roi1 == "true") || (EndData.IsPeels2 == "true" && EndData.Roi2 == "true") ||
                    (EndData.IsPeels3 == "true" && EndData.Roi3 == "true")) {

                    var task1 = Task.Run(() => PrepairEval(image, false));
                    await task1;
                    if (!task1.Result)
                    {
                        StopCycle = true;
                        System.Windows.Forms.MessageBox.Show("Error Peels Evaluation1! ", "ERROR", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
                    }
                    PrepairEvalPl = true;
                }

                    if (EndData.Roi1 == "true" || chkCutImage.Checked)
                    {
                        //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                        ROIrect.X = EndData.roiPosX1;
                        ROIrect.Y = EndData.roiPosY1;
                        ROIrect.Width = EndData.roiWidth1;
                        ROIrect.Height = EndData.roiHeight1;
                        RegIndex = 0;
                        RegIndex2 = 0;
                        if (EndData.IsPeels1 == "true")
                        {
                            AddList("PEELS1_" + num.ToString()); 
                            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2, 1,
                                (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model2Name].Tools.First().RegionOfInterest);
                            samp2.Process();

                            getRagion(samp2, Model2Name, index2, Iindex, regionFound2, num, 1);
                            //add founds to list.
                        }
                        //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    }
                

                // RIO 2
                
                    if (EndData.Roi2 == "true" || chkCutImage.Checked)
                    {
                    //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                        //Thread.Sleep(200);
                        ROIrect.X = EndData.roiPosX2;
                        ROIrect.Y = EndData.roiPosY2;
                        ROIrect.Width = EndData.roiWidth2;
                        ROIrect.Height = EndData.roiHeight2;
                        RegIndex = 0;
                        RegIndex2 = 0;
                        if (EndData.IsFractions2 == "true")
                        {
                            AddList("BREAK2"); 

                            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1, 2,
                                (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model1Name].Tools.First().RegionOfInterest);
                            samp1.Process();

                            getRagion(samp1, Model1Name, index, Iindex, regionFound, num, 2);

                            //add founds to list.
                        }

                        //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    }
                
                

                    if (EndData.Roi2 == "true" || chkCutImage.Checked)
                    {
                        //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                        ROIrect.X = EndData.roiPosX2;
                        ROIrect.Y = EndData.roiPosY2;
                        ROIrect.Width = EndData.roiWidth2;
                        ROIrect.Height = EndData.roiHeight2;
                        RegIndex = 0;
                        RegIndex2 = 0;
                        if (EndData.IsPeels2 == "true")
                        {
                            AddList("PEELS2");
                            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2, 2,
                                (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model2Name].Tools.First().RegionOfInterest);
                            samp2.Process();

                            getRagion(samp2, Model2Name, index2, Iindex, regionFound2, num, 2);
                            //add founds to list.
                        }
                        //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    }
                

                // RIO 3
                
                    if (EndData.Roi3 == "true" || chkCutImage.Checked)
                    {
                    //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                        //Thread.Sleep(200);
                        ROIrect.X = EndData.roiPosX3;
                        ROIrect.Y = EndData.roiPosY3;
                        ROIrect.Width = EndData.roiWidth3;
                        ROIrect.Height = EndData.roiHeight3;
                        RegIndex = 0;
                        RegIndex2 = 0;
                        if (EndData.IsFractions3 == "true")
                        {
                            AddList("BREAK3"); 

                            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1, 3,
                                (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model1Name].Tools.First().RegionOfInterest);
                            samp1.Process();

                            getRagion(samp1, Model1Name, index, Iindex, regionFound, num, 3);

                            //add founds to list.
                        }

                        //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    }
                
                

                    if (EndData.Roi3 == "true" || chkCutImage.Checked)
                    {
                        //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                        ROIrect.X = EndData.roiPosX3;
                        ROIrect.Y = EndData.roiPosY3;
                        ROIrect.Width = EndData.roiWidth3;
                        ROIrect.Height = EndData.roiHeight3;
                        RegIndex = 0;
                        RegIndex2 = 0;
                        if (EndData.IsPeels3 == "true")
                        {
                            AddList("PEELS3"); 
                            ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2, 3,
                                (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model2Name].Tools.First().RegionOfInterest);
                            samp2.Process();

                            getRagion(samp2, Model2Name, index2, Iindex, regionFound2, num, 3);
                            //add founds to list.
                        }
                        //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    }
                



                inv.set(btnStartEval, "Enabled", true);
                sw.Stop();
                inv.settxt(lblDuration, (sw.ElapsedMilliseconds / 1000.0).ToString("0.000"));
                return true;
            }
            catch (System.Exception ex) {  return false; }


        }
        private async Task<bool> PrepairEvalAll(string imagefile, int num)
        {

            if (imagefile == "") imagefile = EndData.ImagePath;
           
            try
            {
                
                string iniFileName = CmbCatNumText + ".ini";
                string iniFilePath = sDirpath + @"Data\DataBase\" + iniFileName;
                List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();
                ViDi2.Runtime.IRedHighDetailParameters hdRedParamPeels = grunTimeWorkapace.StreamDict[Model2Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                ViDi2.Runtime.IRedHighDetailParameters hdRedParamsBreake = grunTimeWorkapace.StreamDict[Model1Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                
                WpfImage image = null;
                if (imageCle[num] == null)
                {
                    return false;
                }
                image = imageCle[num];


                Rectangle ImageDimensions = new Rectangle(0, 0, image.Width, image.Height);
                                
                resArr.resIndex = 0;
                resArr.resInfo.ShowRes = new string[20];
                
                
                if ((EndData.IsFractions1 == "true" && EndData.Roi1 == "true") || (EndData.IsFractions2 == "true" && EndData.Roi2 == "true") ||
                    (EndData.IsFractions3 == "true" && EndData.Roi3 == "true"))
                {
                    var task1 = Task.Run(() => PrepairEval(image, true));
                    var task2 = Task.Run(() => PrepairEval(image, false));
                    await task1;
                    await task2;
                    if (!task1.Result || !task2.Result)
                    {
                        StopCycle = true;
                        System.Windows.Forms.MessageBox.Show("Error PrepairEvalAll! ", "ERROR", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
                    }
                    //PrepairEvalBr = true;
                }

                return true;
            }
            catch (System.Exception ex) { return false; }


        }
        Single timeAll = 0;
        private bool startEvalFractions(string imagefile, int num=0, IImage iimage = null)
        {
            Stopwatch swNeta = new Stopwatch();
            try
            {
                string sPerformance = "";
                var swPerformance = Stopwatch.StartNew();
               
                int iProfilingMark = 0;

                //ViDi2.Runtime.IStream stream = StreamAll;
                //ViDi2.Runtime.IWorkspace workspace = Workspace;
                //ViDi2.Runtime.IControl control = Control;
                //IList<ViDi2.Runtime.IWorkspace> ws = Workspaces;

                //sPerformance += $"StartEvalFractions Profiling: Mark 2 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                //if (bmpS[num] == null && bmpS[num] == null)
                //{
                //    return false;
                //}
                WpfImage image = null;// new WpfImage(bmpS[num]);


                if (imageCle[num] == null )//&& imagecle[num] == null)
                {
                    return false;
                }

                //NPNP
                byte[] bytes = StreamImage[num].ToArray();
                StreamImage[num].Seek(0, System.IO.SeekOrigin.Begin);
                MemoryStream MstreamBr = new MemoryStream(bytes);
                image = new WpfImage(MstreamBr);
                //image = imageCle[num];


                //if (InvokeRequired)
                //{
                //    this.Invoke(new Action(() => image = imageCle[num]));
                //}


                Rectangle ImageDimensions = new Rectangle(0, 0, image.Width, image.Height);
                int Iindex = 0;
                resArr.resIndex = 0;
                resArr.resInfo.ShowRes = new string[20];
                int index = 0;

                //follow to see if one .Process() method was already per brain per inspection cycle
                bool bWasProcessRunForFractions = false;


                ViDi2.Runtime.IRedHighDetailParameters hdRedParamsBreake = grunTimeWorkapace.StreamDict[Model1Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                //Filter only for area. Score will be filtered via threshold
                string filter = "area>= " + txtFractionLowerArea.Text;// + " and score>=" + txtFractionScore.Text.Trim();
                hdRedParamsBreake.RegionFilter = filter;

                //always pass 1 as higher threshold
                SetThreshold(hdRedParamsBreake, EndData.FracLowerThreshold, 1);

                //add if setup-need to chec frac or peels
                System.Windows.Rect ROIrect = new System.Windows.Rect();
                // RIO 1
                if ((EndData.IsFractions1 == "true" && EndData.Roi1 == "true") || (EndData.IsFractions2 == "true" && EndData.Roi2 == "true") ||
                    (EndData.IsFractions3 == "true" && EndData.Roi3 == "true"))
                {
                    //sPerformance += $"StartEvalFractions Profiling: Mark 4{iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                    if (!bRunPrepareOnLoad)
                    {
                        bool res = PrepairEval(image, true);
                        
                        if (!res)
                        {
                            StopCycle = true;
                            System.Windows.Forms.MessageBox.Show("Error Breaks Evaluation1! ", "ERROR", MessageBoxButtons.OK,
                            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
                        }
                    }
                    //sPerformance += $"StartEvalFractions Profiling: Mark 5 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                }


                if (EndData.Roi1 == "true" || chkCutImage.Checked)
                {
                    
                    ROIrect.X = EndData.roiPosX1;
                        ROIrect.Y = EndData.roiPosY1;
                        ROIrect.Width = EndData.roiWidth1;
                        ROIrect.Height = EndData.roiHeight1;
                        RegIndex = 0;
                        RegIndex2 = 0;
                    
                    //sPerformance += $"StartEvalFractions Profiling: Mark 7 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                    if (EndData.IsFractions1 == "true")
                    {
                        AddList("BREAK1_" + (num + 1).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); 

                        //sPerformance += $"StartEvalFractions Profiling: Mark 8 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        swNeta.Restart();
                        ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1, 1,
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model1Name].Tools.First().RegionOfInterest);
                        //NPNP
                        
                        //sPerformance += $"StartEvalFractions Profiling: Mark 9 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        //NPNP problem here???
                        if  (!bRunProcessOnceForAllROI || (bRunProcessOnceForAllROI && !bWasProcessRunForFractions))
                        {
                            samp1 = grunTimeWorkapace.StreamDict[Model1Name].Process(image);
                            bWasProcessRunForFractions = true;
                        }

                        
                        //sPerformance += $"StartEvalFractions Profiling: Mark 11 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        timeAll = timeAll + swNeta.ElapsedMilliseconds / 1000.0f;
                        getRagion(samp1, Model1Name, index, Iindex, regionFound, num, 1);
                        //sPerformance += $"StartEvalFractions Profiling: Mark 12 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        inv.settxt(lblDurNew, (swNeta.ElapsedMilliseconds/1000.0f).ToString());
                        inv.settxt(lblAll, timeAll.ToString());
                        AddList("FINI BREAK1 " + (swNeta.ElapsedMilliseconds/1000.0f).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));

                        //add founds to list.
                    }

                    //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                }

                // RIO 2

                if (EndData.Roi2 == "true" || chkCutImage.Checked)
                {
                    //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    //Thread.Sleep(200);
                    //sPerformance += $"StartEvalFractions Profiling: Mark 13 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                    ROIrect.X = EndData.roiPosX2;
                    ROIrect.Y = EndData.roiPosY2;
                    ROIrect.Width = EndData.roiWidth2;
                    ROIrect.Height = EndData.roiHeight2;
                    RegIndex = 0;
                    RegIndex2 = 0;
                    if (EndData.IsFractions2 == "true")
                    {
                        AddList("BREAK2_" + (num + 1).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); 

                        //sPerformance += $"StartEvalFractions Profiling: Mark 14 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1, 2,
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model1Name].Tools.First().RegionOfInterest);
                        //sPerformance += $"StartEvalFractions Profiling: Mark 15 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        if (!bRunProcessOnceForAllROI || (bRunProcessOnceForAllROI && !bWasProcessRunForFractions))
                        {
                            samp1 = grunTimeWorkapace.StreamDict[Model1Name].Process(image);
                            bWasProcessRunForFractions = true;
                        }
                        //samp1.Process();
                        //sPerformance += $"StartEvalFractions Profiling: Mark 16 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                        getRagion(samp1, Model1Name, index, Iindex, regionFound, num, 2);
                        //sPerformance += $"StartEvalFractions Profiling: Mark 17 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                        //add founds to list.
                    }

                    //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                }

                // RIO 3

                if (EndData.Roi3 == "true" || chkCutImage.Checked)
                {
                    //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    //Thread.Sleep(200);
                    //sPerformance += $"StartEvalFractions Profiling: Mark 18 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                    ROIrect.X = EndData.roiPosX3;
                    ROIrect.Y = EndData.roiPosY3;
                    ROIrect.Width = EndData.roiWidth3;
                    ROIrect.Height = EndData.roiHeight3;
                    RegIndex = 0;
                    RegIndex2 = 0;
                    if (EndData.IsFractions3 == "true")
                    {
                        AddList("BREAK3_" + (num + 1).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); 

                        //sPerformance += $"StartEvalFractions Profiling: Mark 19 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1, 3,
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model1Name].Tools.First().RegionOfInterest);
                        //sPerformance += $"StartEvalFractions Profiling: Mark 20 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        if (!bRunProcessOnceForAllROI || (bRunProcessOnceForAllROI && !bWasProcessRunForFractions))
                        {
                            samp1 = grunTimeWorkapace.StreamDict[Model1Name].Process(image);
                            bWasProcessRunForFractions = true;
                        }
                        //samp1.Process();
                        //sPerformance += $"StartEvalFractions Profiling: Mark 21 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                        getRagion(samp1, Model1Name, index, Iindex, regionFound, num, 3);
                        //sPerformance += $"StartEvalFractions Profiling: Mark 22 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                        //add founds to list.
                    }

                    //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                }

                bool bDebug = true;
                //if (bDebug)
                    //File.AppendAllText(sDirpath + @"Performance.log", sPerformance + Environment.NewLine);
                //inv.set(btnStartEval, "Enabled", true);
                //sw.Stop();
                //inv.settxt(lblDuration, (sw.ElapsedMilliseconds / 1000.0).ToString("0.000"));
                return true;
            }
            catch (System.Exception ex) { return false; }


        }
        private bool startEvalPeels(string imagefile, int num)
        {

            Stopwatch sw = new Stopwatch();
            try
            {

                string sPerformance = "";
                var swPerformance = Stopwatch.StartNew();
                
                int iProfilingMark = 0;

                
                System.Windows.Rect ROIrect = new System.Windows.Rect();

                WpfImage image = null;
                if (imageCleS[num] == null)
                {
                    return false;
                }
                image = imageCleS[num];
                //sPerformance += $"StartEvalPeels Profiling: Mark 2 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;


                Rectangle ImageDimensions = new Rectangle(0, 0, image.Width, image.Height);
                //string cn = "";
                //string cn2 = "";

                int index2 = 0;
                int Iindex = 0;
                resArr.resIndex = 0;
                resArr.resInfo.ShowRes = new string[20];
                

                //add if setup-need to chec frac or peels
                // RIO 1


                //follow to see if one .Process() method was already per brain per inspection cycle
                bool bWasProcessRunForPeels = false;

                ViDi2.Runtime.IRedHighDetailParameters hdRedParamPeels = grunTimeWorkapace.StreamDict[Model2Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                //Filter only for area. Score will be filtered via threshold
                string filter1 = "area>= " + txtPeelLowerArea.Text;// + " and score>=" + txtPeelScore.Text.Trim();
                hdRedParamPeels.RegionFilter = filter1;

                //always pass 1 as higher threshold
                SetThreshold(hdRedParamPeels, EndData.PeelLowerThreshold, 1);

                //sPerformance += $"StartEvalPeels Profiling: Mark 3 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                if ((EndData.IsPeels1 == "true" && EndData.Roi1 == "true") || (EndData.IsPeels2 == "true" && EndData.Roi2 == "true") ||
                    (EndData.IsPeels3 == "true" && EndData.Roi3 == "true"))
                {

                    //sPerformance += $"StartEvalPeels Profiling: Mark 4 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                    if (!bRunPrepareOnLoad)
                    {
                        bool res = PrepairEval(image, false);
                        
                        if (!res)
                        {
                            StopCycle = true;
                            System.Windows.Forms.MessageBox.Show("Error Peels Evaluation1! ", "ERROR", MessageBoxButtons.OK,
                            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
                        }
                    }
                    //PrepairEvalPl = true;
                    //sPerformance += $"StartEvalPeels Profiling: Mark 5 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                }

                if (EndData.Roi1 == "true" || chkCutImage.Checked)
                {
                    //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    //sPerformance += $"StartEvalPeels Profiling: Mark 6 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                    ROIrect.X = EndData.roiPosX1;
                    ROIrect.Y = EndData.roiPosY1;
                    ROIrect.Width = EndData.roiWidth1;
                    ROIrect.Height = EndData.roiHeight1;
                    RegIndex = 0;
                    RegIndex2 = 0;
                    if (EndData.IsPeels1 == "true")
                    {
                        AddList("PEELS1_" + (num + 1).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); 
                        //NPNP
                        //ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2, 1);
                        //sPerformance += $"StartEvalPeels Profiling: Mark 7 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        sw.Restart();
                        ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2, 1, 
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model2Name].Tools.First().RegionOfInterest);
                        //sPerformance += $"StartEvalPeels Profiling: Mark 8 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                        //NPNP problem here???
                        //                        samp2.Process();
                        if (!bRunProcessOnceForAllROI || (bRunProcessOnceForAllROI && !bWasProcessRunForPeels))
                        {
                            samp2 = grunTimeWorkapace.StreamDict[Model2Name].Process(image);
                            bWasProcessRunForPeels = true;
                        }
                        //sPerformance += $"StartEvalPeels Profiling: Mark 9 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                        timeAll = timeAll + sw.ElapsedMilliseconds / 1000.0f;
                        getRagion(samp2, Model2Name, index2, Iindex, regionFound2, num, 1);
                        inv.settxt(lblDurNew, (sw.ElapsedMilliseconds / 1000.0f).ToString());
                        inv.settxt(lblAll, timeAll.ToString());
                        AddList("FINI PEELS1 " + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                        //add founds to list.
                        //sPerformance += $"StartEvalPeels Profiling: Mark 10 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                    }
                    //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                }


                

                if (EndData.Roi2 == "true" || chkCutImage.Checked)
                {
                    //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    //sPerformance += $"StartEvalPeels Profiling: Mark 11 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                    ROIrect.X = EndData.roiPosX2;
                    ROIrect.Y = EndData.roiPosY2;
                    ROIrect.Width = EndData.roiWidth2;
                    ROIrect.Height = EndData.roiHeight2;
                    RegIndex = 0;
                    RegIndex2 = 0;
                    if (EndData.IsPeels2 == "true")
                    {
                        AddList("PEELS2_" + (num + 1).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); 
                        //NPNP
                        //sPerformance += $"StartEvalPeels Profiling: Mark 12 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2, 2,
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model2Name].Tools.First().RegionOfInterest);
                        //sPerformance += $"StartEvalPeels Profiling: Mark 13 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        if (!bRunProcessOnceForAllROI || (bRunProcessOnceForAllROI && !bWasProcessRunForPeels))
                        {
                            samp2 = grunTimeWorkapace.StreamDict[Model2Name].Process(image);
                            bWasProcessRunForPeels = true;
                        }
                        //samp2.Process();
                        //sPerformance += $"StartEvalPeels Profiling: Mark 14 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                        getRagion(samp2, Model2Name, index2, Iindex, regionFound2, num, 2);
                        //sPerformance += $"StartEvalPeels Profiling: Mark 15 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        //add founds to list.
                    }
                    //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                }


                // RIO 3



                if (EndData.Roi3 == "true" || chkCutImage.Checked)
                {
                    //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    //sPerformance += $"StartEvalPeels Profiling: Mark 16 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                    ROIrect.X = EndData.roiPosX3;
                    ROIrect.Y = EndData.roiPosY3;
                    ROIrect.Width = EndData.roiWidth3;
                    ROIrect.Height = EndData.roiHeight3;
                    RegIndex = 0;
                    RegIndex2 = 0;
                    if (EndData.IsPeels3 == "true")
                    {
                        AddList("PEELS3_" + (num + 1).ToString() + " //" + DateTime.Now.ToString("HH:mm:ss.fff"));
                        //NPNP
                        //sPerformance += $"StartEvalPeels Profiling: Mark 17 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2, 3,
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model2Name].Tools.First().RegionOfInterest);
                        //sPerformance += $"StartEvalPeels Profiling: Mark 18 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                        if (!bRunProcessOnceForAllROI || (bRunProcessOnceForAllROI && !bWasProcessRunForPeels))
                        {
                            samp2 = grunTimeWorkapace.StreamDict[Model2Name].Process(image);
                            bWasProcessRunForPeels = true;
                        }
                        //samp2.Process();
                        //sPerformance += $"StartEvalPeels Profiling: Mark 19 {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;

                        getRagion(samp2, Model2Name, index2, Iindex, regionFound2, num, 3);
                        //sPerformance += $"StartEvalPeels Profiling: Mark {iProfilingMark} {DateTime.Now.ToString()} Image {num}: {swPerformance.ElapsedMilliseconds}\n "; iProfilingMark++;
                        //add founds to list.
                    }
                    //this.Invoke((Action)(() => { listBox1.Items.Add("fini test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                }



                bool bDebug = false;
                //if (bDebug)
                //    File.AppendAllText(sDirpath + @"Performance.log", sPerformance + Environment.NewLine);

                //inv.set(btnStartEval, "Enabled", true);
                //sw.Stop();
                //inv.settxt(lblDuration, (sw.ElapsedMilliseconds / 1000.0).ToString("0.000"));
                return true;
            }
            catch (System.Exception ex)
            {
                return false;
            }


        }

        public void getRegion(ISample samp, string modelName, RegionFound[] regionFound, int CycleNum, int reg)
        {
            getRagion(samp, modelName, 0, 0, regionFound, CycleNum, reg);
        }

        public void getRagion(ISample samp, string modelName, int index, int Iindex, RegionFound[] regionFound, int CycleNum, int reg)
        {
            Dictionary<string, IMarking> mark = samp.Markings;

            Dictionary<string, IMarking>.KeyCollection MarkKey = mark.Keys;

            //NPNP
            //IMarking TryM = mark["red_HDM_20M_5472x3648"];
            IMarking TryM = mark.Values.First();
            m_dTotalFrontCognexTime += TryM.Duration;
            m_sTotalFrontCognexTime += TryM.Duration.ToString() + ",";

            ViDi2.IView View = TryM.Views[0];// mm.Marking.Views[0];

            ViDi2.IRedView redview = (ViDi2.IRedView)View;

            regionFound = new RegionFound[redview.Regions.Count];

            ViDi2.Runtime.IRedTool tool = (ViDi2.Runtime.IRedTool)grunTimeWorkapace.StreamDict[modelName].Tools.First();

            var knownClasses = tool.KnownClasses;

            string className = knownClasses[0];
            string[] s = className.Split('_');
            string cn = "";

            if (s.GetLength(0) > 0)
            {
                if (s.GetLength(0) > 1)
                    cn = s[1];
                else
                    cn = s[0];
            }
            this.Invoke((Action)(() =>
            {
                if (modelName == Model1Name)
                    ShowRegions1(redview, regionFound, index, cn, Iindex, CycleNum, reg);
                else
                    ShowRegions2(redview, regionFound, index, cn, Iindex, CycleNum, reg);
            }));
            //if (modelName == Model1Name)
            //    ShowRegions1(redview, regionFound, index, cn, Iindex);
            //else
            //    ShowRegions2(redview, regionFound, index, cn, Iindex);
        }
        public string[] RegionFound1BrSave = new string[1];
        public string[] RegionFound1PlSave = new string[1];
        public string[] RegionFound2BrSave = new string[1];
        public string[] RegionFound2PlSave = new string[1];
        public string[] RegionFound3BrSave = new string[1];
        public string[] RegionFound3PlSave = new string[1];
        public int RegIndex = 0;
        public int RegIndex2 = 0;
        public void ShowRegions1(ViDi2.IRedView redview, RegionFound[] regionFound2, int index2, string cn2, int Iindex, int CycleNum, int reg)
        {
            string sShowRegions1Performance = "ShowRegions1";
            var swShowRegions1 = Stopwatch.StartNew();
            if (redview.Regions.Count != 0)
            {
                foreach (ViDi2.IRegion item in redview.Regions)
                {
                    // sShowRegions1Performance += $"ShowRegions1 Mark 1: {swShowRegions1.ElapsedMilliseconds} \n ";
                    regionFound2[index2].area = item.Area;

                    regionFound2[index2].width = item.Width;
                    regionFound2[index2].height = item.Height;
                    regionFound2[index2].center = item.Center;
                    regionFound2[index2].score = item.Score;
                    regionFound2[index2].className = cn2;  // item.Name; region name
                    regionFound2[index2].classColor = item.Color;
                    regionFound2[index2].compactness = item.Compactness;
                    regionFound2[index2].covers = item.Covers;
                    regionFound2[index2].outer = item.Outer;
                    regionFound2[index2].perimeter = item.Perimeter;
                    regionFound2[index2].X0 = item.Center.X;
                    regionFound2[index2].Y0 = item.Center.Y;
                    regionFound2[index2].H = item.Height;
                    regionFound2[index2].W = item.Width;
                    // sShowRegions1Performance += $"ShowRegions1 Mark 2: {swShowRegions1.ElapsedMilliseconds} \n ";
                    string[] res1 = new string[redview.Regions.Count];
                    // sShowRegions1Performance += $"ShowRegions1 Mark 3: {swShowRegions1.ElapsedMilliseconds} \n ";
                    res1[index2] = (CycleNum+1).ToString("00") + " "
                        //+ (index2).ToString() 
                        + " ROI:" + reg.ToString() + " " + "Break:" + " Score=" + regionFound2[index2].score.ToString() + " " +
                        "Area=" + regionFound2[index2].area.ToString() +" "+ 
                        "Perimeter=" + regionFound2[index2].perimeter.ToString() + " " +
                        "Ourer=" + regionFound2[index2].outer.Count.ToString() + " " +
                         "X0=" + regionFound2[index2].X0.ToString() + " " +
                         "Y0=" + regionFound2[index2].Y0.ToString() + " " +
                          "H=" + regionFound2[index2].H.ToString() + " " +
                           "W=" + regionFound2[index2].W.ToString() ;

                    // sShowRegions1Performance += $"ShowRegions1 Mark 4: {swShowRegions1.ElapsedMilliseconds} \n ";
                    //NPNP
                    //if (!bUseWholeImageAsROI)
                    if (m_eCognexROIType==eCognexROIType.UseAsBeforeGeographicROI)
                    {
                        AddList(res1[index2]);

                        if (reg == 1)
                        {
                            RegionFound1BrSave[RegionFound1BrSave.Length - 1] = res1[index2];
                            Array.Resize<String>(ref RegionFound1BrSave, RegionFound1BrSave.Length + 1);
                        }
                        else if (reg == 2)
                        {
                            RegionFound2BrSave[RegionFound2BrSave.Length - 1] = res1[index2];
                            Array.Resize<String>(ref RegionFound2BrSave, RegionFound2BrSave.Length + 1);
                        }
                        else if (reg == 3)
                        {
                            RegionFound3BrSave[RegionFound3BrSave.Length - 1] = res1[index2];
                            Array.Resize<String>(ref RegionFound3BrSave, RegionFound3BrSave.Length + 1);
                        }
                    }
                    // sShowRegions1Performance += $"ShowRegions1 Mark 5: {swShowRegions1.ElapsedMilliseconds} \n ";

                    //Array.Resize<String>(ref RegionFound1BrSave, RegionFound1BrSave.Length + 1);
                    Iindex++;
                    index2++;
                    RegIndex2++;
                }

                //use ONE ROI for cognex. The "Geographical ROIs" used in the GUI are preprocess
                if(m_eCognexROIType==eCognexROIType.eUseWholeImageAsROI || m_eCognexROIType == eCognexROIType.eUseWholeImageMinus400pixlesAsROI)
                {
                    // sShowRegions1Performance += $"ShowRegions1 Mark 6: {swShowRegions1.ElapsedMilliseconds} \n ";
                    ReportInferenceResults(true, CycleNum, reg, redview);
                    // sShowRegions1Performance += $"ShowRegions1 Mark 7: {swShowRegions1.ElapsedMilliseconds} \n ";
                }

                //File.AppendAllText(sDirpath + @"PerformanceShowRegions1.log", $"{DateTime.Now} " + sShowRegions1Performance  + Environment.NewLine);

                PealRegions = regionFound2;
            }
        }

        public void ShowRegions2(ViDi2.IRedView redview, RegionFound[] regionFound, int index, string cn, int Iindex, int CycleNum, int reg)
        {
            if (redview.Regions.Count != 0)
            {
                foreach (ViDi2.IRegion item in redview.Regions)
                {
                    regionFound[index].area = item.Area;
                    regionFound[index].width = item.Width;
                    regionFound[index].height = item.Height;
                    regionFound[index].center = item.Center;
                    regionFound[index].score = item.Score;
                    regionFound[index].className = cn;  // item.Name; region name
                    regionFound[index].classColor = item.Color;
                    regionFound[index].compactness = item.Compactness;
                    regionFound[index].covers = item.Covers;
                    regionFound[index].outer = item.Outer;
                    regionFound[index].perimeter = item.Perimeter;
                    regionFound[index].X0 = item.Center.X;
                    regionFound[index].Y0 = item.Center.Y;
                    regionFound[index].H = item.Height;
                    regionFound[index].W = item.Width;
                    string[] res1 = new string[redview.Regions.Count];

                    res1[index] = (CycleNum+1).ToString("00") +" "
                        //+ (index).ToString() 
                        + " ROI:" + reg.ToString() + " " + "Peels: " + "Score=" + regionFound[index].score.ToString() + " " +
                        "Area=" + regionFound[index].area.ToString() + " " +
                        "Perimeter=" + regionFound[index].perimeter.ToString() + " " +
                        "Ourer=" + regionFound[index].outer.Count.ToString() + " " +
                        "X0=" + regionFound[index].X0.ToString() + " " +
                         " Y0=" + regionFound[index].Y0.ToString() + " " +
                          "H=" + regionFound[index].H.ToString() + " " +
                           "W=" + regionFound[index].W.ToString() ;

                    //NPNP
                    //if (!bUseWholeImageAsROI)
                    if (m_eCognexROIType==eCognexROIType.UseAsBeforeGeographicROI)
                    {
                        AddList(res1[index]);
                        if (reg == 1) { RegionFound1PlSave[RegionFound1PlSave.Length - 1] = res1[index]; Array.Resize<String>(ref RegionFound1PlSave, RegionFound1PlSave.Length + 1); }
                        else if (reg == 2) { RegionFound2PlSave[RegionFound2PlSave.Length - 1] = res1[index]; Array.Resize<String>(ref RegionFound2PlSave, RegionFound2PlSave.Length + 1); }
                        else if (reg == 3) { RegionFound3PlSave[RegionFound3PlSave.Length - 1] = res1[index]; Array.Resize<String>(ref RegionFound3PlSave, RegionFound3PlSave.Length + 1); }
                    }
                    //RegionFound1PlSave[RegIndex] = res1[index];
                    //Array.Resize<String>(ref RegionFound1PlSave, RegionFound1PlSave.Length + 1);
                    Iindex++;
                    index++;
                    RegIndex++;
                }
                //use ONE ROI for cognex. The "Geographical ROIs" used in the GUI are preprocess
                if (m_eCognexROIType==eCognexROIType.eUseWholeImageAsROI || m_eCognexROIType == eCognexROIType.eUseWholeImageMinus400pixlesAsROI)
                {
                    ReportInferenceResults(false, CycleNum, reg, redview);
                }
                BreakeRegions = regionFound;
            }
        }
        public void InitializeWorkspaces()
        {
            List<int> GPUList = new List<int>();
            control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);
            //Gpu Cards
            control.InitializeComputeDevices(GpuMode.SingleDevicePerTool, GPUList);
            //how many cards
            var computeDevices = control.ComputeDevices;
            //create stream dictionary
            var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();

            string gpuID = "default/red_HDM_20M_5472x3648/0";
            string wsName = Model1Name;
            string wsPath = @"C:\Users\inspmachha\Desktop\final models\Proj_021_201223_104500_21122023_104445.vrws";
            string wsName2 = Model2Name;
            string wsPath2 = @"C:\Users\inspmachha\Desktop\final models\WS_Proj_022_261223_111400_261223_183645.vrws";

            string wsNameFront = Model1NameFront;
            string wsPathFront = @"C:\Users\inspmachha\Desktop\final models\FrontCSInspectionFullArea3Lightings.vrws";
            //string wsName2Front = Model2NameFront;
            string wsPath2Front = @"C:\Users\inspmachha\Desktop\final models\FrontCSInspectionFullArea3Lightings.vrws";

            StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath, gpuID).Streams["default"]);
            StreamDict.Add(wsName2, control.Workspaces.Add(wsName2, wsPath2, gpuID).Streams["default"]);

            StreamDict.Add(wsNameFront, control.Workspaces.Add(wsNameFront, wsPathFront, gpuID).Streams["default"]);
            //StreamDict.Add(wsName2Front, control.Workspaces.Add(wsName2Front, wsPath2Front, gpuID).Streams["default"]);

            grunTimeWorkapace.gpuId01 = 0;
            grunTimeWorkapace.StreamDict = StreamDict;
            //call sized picture
            //getSizedImage(grunTimeWorkapace);
        }
        public string CmbCatNumText="";
        public string CmbCatNumText1 = "";

        public void AttachChangeEventToTextAndCheckBoxes(System.Windows.Forms.Control.ControlCollection controls)
        {
            foreach (System.Windows.Forms.Control aControl in controls)
            {
                if (aControl is System.Windows.Forms.TextBox && aControl.Name != "txtListBox1")
                {
                    ((System.Windows.Forms.TextBox)aControl).TextChanged += TextBoxChanged;
                }
                if (aControl is System.Windows.Forms.CheckBox)
                {
                    ((System.Windows.Forms.CheckBox)aControl).CheckedChanged += CheckBoxChanged;
                }
                AttachChangeEventToTextAndCheckBoxes(aControl.Controls);
            }
        }

        public void ClearTextBoxesBackGroundColor(System.Windows.Forms.Control.ControlCollection controls)
        {
            foreach (System.Windows.Forms.Control aControl in controls)
            {
                if (aControl is System.Windows.Forms.TextBox)
                {
                    ((System.Windows.Forms.TextBox)aControl).BackColor = SystemColors.Window;
                }
                if (aControl is System.Windows.Forms.CheckBox)
                {
                    ((System.Windows.Forms.CheckBox)aControl).BackColor = Color.Transparent;
                }
                ClearTextBoxesBackGroundColor(aControl.Controls);
            }
        }

        public void CheckBoxChanged(object sender, EventArgs e)
        {
            if (!bIsCatalogueNumberChanging)
            {
                ((System.Windows.Forms.CheckBox)sender).BackColor = Color.Red;
                SetControlsChanged();
            }
        }
        public void TextBoxChanged(object sender,EventArgs e)
        {
            if (!bIsCatalogueNumberChanging)
            {
                ((System.Windows.Forms.TextBox)sender).BackColor = Color.Red;
                SetControlsChanged();
            }
        }
        public void SetControlsChanged()
        {
            bDataInControlsChanged = true;
            btnSaveROI.BackColor = Color.Red;
            btnSaveROI1.BackColor = Color.Red;
        }
        public void ClearControlsChanged()
        {
            bDataInControlsChanged = false;
            btnSaveROI.BackColor = SystemColors.Control;
            btnSaveROI1.BackColor = SystemColors.Control;
            ClearTextBoxesBackGroundColor(this.Controls);
        }


        public void CmbCatNum_SelectedValueChanged(object sender, EventArgs e)
        {
            try
            {
                bIsCatalogueNumberChanging = true;
                CmbCatNumText = CmbCatNum.Text;
                if (CmbCatNum.Text != "")
                {
                    
                    EndData = JassonDataClass.getJassonParameters(CmbCatNum.Text);
                    UpdateTexts(EndData);
                    UpdateTexts1(EndData);
                    if (chkShowImage.Checked)
                    {
                        try
                        {
                            using (var file = new FileStream(EndData.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Inheritable))
                            {
                                onload = true;
                                pictureBoxInspect.Image = (Bitmap)Bitmap.FromStream(file); // Image.FromFile(EndData.ImagePath);
                                file.Close();

                            }
                        }
                        catch
                        {
                            System.Windows.Forms.MessageBox.Show($"Couldn't find {EndData.ImagePath} in item {EndData.EndmillName} in {frmMain.JassonPath}");
                        }
                    }
                    //NPNP
                    //
                    if (EndData.iExposureTimeDefectInspection < 300000 && EndData.iExposureTimeDefectInspection > 100)
                    {
                        myCustomEventArgInt.iint = EndData.iExposureTimeDefectInspection;
                        if(onExposureChangedFromCatalogue != null)
                            onExposureChangedFromCatalogue(this, myCustomEventArgInt);
                    }
                    btnStartEval.Enabled = true;
                }
                else btnStartEval.Enabled = false;
                bIsCatalogueNumberChanging = false;

            }
            catch (System.Exception ex) { btnStartEval.Enabled = true; };
        }

        private void LoadEndmills()
        {
            //C:\Project\4.2.2025\InspectSolution\setUpApplication\projSampaleViewer\bin\x64\Debug\Data\DataBase\EndmillsData.Jason";
            string filePath = JassonPath;// @"C:\Users\inspmachha\Desktop\setUpApplication - Copy\projSampaleViewer\bin\x64\Debug\Data\DataBase\EndmillsData.Jason"; // Set the path to your JSON file

            try
            {
                string json = File.ReadAllText(filePath); // Read the JSON data from the file
                //NPNP
                //endmills = JsonConvert.DeserializeObject<List<EndmillData>>(json); // Deserialize JSON data into the endmills list
                endmills = JsonConvert.DeserializeObject<List<BeckhoffBasler.Endmill>>(json); // Deserialize JSON data into the endmills list
                if (endmills == null)
                {
                    System.Windows.Forms.MessageBox.Show("Failed to load endmill data. Please check the JSON format.");
                    return;
                }
                CmbCatNum.Items.AddRange(endmills.Select(e => e.EndmillName).ToArray()); // Populate ComboBox with endmill names
                CmbCatNumText = CmbCatNum.Text;
            }
            catch (System.Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"An error occurred while loading endmill data: {ex.Message}");
            }
        }

        public static string PromptForInput(string prompt, string defaultValue , bool bInputEnabled = true)
        {
            // Create form
            Form promptForm = new Form()
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Input",
                StartPosition = FormStartPosition.CenterScreen
            };

            Label lbl = new Label() { Left = 10, Top = 20, Text = prompt, Width = 360 };
            System.Windows.Forms.TextBox txtBox = new System.Windows.Forms.TextBox() { Left = 10, Top = 50, Width = 360, Text = defaultValue , Enabled = bInputEnabled };

            System.Windows.Forms.Button btnOk = new System.Windows.Forms.Button() { Text = "OK", Left = 220, Width = 75, Top = 80, DialogResult = DialogResult.OK };
            System.Windows.Forms.Button btnCancel = new System.Windows.Forms.Button() { Text = "Cancel", Left = 295, Width = 75, Top = 80, DialogResult = DialogResult.Cancel };

            promptForm.Controls.Add(lbl);
            promptForm.Controls.Add(txtBox);
            promptForm.Controls.Add(btnOk);
            promptForm.Controls.Add(btnCancel);

            promptForm.AcceptButton = btnOk;
            promptForm.CancelButton = btnCancel;

            // Show modal
            if (promptForm.ShowDialog() == DialogResult.OK)
                return txtBox.Text;
            else
                return null; // user hit Cancel
        }


        public void CmbCatNum_SelectedIndexChanged(object sender, EventArgs e)
        {
            CmbCatNumText = CmbCatNum.Text;
            return;
            //if (endmills == null)
            //{
            //    System.Windows.Forms.MessageBox.Show("Endmill data is not loaded.");
            //    return;
            //}
            //string selectedEndmillName = CmbCatNum.SelectedItem.ToString();
            //EndmillData selectedEndmill = endmills.FirstOrDefault(endmill => endmill.EndmillName == selectedEndmillName);

            //if (selectedEndmill != null)
            //{
            //    txtPProiPosX.Text = selectedEndmill.RoiPosX1.ToString();
            //    txtPProiPosY.Text = selectedEndmill.RoiPosY1.ToString();
            //    txtPProiWidth.Text = selectedEndmill.RoiWidth1.ToString();
            //    txtPProiHeight.Text = selectedEndmill.RoiHeight1.ToString();
            //    txtROIangle.Text = selectedEndmill.RoiAngle1.ToString();
            //    txtRatioImage2ROI.Text = selectedEndmill.RoiRatio1.ToString();

            //    if (selectedEndmill.IsFractions1 == "true")
            //    {
            //        chkFractions1.Checked = true;
            //    }
            //    else chkFractions1.Checked = false;
            //    if (selectedEndmill.IsPeels1 == "true")
            //    {
            //        chkPeels1.Checked = true;
            //    }
            //    else chkPeels1.Checked = false;
            //    if (selectedEndmill.Roi1 == "true")
            //    {
            //        chkROI1.Checked = true;
            //    }
            //    else chkROI1.Checked = false;
            //    txtPosX2.Text = selectedEndmill.RoiPosX2.ToString();
            //    txtPosY2.Text = selectedEndmill.RoiPosY2.ToString();
            //    txtWidth2.Text = selectedEndmill.RoiWidth2.ToString();
            //    txtHeight2.Text = selectedEndmill.RoiHeight2.ToString();
            //    txtAngle2.Text = selectedEndmill.RoiAngle2.ToString();
            //    txtRatio2.Text = selectedEndmill.RoiRatio2.ToString();

            //    if (selectedEndmill.IsFractions2 == "true")
            //    {
            //        chkFractions2.Checked = true;
            //    }
            //    else chkFractions2.Checked = false;
            //    if (selectedEndmill.IsPeels2 == "true")
            //    {
            //        chkPeels2.Checked = true;
            //    }
            //    else chkPeels2.Checked = false;
            //    if (selectedEndmill.Roi2 == "true")
            //    {
            //        chkROI2.Checked = true;
            //    }
            //    else chkROI2.Checked = false;
            //    txtPosX3.Text = selectedEndmill.RoiPosX3.ToString();
            //    txtPosY3.Text = selectedEndmill.RoiPosY3.ToString();
            //    txtWidth3.Text = selectedEndmill.RoiWidth3.ToString();
            //    txtHeight3.Text = selectedEndmill.RoiHeight3.ToString();
            //    txtAngle3.Text = selectedEndmill.RoiAngle3.ToString();
            //    txtRatio3.Text = selectedEndmill.RoiRatio3.ToString();

            //    if (selectedEndmill.IsFractions3 == "true")
            //    {
            //        chkFractions3.Checked = true;
            //    }
            //    else chkFractions3.Checked = false;
            //    if (selectedEndmill.IsPeels3 == "true")
            //    {
            //        chkPeels3.Checked = true;
            //    }
            //    else chkPeels3.Checked = false;
            //    if (selectedEndmill.Roi3 == "true")
            //    {
            //        chkROI3.Checked = true;
            //    }
            //    else chkROI3.Checked = false;
            //}
        }
        private void UpdateTexts(Endmill endmill)
        {

            //if (endmills == null)
            //{
            //    System.Windows.Forms.MessageBox.Show("Endmill data is not loaded.");
            //    return;
            //}
            //string selectedEndmillName = CmbCatNum.SelectedItem.ToString();
            //EndmillData selectedEndmill = endmills.FirstOrDefault(endmill => endmill.EndmillName == selectedEndmillName);

            if (endmill != null)
            {
                txtPProiPosX.Text = endmill.roiPosX1.ToString();
                txtPProiPosY.Text = endmill.roiPosY1.ToString();
                txtPProiWidth.Text = endmill.roiWidth1.ToString();
                txtPProiHeight.Text = endmill.roiHeight1.ToString();
                txtROIangle.Text = endmill.roiAngle1.ToString();
                //txtRatioImage2ROI.Text = endmill.roiRatio1.ToString();

                if (endmill.IsFractions1 == "true")
                {
                    chkFractions1.Checked = true;
                }
                else chkFractions1.Checked = false;
                if (endmill.IsPeels1 == "true")
                {
                    chkPeels1.Checked = true;
                }
                else chkPeels1.Checked = false;
                if (endmill.Roi1 == "true")
                {
                    chkROI1.Checked = true;
                }
                else chkROI1.Checked = false;
                txtPosX2.Text = endmill.roiPosX2.ToString();
                txtPosY2.Text = endmill.roiPosY2.ToString();
                txtWidth2.Text = endmill.roiWidth2.ToString();
                txtHeight2.Text = endmill.roiHeight2.ToString();
                txtAngle2.Text = endmill.roiAngle2.ToString();
                //txtRatio2.Text = endmill.roiRatio2.ToString();

                if (endmill.IsFractions2 == "true")
                {
                    chkFractions2.Checked = true;
                }
                else chkFractions2.Checked = false;
                if (endmill.IsPeels2 == "true")
                {
                    chkPeels2.Checked = true;
                }
                else chkPeels2.Checked = false;
                if (endmill.Roi2 == "true")
                {
                    chkROI2.Checked = true;
                }
                else chkROI2.Checked = false;
                txtPosX3.Text = endmill.roiPosX3.ToString();
                txtPosY3.Text = endmill.roiPosY3.ToString();
                txtWidth3.Text = endmill.roiWidth3.ToString();
                txtHeight3.Text = endmill.roiHeight3.ToString();
                txtAngle3.Text = endmill.roiAngle3.ToString();
                //txtRatio3.Text = endmill.roiRatio3.ToString();

                if (endmill.IsFractions3 == "true")
                {
                    chkFractions3.Checked = true;
                }
                else chkFractions3.Checked = false;
                if (endmill.IsPeels3 == "true")
                {
                    chkPeels3.Checked = true;
                }
                else chkPeels3.Checked = false;
                if (endmill.Roi3 == "true")
                {
                    chkROI3.Checked = true;
                }
                else chkROI3.Checked = false;

                txtFractionLower.Text= endmill.FracLowerThreshold.ToString();
                txtFractionUpper.Text = endmill.FracUpperThreshold.ToString();

                txtPeelLower.Text = endmill.PeelLowerThreshold.ToString();
                txtPeelUpper.Text = endmill.PeelUpperThreshold.ToString();

                txtFractionLowerArea.Text = endmill.FracLowerArea.ToString();
                txtFractionScore.Text = endmill.FracScore.ToString();
                txtPeelLowerArea.Text = endmill.PeelLowerArea.ToString();
                txtPeelScore.Text = endmill.PeelScore.ToString();

                //NPNP
                txtNumberOfTopImages.Text = endmill.iNumberOfTopImages.ToString();

            }
        }

        private void ProcessImages(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string imagePath;
                while (imageQueue.TryDequeue(out imagePath))
                {
                    ProcessImage(imagePath);
                }

                // Avoid tight loop, sleep for a short period if queue is empty
                Thread.Sleep(100);
            }
        }

        private void ProcessImage(string imagePath)
        {
            //listBox1.Items.Clear();
            inv.settxt(txtListBox1, "");
            lstStr = "";
            string iniFileName = CmbCatNum.Text + ".ini";
            string iniFilePath = sDirpath + @"Data\DataBase\" + iniFileName;
            List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();
            ViDi2.Runtime.IRedHighDetailParameters hdRedParamPeels = grunTimeWorkapace.StreamDict[Model2Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
            ViDi2.Runtime.IRedHighDetailParameters hdRedParamsBreake = grunTimeWorkapace.StreamDict[Model1Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
            System.Windows.Rect ROIrect = new System.Windows.Rect();
            var image = new ViDi2.UI.WpfImage(imagePath);
            // Your existing image processing code here
            SetThreshold(hdRedParamsBreake, EndData.FracLowerThreshold, EndData.FracLowerThreshold);
            SetThreshold(hdRedParamPeels, EndData.PeelLowerThreshold, EndData.PeelUpperThreshold);
            Rectangle ImageDimensions = new Rectangle(0, 0, image.Width, image.Height);

            //add if setup-need to chec frac or peels
            ISample samp1 = grunTimeWorkapace.StreamDict[Model1Name].Process(image);
            ISample samp2 = grunTimeWorkapace.StreamDict[Model2Name].Process(image);

            Dictionary<string, IMarking> mark = samp1.Markings;
            Dictionary<string, IMarking> mark2 = samp2.Markings;

            Dictionary<string, IMarking>.KeyCollection MarkKey = mark.Keys;
            Dictionary<string, IMarking>.KeyCollection MarkKey2 = mark2.Keys;

            IMarking TryM = mark["red_HDM_20M_5472x3648"];
            IMarking TryM2 = mark2["red_HDM_20M_5472x3648"];

            ViDi2.IView View = TryM.Views[0];// mm.Marking.Views[0];
            ViDi2.IView View2 = TryM2.Views[0];

            ViDi2.IRedView redview = (ViDi2.IRedView)View;
            ViDi2.IRedView redview2 = (ViDi2.IRedView)View2;

            RegionFound[] regionFound = new RegionFound[redview.Regions.Count];
            RegionFound[] regionFound2 = new RegionFound[redview2.Regions.Count];

            ViDi2.Runtime.IRedTool tool = (ViDi2.Runtime.IRedTool)grunTimeWorkapace.StreamDict[Model1Name].Tools.First();
            ViDi2.Runtime.IRedTool tool2 = (ViDi2.Runtime.IRedTool)grunTimeWorkapace.StreamDict[Model2Name].Tools.First();

            var knownClasses = tool.KnownClasses;
            var knownClasses2 = tool2.KnownClasses;

            string className = knownClasses[0];
            string className2 = knownClasses2[0];
            string[] s = className.Split('_');
            string[] s2 = className2.Split('_');
            string cn = "";
            string cn2 = "";

            int index2 = 0;
            int Iindex = 0;
            resArr.resIndex = 0;
            resArr.resInfo.ShowRes = new string[20];
            int index = 0;
            if (EndData.Roi1 == "true")
            {
                ROIrect.X = EndData.roiPosX1;
                ROIrect.Y = EndData.roiPosY1;
                ROIrect.Width = EndData.roiWidth1;
                ROIrect.Height = EndData.roiHeight1;
                if (EndData.IsFractions1 == "true")
                {
                    ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1, 1,
                        (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model1Name].Tools.First().RegionOfInterest);
                    samp1.Process();
                    getRagion(samp1, Model1Name, index, Iindex, regionFound,0,1);

                    //add founds to list.
                }
                if (EndData.IsPeels1 == "true")
                {
                    ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2, 1,
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model2Name].Tools.First().RegionOfInterest);
                    samp2.Process();
                    getRagion(samp2, Model2Name, index2, Iindex, regionFound2,0,1);
                    //add founds to list.
                }
            }
            if (EndData.Roi2 == "true")
            {
                ROIrect.X = EndData.roiPosX2;
                ROIrect.Y = EndData.roiPosY2;
                ROIrect.Width = EndData.roiWidth2;
                ROIrect.Height = EndData.roiHeight2;
                if (EndData.IsFractions2 == "true")
                {
                    ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1, 2,
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model1Name].Tools.First().RegionOfInterest);
                    samp1.Process();
                    getRagion(samp1, Model1Name, index, Iindex, regionFound,0,2);
                    //add founds to list.
                }
                if (EndData.IsPeels2 == "true")
                {
                    ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2, 2,
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model2Name].Tools.First().RegionOfInterest);
                    samp2.Process();
                    getRagion(samp2, Model2Name, index2, Iindex, regionFound2,0,2);
                    //add founds to list.
                }
            }
            if (EndData.Roi3 == "true")
            {
                ROIrect.X = EndData.roiPosX3;
                ROIrect.Y = EndData.roiPosY3;
                ROIrect.Width = EndData.roiWidth3;
                ROIrect.Height = EndData.roiHeight3;
                if (EndData.IsFractions3 == "true")
                {
                    ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp1, 3,
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model1Name].Tools.First().RegionOfInterest);
                    samp1.Process();
                    getRagion(samp1, Model1Name, index, Iindex, regionFound,0,3);
                    //add founds to list.
                }
                if (EndData.IsPeels3 == "true")
                {
                    ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, samp2, 3,
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model2Name].Tools.First().RegionOfInterest);
                    samp2.Process();
                    getRagion(samp2, Model2Name, index2, Iindex, regionFound2,0,3);
                    //add founds to list.
                }
            }

            // Continue with the rest of your processing logic...
        }

        private void StartImageProcessingTask()
        {
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            processingTask = Task.Run(() => ProcessImages(cancellationToken), cancellationToken);
        }

        private void images_Click(object sender, EventArgs e)
        {
            //frmBeckhoff bacoff = new frmBeckhoff();
            //bacoff.ShowDialog();
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {

            control.Dispose();
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

        public class EndmillData
        {
            public string EndmillName { get; set; }
            public int CatalogNo { get; set; }
            public int BladesNo { get; set; }
            public int Diameter { get; set; }
            public int Length { get; set; }
            public double FracLowerThreshold { get; set; }
            public double FracUpperThreshold { get; set; }
            public double PeelLowerThreshold { get; set; }
            public double PeelUpperThreshold { get; set; }
            public int RoiPosX1 { get; set; }
            public int RoiPosY1 { get; set; }
            public int RoiWidth1 { get; set; }
            public int RoiHeight1 { get; set; }
            public double RoiAngle1 { get; set; }
            public double RoiRatio1 { get; set; }
            public int RoiPosX2 { get; set; }
            public int RoiPosY2 { get; set; }
            public int RoiWidth2 { get; set; }
            public int RoiHeight2 { get; set; }
            public double RoiAngle2 { get; set; }
            public double RoiRatio2 { get; set; }
            public int RoiPosX3 { get; set; }
            public int RoiPosY3 { get; set; }
            public int RoiWidth3 { get; set; }
            public int RoiHeight3 { get; set; }
            public double RoiAngle3 { get; set; }
            public double RoiRatio3 { get; set; }
            public string ImagePath { get; set; }
            public string IsFractions1 { get; set; }
            public string IsPeels1 { get; set; }
            public string IsFractions2 { get; set; }
            public string IsPeels2 { get; set; }
            public string IsFractions3 { get; set; }
            public string IsPeels3 { get; set; }
            public string Roi1 { get; set; }
            public string Roi2 { get; set; }
            public string Roi3 { get; set; }
        }

        private void listBox1_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            //listBox1.Items.Clear();
            //lstStr = "";
        }

        private void btnShowROI_Click(object sender, EventArgs e)
        {
            if (!chkShowImage.Checked) return;
                //int width;
                //int height;
                //pictureBoxInspect.Image = null;
                ////pictureBoxInspect.ImageLocation = EndData.ImagePath;
                //using (var stream = File.OpenRead(EndData.ImagePath))
                //{
                //    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.Default);
                //    height = decoder.Frames[0].PixelHeight;
                //    width = decoder.Frames[0].PixelWidth;
                //    stream.Close();
                //}
                //Single scaleH = (Single)pictureBoxInspect.Height / (Single)height;
                //Single scaleW = (Single)pictureBoxInspect.Width / (Single)width;


                //Graphics gf = pictureBoxInspect.CreateGraphics();
                //gf.Clear(Color.Yellow);

                //Pen p = new Pen(Color.Red);
                //p.Width = 2;
                ////gf.DrawRectangle(p, int.Parse(txtPProiPosX.Text)* scaleH+2, int.Parse(txtPProiPosY.Text) * scaleW+2, 
                ////    int.Parse(txtPProiWidth.Text)* scaleW-2, int.Parse(txtPProiHeight.Text) * scaleH-2);
                //Rectangle re = new Rectangle(0, 0, 50, 50);
                //gf.DrawRectangle(p,re);
                paint = true;
            onload = true;
            //scale();
            pictureBoxInspect.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxInspect.Refresh();
        }
        bool paint = false;
        public Double scaleH = 0;
        public Double scaleW = 0;
        public Double scaleHimage = 0;
        public Double scaleWimage = 0;
        bool onload = false;
        bool mousemove = false;
        private void pictureBoxInspect_Paint(object sender, PaintEventArgs e)
        {
            if (!chkShowImage.Checked) return;
                //return;
                //if (!paint) return;
                try
            {
                paint = false;
                Rectangle re = new Rectangle();
                Pen p = new Pen(Color.Red);
                Double y01 = 0;

                pictureBoxInspect.Height = (int)(pictureBoxInspect.Width * 3648.0f / 5472.0f);
                //3648.0f / 5472.0f image for learning from C:\Project\4.2.2025\InspectSolution\setUpApplication\images
                pictureBoxInspect.SizeMode = PictureBoxSizeMode.StretchImage;
                scaleW = (Single)pictureBoxInspect.Width / (Single)pictureBoxInspect.Image.Width;
                scaleH = (Single)pictureBoxInspect.Height / (Single)pictureBoxInspect.Image.Height;
                if (bROI[0])
                {
                   

                            if (!onload && dragging)
                            {
                                p.Color = Color.Red;
                                txtPProiPosX.Text = ((int)(mouse_dwnX / scaleW)).ToString();

                                txtPProiPosY.Text = ((int)(mouse_dwnY / scaleH)).ToString();
                                txtPProiWidth.Text = ((int)((w) / scaleW)).ToString();

                                txtPProiHeight.Text = ((int)((h) / scaleH)).ToString();
                                re = new Rectangle((int)(mouse_dwnX), (int)(mouse_dwnY), (int)(w), (int)(h));
                            }
                            else
                            {
                                p.Color = Color.Red;
                                mouse_dwnX = (Single.Parse(txtPProiPosX.Text) * scaleW);
                                mouse_dwnY = (Single.Parse(txtPProiPosY.Text) * scaleH);
                                h = (Single.Parse(txtPProiHeight.Text)) * scaleH;
                                w = (Single.Parse(txtPProiWidth.Text)) * scaleW;

                                re = new Rectangle((int)(mouse_dwnX), (int)(mouse_dwnY), (int)(w), (int)(h));
                            }
                        

                }
                if (bROI[1])
                {
                    if (!onload)
                    {

                        txtPosX2.Text = ((int)(mouse_dwnX / scaleW)).ToString();

                        txtPosY2.Text = ((int)(mouse_dwnY / scaleH)).ToString();
                        txtWidth2.Text = ((int)((w) / scaleW)).ToString();

                        txtHeight2.Text = ((int)((h) / scaleH)).ToString();
                        p.Color = Color.Blue;
                        re = new Rectangle((int)(mouse_dwnX), (int)(mouse_dwnY), (int)(w), (int)(h));
                    }
                    else
                    {
                        p.Color = Color.Blue;

                        mouse_dwnX = (Single.Parse(txtPosX2.Text) * scaleW);
                        mouse_dwnY = (Single.Parse(txtPosY2.Text) * scaleH);
                        h = (Single.Parse(txtHeight2.Text)) * scaleH;
                        w = (Single.Parse(txtWidth2.Text)) * scaleW;

                        re = new Rectangle((int)(mouse_dwnX), (int)(mouse_dwnY), (int)(w), (int)(h));
                    }
                }
                if (bROI[2])
                {
                    if (!onload)
                    {

                        txtPosX3.Text = ((int)(mouse_dwnX / scaleW)).ToString();

                        txtPosY3.Text = ((int)(mouse_dwnY / scaleH)).ToString();
                        txtWidth3.Text = ((int)((w) / scaleW)).ToString();

                        txtHeight3.Text = ((int)((h) / scaleH)).ToString();
                        p.Color = Color.Green;
                        re = new Rectangle((int)(mouse_dwnX), (int)(mouse_dwnY), (int)(w), (int)(h));
                    }
                    else
                    {
                        p.Color = Color.Green;

                        mouse_dwnX = (Single.Parse(txtPosX3.Text) * scaleW);
                        mouse_dwnY = (Single.Parse(txtPosY3.Text) * scaleH);
                        h = (Single.Parse(txtHeight3.Text)) * scaleH;
                        w = (Single.Parse(txtWidth3.Text)) * scaleW;

                        re = new Rectangle((int)(mouse_dwnX), (int)(mouse_dwnY), (int)(w), (int)(h));
                    }
                }
                p.Width = 1;
                
                e.Graphics.DrawRectangle(p, re);
                paint = false;
                
            }
            catch (System.Exception ex) { }
        }
       
        Double mouse_dwnX = 0;
        Double mouse_dwnY = 0;
        Double mouse_upX = 0;
        Double mouse_upY = 0;
        bool dragging = false;
        private void pictureBoxInspect_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            mouse_dwnX = e.X;
            mouse_dwnY = e.Y;
            dragging = true;
            onload = false;
            mousemove = true;

            scaleW = (Single)pictureBoxInspect.Width / (Single)pictureBoxInspect.Image.Width;
            scaleH = (Single)pictureBoxInspect.Height  / (Single)pictureBoxInspect.Image.Height;

            if (bROI[0])
            {
                txtPProiPosX.Text = ((int)(mouse_dwnX / scaleW)).ToString();
                txtPProiPosY.Text = ((int)((mouse_dwnY / scaleH))).ToString();
            }
            if (bROI[1])
            {
                txtPosX2.Text = ((int)(mouse_dwnX / scaleW)).ToString();
                txtPosY2.Text = ((int)((mouse_dwnY / scaleH))).ToString();
            }
            if (bROI[2])
            {
                txtPosX3.Text = ((int)(mouse_dwnX / scaleW)).ToString();
                txtPosY3.Text = ((int)((mouse_dwnY / scaleH))).ToString();
            }
            pictureBoxInspect.Refresh();
        }
        
        private void pictureBoxInspect_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            mouse_upX = e.X;
            mouse_upY = e.Y;
            dragging = false;
                scaleW = (Single)pictureBoxInspect.Width / (Single)pictureBoxInspect.Image.Width;
                scaleH = (Single)pictureBoxInspect.Height / (Single)pictureBoxInspect.Image.Height;
            
            if (bROI[0])
            {
                txtPProiWidth.Text = ((int)((w) / scaleW)).ToString();
                txtPProiHeight.Text = ((int)((h ) / scaleH)).ToString();
            }
            else if (bROI[1])
            {
                txtWidth2.Text = ((int)((w) / scaleW)).ToString();
                txtHeight2.Text = ((int)((h) / scaleH)).ToString();
            }
            else if (bROI[2])
            {
                txtWidth3.Text = ((int)((w) / scaleW)).ToString();
                txtHeight3.Text = ((int)((h) / scaleH)).ToString();
            }
            pictureBoxInspect.Refresh();
        }
        Double w = 0;
        Double h = 0;
        private void pictureBoxInspect_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            try
            {
                if (!dragging) return;
                if (e.Button == MouseButtons.Left)
                {
                    w = (Single)e.X - (Single)mouse_dwnX;
                    h = (Single)e.Y - (Single)mouse_dwnY;
                    scaleW = (Single)pictureBoxInspect.Width / (Single)pictureBoxInspect.Image.Width;
                    scaleH = (Single)pictureBoxInspect.Height / (Single)pictureBoxInspect.Image.Height;
                    if (bROI[0])
                    {
                        
                        txtPProiWidth.Text = ((int)((w) / scaleW)).ToString();
                        //txtPProiHeight.Text = ((int)((w) / scaleW)).ToString();
                        txtPProiHeight.Text = ((int)((e.Y - mouse_dwnY) / scaleH)).ToString();
                    }
                    else if (bROI[1])
                    {
                        
                        txtWidth2.Text = ((int)((w) / scaleW)).ToString();
                        txtHeight2.Text = ((int)((e.Y - mouse_dwnY) / scaleH)).ToString();
                    }
                    else if (bROI[2])
                    {
                        
                        txtWidth3.Text = ((int)((w) / scaleW)).ToString();
                        //txtHeight3.Text = ((int)((w) / scaleW)).ToString();
                        txtHeight3.Text = ((int)((e.Y - mouse_dwnY) / scaleH)).ToString();
                    }
                    pictureBoxInspect.Refresh();
                }
            }
            catch (System.Exception ex) { }
        }
        bool[] bROI = new bool[3];
        private void button2_Click(object sender, EventArgs e)
        {
            if (((System.Windows.Forms.Button)sender).Name == "button2") { bROI[0] = true; bROI[1] = false; bROI[2] = false; }
            else if (((System.Windows.Forms.Button)sender).Name == "button3") { bROI[0] = false; bROI[1] = true; bROI[2] = false; }
            else if (((System.Windows.Forms.Button)sender).Name == "button4") { bROI[0] = false; bROI[1] = false; bROI[2] = true; }
            paint = true;
            onload = true;
            //scale();
            pictureBoxInspect.Refresh();
        }

        public void btnSaveROI_Click(object sender, EventArgs e)
        {
            SaveROI(sender,e);
            SaveROI1(sender, e);

        }

        public void SaveROI(object sender, EventArgs e)
        {
            EndData.roiPosX1 = int.Parse(txtPProiPosX.Text);
            EndData.roiPosY1 = int.Parse(txtPProiPosY.Text);
            EndData.roiWidth1 = int.Parse(txtPProiWidth.Text);
            EndData.roiHeight1 = int.Parse(txtPProiHeight.Text);
            EndData.Roi1 = chkROI1.Checked.ToString().ToLower();
            EndData.IsFractions1 = chkFractions1.Checked.ToString().ToLower();
            EndData.IsPeels1 = chkPeels1.Checked.ToString().ToLower();
            

            EndData.roiPosX2 = int.Parse(txtPosX2.Text);
            EndData.roiPosY2 = int.Parse(txtPosY2.Text);
            EndData.roiWidth2 = int.Parse(txtWidth2.Text);
            EndData.roiHeight2 = int.Parse(txtHeight2.Text);
            EndData.Roi2 = chkROI2.Checked.ToString().ToLower();
            EndData.IsFractions2 = chkFractions2.Checked.ToString().ToLower();
            EndData.IsPeels2 = chkPeels2.Checked.ToString().ToLower();

            EndData.roiPosX3 = int.Parse(txtPosX3.Text);
            EndData.roiPosY3 = int.Parse(txtPosY3.Text);
            EndData.roiWidth3 = int.Parse(txtWidth3.Text);
            EndData.roiHeight3 = int.Parse(txtHeight3.Text);
            EndData.Roi3 = chkROI3.Checked.ToString().ToLower();
            EndData.IsFractions3 = chkFractions3.Checked.ToString().ToLower();
            EndData.IsPeels3 = chkPeels3.Checked.ToString().ToLower();

            EndData.PeelLowerThreshold = Single.Parse(txtPeelLower.Text);
            EndData.PeelUpperThreshold = Single.Parse(txtPeelUpper.Text);
            EndData.FracLowerThreshold = Single.Parse(txtFractionLower.Text);
            EndData.FracUpperThreshold = Single.Parse(txtFractionUpper.Text);

            EndData.PeelLowerArea = Single.Parse(txtPeelLowerArea.Text);
            EndData.PeelScore = Single.Parse(txtPeelScore.Text);
            EndData.FracLowerArea = Single.Parse(txtFractionLowerArea.Text);
            EndData.FracScore = Single.Parse(txtFractionScore.Text);

            //NPNP
            EndData.iNumberOfTopImages = int.Parse(txtNumberOfTopImages.Text);
            onExposureChangedFromBeckofForm(this, myCustomEventArgIntRef);
            EndData.iExposureTimeDefectInspection = myCustomEventArgIntRef.Value;

            JassonDataClass.setNewValue(EndData, false);

            ClearControlsChanged();
               
        }
        //public void SaveROI(string filename)
        //{

        //    if (filename == "") filename = frmMain.JassonPath;
        //    EndData.roiPosX1 = int.Parse(txtPProiPosX.Text);
        //    EndData.roiPosY1 = int.Parse(txtPProiPosY.Text);
        //    EndData.roiWidth1 = int.Parse(txtPProiWidth.Text);
        //    EndData.roiHeight1 = int.Parse(txtPProiHeight.Text);
        //    EndData.Roi1 = chkROI1.Checked.ToString().ToLower();
        //    EndData.IsFractions1 = chkFractions1.Checked.ToString().ToLower();
        //    EndData.IsPeels1 = chkPeels1.Checked.ToString().ToLower();


        //    EndData.roiPosX2 = int.Parse(txtPosX2.Text);
        //    EndData.roiPosY2 = int.Parse(txtPosY2.Text);
        //    EndData.roiWidth2 = int.Parse(txtWidth2.Text);
        //    EndData.roiHeight2 = int.Parse(txtHeight2.Text);
        //    EndData.Roi2 = chkROI2.Checked.ToString().ToLower();
        //    EndData.IsFractions2 = chkFractions2.Checked.ToString().ToLower();
        //    EndData.IsPeels2 = chkPeels2.Checked.ToString().ToLower();

        //    EndData.roiPosX3 = int.Parse(txtPosX3.Text);
        //    EndData.roiPosY3 = int.Parse(txtPosY3.Text);
        //    EndData.roiWidth3 = int.Parse(txtWidth3.Text);
        //    EndData.roiHeight3 = int.Parse(txtHeight3.Text);
        //    EndData.Roi3 = chkROI3.Checked.ToString().ToLower();
        //    EndData.IsFractions3 = chkFractions3.Checked.ToString().ToLower();
        //    EndData.IsPeels3 = chkPeels3.Checked.ToString().ToLower();

        //    EndData.PeelLowerThreshold = Single.Parse(txtPeelLower.Text);
        //    EndData.PeelUpperThreshold = Single.Parse(txtPeelUpper.Text);
        //    EndData.FracLowerThreshold = Single.Parse(txtFractionLower.Text);
        //    EndData.FracUpperThreshold = Single.Parse(txtFractionUpper.Text);

        //    EndData.PeelLowerArea = Single.Parse(txtPeelLowerArea.Text);
        //    EndData.PeelScore = Single.Parse(txtPeelScore.Text);
        //    EndData.FracLowerArea = Single.Parse(txtFractionLowerArea.Text);
        //    EndData.FracScore = Single.Parse(txtFractionScore.Text);

        //    JassonDataClass.setNewValue(EndData, false);

        //}
        void GPU()
        {
            try
            {

                // (1) 
                //
                // To maximize throughput all tools will use only one GPU. We can then use a hardware concurrency
                // equal to the number of GPUs.
                {
                    //("Example maximizing throughput");
                    //listBox1.Items.Clear();
                    inv.settxt(txtListBox1, "");
                    lstStr = "";
                    //listBox1.Items.Add("Example maximizing throughput");


                    List<int> GPUList = new List<int>();
                    // We could instead specify which gpu to use by initializing with :
                    // List<int> GPUList = new List<int>(){0,1}; 
                    // to use only first and second GPUs

                    // Initialize a control

                    // Initialilizes the Compute devices
                    // Parameters : - GPUMode.SingleDevicePerTool each tool will use a single GPU -> Maximizing throughput
                    //              - new GPUList : automatically resolve all available gpus if empty
                    //using (ViDi2.Runtime.IControl control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList))
                    control.Dispose();    
                    control = new ViDi2.Runtime.Local.Control(GpuMode.Deferred, GPUList);
                        control.InitializeComputeDevices(GpuMode.SingleDevicePerTool, GPUList);

                        var computeDevices = control.ComputeDevices;

                        // the example will run with fewer than 2 GPUs, but the results might not be meaningful
                        if (computeDevices.Count < 2)
                        {
                            //listBox1.Items.Add("Warning ! Example needs at least two GPUs to be meaningfull");
                        }

                        Console.WriteLine("Available computing devices :");
                        foreach (var computeDevice in computeDevices)
                        {
                            //listBox1.Items.Add(computeDevice.Name + " : memory " + computeDevice.Memory.ToString());
                        }

                        // opens a runtime workspace from file
                        string WorkspaceFile = "..\\..\\..\\..\\resources\\runtime\\Textile.vrws";

                        if (!File.Exists(WorkspaceFile))
                        {
                            // if you got here then it's likely that the resources were not extracted in the path
                            //listBox1.Items.Add(WorkspaceFile + " does not exist");
                            //listBox1.Items.Add("Current Directory =" + Directory.GetCurrentDirectory());
                            return;
                        }

                        var workspace = control.Workspaces.Add("workspace1", "..\\..\\..\\..\\resources\\runtime\\Textile.vrws");

                        // store a reference to the stream 'default'
                        var stream = workspace.Streams["default"];

                        String ImageName = "..\\..\\..\\..\\resources\\images\\000000.png";
                        if (!File.Exists(ImageName))
                        {
                            //listBox1.Items.Add(ImageName + " does not exist");
                            return;
                        }
                        // load an image from file
                        IImage img = new ViDi2.Local.LibraryImage(ImageName);

                        // warm up operation, first call to process takes additionnal time
                        stream.Process(img);

                        // Action processing 10 images, DeviceId defines which device to use
                        Action<int> ProcessAction = new Action<int>((int DeviceId) =>
                        {
                            Console.WriteLine($"processing on device {DeviceId}");
                            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                            sw.Start();
                            for (var iteration = 0; iteration < 10; ++iteration)
                            {
                                using (ISample sample = stream.CreateSample(img))
                                {
                                    // process all tools
                                    sample.Process(null, new List<int>() { DeviceId });

                                    double totalDurationMs = 0;
                                    // Iterating over all the markings to get VPDL Processing time
                                    foreach (var marking in sample.Markings)
                                    {
                                        totalDurationMs += marking.Value.Duration;
                                    }
                                    //listBox1.Items.Add("image processed on device " + DeviceId.ToString() + " in" + totalDurationMs.ToString() + " ms");
                                }
                            }
                            //listBox1.Items.Add("10 images processed on device" + DeviceId.ToString() + " in" + sw.ElapsedMilliseconds.ToString() + " ms");
                        });

                        var tasks = new List<Task>();

                        System.Diagnostics.Stopwatch globalSw = new System.Diagnostics.Stopwatch();


                        //listBox1.Items.Add("Will now process 10*" + computeDevices.Count.ToString() + " images with " + computeDevices.Count.ToString() + " threads");

                        globalSw.Start();
                        // We will launch as many concurrent thread as there are devices available
                        for (int k = 0; k < computeDevices.Count; ++k)
                        {
                            int DeviceId = k;
                            tasks.Add(Task.Factory.StartNew(() => ProcessAction(DeviceId)));
                        }
                        // wait for all tasks to finish
                        Task.WaitAll(tasks.ToArray());

                        //listBox1.Items.Add("Processed " + computeDevices.Count.ToString() + "*10 images in " + globalSw.ElapsedMilliseconds.ToString() + " ms");
                        //listBox1.Items.Add("----------------------------------------------------------");
                        //listBox1.Items.Add("-----------Reference Measure using only one GPU-----------");
                        //listBox1.Items.Add("----------------------------------------------------------");
                        //listBox1.Items.Add("Will now process 10 images on device 0 with a single thread");

                        // Processes all images using only one thread to get a measure of processing time without overhead of using
                        //  multiple threads
                        ProcessAction(0);
                }
                

            }
            catch (System.Exception ex) { }

            return;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            GPU();
        }
        //---------------------- front ------------------------
        public WpfImage imageFront;
        bool[] bROI1 = new bool[3];
        public async Task<bool> InspectionCycleFront()
        {
            Stopwatch sw = new Stopwatch();
            Stopwatch sw1 = new Stopwatch();
            bool rep = false;

            try
            {
                bDefectFoundInFrontInspection = false;
                sw.Restart();
                this.Invoke((Action)(() => { listBox11.Items.Clear(); }));
                lstStr = "";
                this.Invoke((Action)(() => { listBox11.Items.Add("----------Start Inspect Cycle" + " ------------- //" + DateTime.Now.ToString("HH:mm:ss.fff")); }));
                StopCycle = false;
                int framemax = 0;
                //imageCle = new ViDi2.UI.WpfImage[1];
                ImageFront();
                //for (int i = 0; i < 1; i++)
                //{

                this.Invoke((Action)(() => { listBox11.Items.Add("--Wait Inspection Front"  + "-- //" + DateTime.Now.ToString("HH:mm:ss.fff")); }));
                   
                    while (imageFront == null && !StopCycle) { Thread.Sleep(50); if (StopCycle) return false; }
                    if (StopCycle) return false;
                //
                EndData.ImagePathFront = FrontPath + @"\Images\snap-inspect.jpg";// @"C:\Project\Cam2\Cam2BaslerML\Cam2BaslerML\bin\Debug\Images\snap-inspect.jpg";
                this.Invoke((Action)(() => { UpdateTexts1(EndData); }));
                
                if (chkShowImage1.Checked)
                {
                    using (var file = new FileStream(EndData.ImagePathFront, FileMode.Open, FileAccess.Read, FileShare.Inheritable))
                    {
                        onload = true;
                        pictureBoxInspect1.Image = (Bitmap)Bitmap.FromStream(file); // Image.FromFile(EndData.ImagePath);
                        file.Close();

                    }
                }
                //


                sw1.Restart();

                    var task1 = Task.Run(() => startEvalFractionsFront(/*EndData.ImagePathFront*/));
                    await task1;
                    //var task2 = Task.Run(() => startEvalPeelsFront(EndData.ImagePathFront));
                    //await task2;
                    if (!task1.Result) return false;
                    //if (!task2.Result) return false;
                    inv.settxt(lblDuration1, (sw1.ElapsedMilliseconds / 1000.0).ToString("0.000"));
                    inv.settxt(lblCycleTime1, (sw.ElapsedMilliseconds / 1000.0f).ToString("0.0"));

                //}

                this.Invoke((Action)(() => { listBox11.Items.Add("----------Fini ALL--------------- " + " //" + DateTime.Now.ToString("HH:mm:ss.fff")); }));
                sw.Stop();
                inv.settxt(lblCycleTime1, (sw.ElapsedMilliseconds / 1000.0f).ToString("0.0"));
                rep = true;
                return rep;
            }
            catch (System.Exception ex) { return false; }


        }
        private bool startEvalFractionsFront(/*string imagefile*/)
        {

            try
            {


                WpfImage image = null;
                if (imageFront == null)
                {
                    return false;
                }
                image = imageFront;


                Rectangle ImageDimensions = new Rectangle(0, 0, image.Width, image.Height);

                int Iindex = 0;
                resArr.resIndex = 0;
                resArr.resInfo.ShowRes = new string[20];
                int index = 0;


                //add if setup-need to chec frac or peels
                System.Windows.Rect ROIrect = new System.Windows.Rect();
                // RIO 1
                //if (EndData.IsFractionsFront == "true" && EndData.RoiFront == "true") 
                //{
                    //if (!bRunPrepareOnLoad)
                    //{
                    //NPNP
                        //var task1 = Task.Run(() => PrepairEvalFront(image, true));
                        //await task1;
                        //if (!task1.Result)
                        //{
                        //    StopCycle = true;
                        //    System.Windows.Forms.MessageBox.Show("Error Breaks Front Evaluation1! ", "ERROR", MessageBoxButtons.OK,
                        //    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
                        //}

                    //}

                //}


                if (EndData.RoiFront == "true" || chkCutImage1.Checked)
                {
                    //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
                    ROIrect.X = EndData.roiPosXfront;
                    ROIrect.Y = EndData.roiPosYfront;
                    ROIrect.Width = EndData.roiWidthfront;
                    ROIrect.Height = EndData.roiHeightfront;
                    //RegIndex = 0;
                    //RegIndex2 = 0;
                    RegIndexFront = 0;
                    if (EndData.IsFractionsFront == "true")
                    {
                        //this.Invoke((Action)(() => { listBox11.Items.Add("BREAKfront"); }));

                        //NPNP
                        string sFilter = "area>= " + txtFractionLowerArea1.Text;// + " and score>=" + txtFractionScore.Text.Trim();
                        var hdRedParamsBreakeFrontROI = (ViDi2.IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model1NameFront].Tools.First().RegionOfInterest;
                        //ViDi2.IManualRegionOfInterest hdRedParamsBreakeFront = (ViDi2.IManualRegionOfInterest).FrontTool.RegionOfInterest;
                        //ApplyROIRectFrontFractions(ImageDimensions, hdRedParamsBreakeFrontROI);
                        ViDi2.Runtime.IRedHighDetailParameters hdRedParamsBreakeFront = grunTimeWorkapace.StreamDict[Model1NameFront].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
                        SetThreshold(hdRedParamsBreakeFront, EndData.FracLowerThresholdFront, 1);

                        ApplyROIRect(true, ImageDimensions, chkAutoROI.Checked, ROIrect, sampFront, 1,
                            (IManualRegionOfInterest)grunTimeWorkapace.StreamDict[Model1NameFront].Tools.First().RegionOfInterest);


                        hdRedParamsBreakeFront.RegionFilter = sFilter;

                        //NPNP
                        //ApplyROIRectFront(true, ImageDimensions, chkAutoROI1.Checked, ROIrect, sampFront, 1);
                        //sampFront.Process();
                        sampFront = grunTimeWorkapace.StreamDict[Model1NameFront].Process(image);

                        getRagionFront(sampFront, Model1NameFront, index, Iindex, regionFoundFront, 0, 1);

                        //add founds to list.
                    }

                   
                }

               

               
                return true;
            }
            catch (System.Exception ex) { return false; }


        }



        private void AddEndmill(string sCatalogueItemName, string sImagePath/*, string sImagePathFront*/)
        {
            try
            {

                //NPNP make sure snaping while setting up new catalogue numbers is taken from here
                string ImagePath = AppDomain.CurrentDomain.BaseDirectory;
                string ImagePathImagePathDestination = "";
                if(bIsOperatorMode)
                    ImagePathImagePathDestination = ImagePath + "\\Data\\" + sCatalogueItemName + ".jpg";
                else
                    ImagePathImagePathDestination = ImagePath + "\\Data\\" + sCatalogueItemName + ".jpg";

                //NPNP make sure while in setup, snapshots are copied here, with this name
                File.Copy(ImagePath + @"Images\snap1.jpg", ImagePathImagePathDestination);

                var newEndmill = new Endmill
                {
                    // Basic info (assuming name & identifiers are here)
                    EndmillName = sCatalogueItemName, //"New Edmil " + (new Random()).Next(10000, 99999).ToString(), //txtEndmillName.Text,
                                                      //CatalogNo = int.Parse(txtCatalogNo.Text),
                                                      //BladesNo = int.Parse(txtBladesNo.Text),
                                                      //Diameter = int.Parse(txtDiameter.Text),
                                                      //Length = int.Parse(txtLength.Text),

                    // Thresholds
                    FracLowerThreshold = float.Parse(txtFractionLower.Text),
                    FracUpperThreshold = float.Parse(txtFractionUpper.Text),

                    // ROI 1 parameters
                    roiPosX1 = int.Parse(txtPProiPosX.Text),
                    roiPosY1 = int.Parse(txtPProiPosY.Text),
                    roiWidth1 = int.Parse(txtPProiWidth.Text),
                    roiHeight1 = int.Parse(txtPProiHeight.Text),
                    roiAngle1 = float.Parse(txtROIangle.Text),
                    // roiRatio1 = float.Parse(txtRatioImage2ROI.Text), // if needed

                    // ROI 2 parameters
                    roiPosX2 = int.Parse(txtPosX2.Text),
                    roiPosY2 = int.Parse(txtPosY2.Text),
                    roiWidth2 = int.Parse(txtWidth2.Text),
                    roiHeight2 = int.Parse(txtHeight2.Text),
                    roiAngle2 = float.Parse(txtAngle2.Text),
                    // roiRatio2 = float.Parse(txtRatio2.Text),

                    // ROI 3 parameters
                    roiPosX3 = int.Parse(txtPosX3.Text),
                    roiPosY3 = int.Parse(txtPosY3.Text),
                    roiWidth3 = int.Parse(txtWidth3.Text),
                    roiHeight3 = int.Parse(txtHeight3.Text),
                    roiAngle3 = float.Parse(txtAngle3.Text),
                    // roiRatio3= float.Parse(txtRatio3.Text),
                    ImagePath = ImagePathImagePathDestination/*sImagePath*/,
                    ImagePathFront = "Not Relevant" /*sImagePathFront*/,

                    // Thresholds for the main thresholds
                    PeelLowerThreshold = float.Parse(txtPeelLower.Text),
                    PeelUpperThreshold = float.Parse(txtPeelUpper.Text),
                    FracLowerArea = float.Parse(txtFractionLowerArea.Text),
                    //NPNP
                    //FracUpperArea = float.Parse(txtFractionUpperArea.Text),
                    PeelLowerArea = float.Parse(txtPeelLowerArea.Text),
                    //PeelUpperArea = float.Parse(txtPeelUpperArea.Text),

                    // Front ROI info
                    RoiFront = (chkROI11.Checked ? "true" : "false"),
                    roiPosXfront = int.Parse(txtPProiPosX.Text), // Using the same control? Or a separate one?
                    roiPosYfront = int.Parse(txtPProiPosY.Text),
                    roiWidthfront = int.Parse(txtPProiWidth.Text),
                    roiHeightfront = int.Parse(txtPProiHeight.Text),

                    // Boolean string settings
                    IsFractions1 = (chkFractions1.Checked ? "true" : "false"),
                    IsPeels1 = (chkPeels1.Checked ? "true" : "false"),
                    IsFractions2 = (chkFractions2.Checked ? "true" : "false"),
                    IsPeels2 = (chkPeels2.Checked ? "true" : "false"),
                    IsFractions3 = (chkFractions3.Checked ? "true" : "false"),
                    IsPeels3 = (chkPeels3.Checked ? "true" : "false"),
                    IsFractionsFront = (chkFractions11.Checked ? "true" : "false"),
                    iNumberOfTopImages = int.Parse(txtNumberOfTopImages.Text)//,

                    //IsPeelsFront = txtIsPeelsFront.Text
                };

                endmills.Add(newEndmill);
                SaveEndmills();
                RefreshComboBox();
                CmbCatNum.SelectedItem = newEndmill.EndmillName;
                CmbCatNum1.SelectedItem = newEndmill.EndmillName;
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Error adding a new catalogue number: {ex.Message}");
            }
        }
        // Save to JSON
        public void SaveEndmills()
        {
            try
            {
                string json = JsonConvert.SerializeObject(endmills, Formatting.Indented);
                File.WriteAllText(JassonPath, json);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving: {ex.Message}");
            }
        }


        public int CalcHighestCroppingTop(int iToletance)
        {
            //calculate the top border position of the cropping area that includes ALL geographical ROI + a tolerance
            int iHighestCroppingTop = EndData.roiPosY1;
            iHighestCroppingTop = (EndData.roiPosY2 < iHighestCroppingTop ? EndData.roiPosY2 : iHighestCroppingTop);
            iHighestCroppingTop = (EndData.roiPosY3 < iHighestCroppingTop ? EndData.roiPosY3 : iHighestCroppingTop);
            return iHighestCroppingTop - iToletance;
        }

        public int CalcRightmostCroppingRight(int iToletance)
        {
            //calculate the right border position of the cropping area that includes ALL geographical ROI + a tolerance
            int RightmostCroppingRight = EndData.roiPosX1 + EndData.roiWidth1;
            RightmostCroppingRight = (EndData.roiPosX2 + EndData.roiWidth2 > RightmostCroppingRight ? EndData.roiPosX2 + EndData.roiWidth2 : RightmostCroppingRight);
            RightmostCroppingRight = (EndData.roiPosX3 + EndData.roiWidth3 > RightmostCroppingRight ? EndData.roiPosX3 + EndData.roiWidth3 : RightmostCroppingRight);
            return RightmostCroppingRight + iToletance;
        }


        // Reload ComboBox
        public void RefreshComboBox()
        {
            CmbCatNum.DataSource = null;
            if (endmills != null && endmills.Any())
            {
                CmbCatNum.DataSource = endmills.Select(e => e.EndmillName).ToList();
            }
        }



        //private async Task<bool> startEvalPeelsFront(string imagefile)
        //{


        //    try
        //    {

        //        //string iniFileName = CmbCatNumText + ".ini";
        //        //string iniFilePath = sDirpath + @"Data\DataBase\" + iniFileName;
        //        //List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();
        //        //ViDi2.Runtime.IRedHighDetailParameters hdRedParamPeels = grunTimeWorkapace.StreamDict[Model2Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
        //        //ViDi2.Runtime.IRedHighDetailParameters hdRedParamsBreake = grunTimeWorkapace.StreamDict[Model1Name].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;
        //        System.Windows.Rect ROIrect = new System.Windows.Rect();

        //        WpfImage image = null;
        //        if (imageFront == null)
        //        {
        //            return false;
        //        }
        //        image = imageFront;


        //        Rectangle ImageDimensions = new Rectangle(0, 0, image.Width, image.Height);
                

        //        int index2 = 0;
        //        int Iindex = 0;
        //        resArr.resIndex = 0;
        //        resArr.resInfo.ShowRes = new string[20];
                
        //        //add if setup-need to chec frac or peels
        //        // RIO 1

        //        if (EndData.IsPeelsFront == "true" && EndData.RoiFront == "true") 
        //        {

        //            var task1 = Task.Run(() => PrepairEvalFront(image, false));
        //            await task1;
        //            if (!task1.Result)
        //            {
        //                StopCycle = true;
        //                System.Windows.Forms.MessageBox.Show("Error Peels Evaluation1! ", "ERROR", MessageBoxButtons.OK,
        //                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
        //            }
        //            //PrepairEvalPl = true;
        //        }

        //        if (EndData.RoiFront == "true" || chkCutImage1.Checked)
        //        {
        //            //this.Invoke((Action)(() => { listBox1.Items.Add("start test1 " + (sw.ElapsedMilliseconds / 1000.0)).ToString("0.00"); }));
        //            ROIrect.X = EndData.roiPosXfront;
        //            ROIrect.Y = EndData.roiPosYfront;
        //            ROIrect.Width = EndData.roiWidthfront;
        //            ROIrect.Height = EndData.roiHeightfront;
        //            RegIndex = 0;
        //            RegIndex2 = 0;
        //            if (EndData.IsPeelsFront == "true")
        //            {
        //                //this.Invoke((Action)(() => { listBox11.Items.Add("PEELSfront"); }));
        //                ApplyROIRectFront(true, ImageDimensions, chkAutoROI1.Checked, ROIrect, samp2, 1);
        //                samp2.Process();

        //                getRagionFront(samp2, Model2NameFront, index2, Iindex, regionFound2, 0, 1);
        //                //add founds to list.
        //            }
                   
        //        }

        //        return true;
        //    }
        //    catch (System.Exception ex) { return false; }


        //}
        public void ImageFront()
        {
            try
            {
                string imagefile = "";// SnapFile[i];
                if (imagefile == "") imagefile = EndData.ImagePathFront;
                //string fnamecopy = "";
                //fnamecopy = imagefile.Insert(imagefile.IndexOf("snap"), "COPY\\");
                //File.Copy(imagefile, fnamecopy, true);
                imageFront = new ViDi2.UI.WpfImage(imagefile);
            }
            catch (System.Exception ex) { }
        }
        public bool PrepairEvalFront(WpfImage image, bool isFract)
        {

            try
            {
                List<Dictionary<string, IMarking>> lstIMarking = new List<Dictionary<string, IMarking>>();
                ViDi2.Runtime.IRedHighDetailParameters hdRedParamsBreake = grunTimeWorkapace.StreamDict[Model1NameFront].Tools.First().ParametersBase as ViDi2.Runtime.IRedHighDetailParameters;

                //NPNP
                SetThreshold(hdRedParamsBreake, EndData.FracLowerThresholdFront, 1);
                //if (isFract)
                //{

                sampFront = grunTimeWorkapace.StreamDict[Model1NameFront].Process(image);
                Dictionary<string, IMarking> mark = sampFront.Markings;
                Dictionary<string, IMarking>.KeyCollection MarkKey = mark.Keys;
                IMarking TryM = mark["Analyze"];
                ViDi2.IView View = TryM.Views[0];// mm.Marking.Views[0];
                ViDi2.IRedView redview = (ViDi2.IRedView)View;
                regionFoundFront = new RegionFound[redview.Regions.Count];
                ViDi2.Runtime.IRedTool tool = (ViDi2.Runtime.IRedTool)grunTimeWorkapace.StreamDict[Model1NameFront].Tools.First();
                

                return true;
            }
            catch (System.Exception ex) { return false; }
        }
        
        private void CmbCatNum1_SelectedIndexChanged(object sender, EventArgs e)
        {
            CmbCatNumText1 = CmbCatNum1.Text;
            return;
        }

        private void CmbCatNum1_SelectedValueChanged(object sender, EventArgs e)
        {
            try
            {
                bIsCatalogueNumberChanging = true;

                CmbCatNumText1 = CmbCatNum1.Text;
                if (CmbCatNum1.Text != "")
                {

                    EndData = JassonDataClass.getJassonParameters(CmbCatNum1.Text);
                    EndData.ImagePathFront = FrontPath + @"\Images\snap-inspect.jpg";// @"C:\Project\Cam2\Cam2BaslerML\Cam2BaslerML\bin\Debug\Images\snap-inspect.jpg";
                    UpdateTexts1(EndData);
                    if (chkShowImage1.Checked)
                    {
                        using (var file = new FileStream(EndData.ImagePathFront, FileMode.Open, FileAccess.Read, FileShare.Inheritable))
                        {
                            onload = true;
                            pictureBoxInspect1.Image = (Bitmap)Bitmap.FromStream(file); // Image.FromFile(EndData.ImagePath);
                            file.Close();

                        }
                    }
                    btnStartEval1.Enabled = true;
                }
                else btnStartEval1.Enabled = false;

                bIsCatalogueNumberChanging = false;

            }
            catch (System.Exception ex) { btnStartEval1.Enabled = true; };
        }
        private void UpdateTexts1(Endmill endmill)
        {

            
            if (endmill != null)
            {
                txtPProiPosX1.Text = endmill.roiPosXfront.ToString();
                txtPProiPosY1.Text = endmill.roiPosYfront.ToString();
                txtPProiWidth1.Text = endmill.roiWidthfront.ToString();
                txtPProiHeight1.Text = endmill.roiHeightfront.ToString();
                //txtROIangle.Text = endmill.roiAngle1.ToString();
                //txtRatioImage2ROI.Text = endmill.roiRatio1.ToString();

                if (endmill.IsFractionsFront == "true")
                {
                    chkFractions11.Checked = true;
                }
                else chkFractions11.Checked = false;
                if (endmill.RoiFront == "true")
                {
                    chkROI11.Checked = true;
                }
                else chkROI11.Checked = false;
               




                txtFractionLower1.Text = endmill.FracLowerThresholdFront.ToString();
                txtFractionUpper1.Text = endmill.FracUpperThresholdFront.ToString();

                

                txtFractionLowerArea1.Text = endmill.FracLowerAreaFront.ToString();
                //txtFractionUpperArea1.Text = endmill.FracUpperAreaFront.ToString();
                
            }
        }
        private void LoadEndmills1()
        {
            //C:\Project\4.2.2025\InspectSolution\setUpApplication\projSampaleViewer\bin\x64\Debug\Data\DataBase\EndmillsData.Jason";
            string filePath = JassonPath;// @"C:\Users\inspmachha\Desktop\setUpApplication - Copy\projSampaleViewer\bin\x64\Debug\Data\DataBase\EndmillsData.Jason"; // Set the path to your JSON file

            try
            {
                string json = File.ReadAllText(filePath); // Read the JSON data from the file
                //NPNP
                //endmills = JsonConvert.DeserializeObject<List<EndmillData>>(json); // Deserialize JSON data into the endmills list
                endmills = JsonConvert.DeserializeObject<List<BeckhoffBasler.Endmill>>(json); // Deserialize JSON data into the endmills list
                if (endmills == null)
                {
                    System.Windows.Forms.MessageBox.Show("Failed to load endmill data. Please check the JSON format.");
                    return;
                }
                CmbCatNum1.Items.AddRange(endmills.Select(e => e.EndmillName).ToArray()); // Populate ComboBox with endmill names
                CmbCatNumText1 = CmbCatNum1.Text;
            }
            catch (System.Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"An error occurred while loading endmill data: {ex.Message}");
            }
        }
        bool paint1 = false;
        public Double scaleH1 = 0;
        public Double scaleW1 = 0;
        bool onload1 = false;
        private void button21_Click(object sender, EventArgs e)
        {
            if (((System.Windows.Forms.Button)sender).Name == "button21") { bROI1[0] = true;  }
            
            paint1 = true;
            onload1 = true;
            //scale();
            pictureBoxInspect1.Refresh();
        }

        private void btnSaveROI1_Click(object sender, EventArgs e)
        {
            SaveROI(sender, e);
            SaveROI1(sender, e);
        }

        public void SaveROI1(object sender, EventArgs e)
        {
            EndData.roiPosXfront = int.Parse(txtPProiPosX1.Text);
            EndData.roiPosYfront = int.Parse(txtPProiPosY1.Text);
            EndData.roiWidthfront = int.Parse(txtPProiWidth1.Text);
            EndData.roiHeightfront = int.Parse(txtPProiHeight1.Text);
            EndData.RoiFront = chkROI11.Checked.ToString().ToLower();
            EndData.IsFractionsFront = chkFractions11.Checked.ToString().ToLower();
            
            EndData.FracLowerThresholdFront = Single.Parse(txtFractionLower1.Text);
            EndData.FracUpperThresholdFront = Single.Parse(txtFractionUpper1.Text);

            EndData.FracLowerAreaFront = Single.Parse(txtFractionLowerArea1.Text);
            //EndData1.FracUpperAreaFront = Single.Parse(txtFractionUpperArea1.Text);

            JassonDataClass.setNewValue(EndData, false);

            ClearControlsChanged();
        }

        private void btnShowROI1_Click(object sender, EventArgs e)
        {
            if (!chkShowImage1.Checked) return;
            paint1 = true;
            onload1 = true;

            pictureBoxInspect1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxInspect1.Refresh();
        }
        Double mouse_dwnX1 = 0;
        Double mouse_dwnY1 = 0;
        Double mouse_upX1 = 0;
        Double mouse_upY1 = 0;
        bool dragging1 = false;
        Double w1 = 0;
        Double h1 = 0;
        private void pictureBoxInspect1_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBoxInspect1.Image == null) return;
            if (!chkShowImage.Checked) return;
            //return;
            //if (!paint) return;
            try
            {
                paint1 = false;
                Rectangle re = new Rectangle();
                Pen p = new Pen(Color.Red);
                Double y01 = 0;

                //pictureBoxInspect1.Height = (int)pictureBoxInspect1.Width * 3648 / 5472;
                //3648.0f / 5472.0f image for learning from C:\Project\4.2.2025\InspectSolution\setUpApplication\images
                pictureBoxInspect1.Height = (int)(pictureBoxInspect1.Width * 3032.0f / 5320.0f);
                pictureBoxInspect1.SizeMode = PictureBoxSizeMode.StretchImage;
                scaleW1 = (Single)pictureBoxInspect1.Width / (Single)pictureBoxInspect1.Image.Width;
                scaleH1 = (Single)pictureBoxInspect1.Height / (Single)pictureBoxInspect1.Image.Height;
                if (bROI1[0])
                {


                    if (!onload)
                    {
                        p.Color = Color.Red;
                        txtPProiPosX1.Text = ((int)(mouse_dwnX1 / scaleW1)).ToString();

                        txtPProiPosY1.Text = ((int)(mouse_dwnY1 / scaleH1)).ToString();
                        txtPProiWidth1.Text = ((int)((w1) / scaleW1)).ToString();

                        txtPProiHeight1.Text = ((int)((h1) / scaleH1)).ToString();
                        re = new Rectangle((int)(mouse_dwnX1), (int)(mouse_dwnY1), (int)(w1), (int)(h1));
                    }
                    else
                    {
                        p.Color = Color.Red;
                        mouse_dwnX1 = (Single.Parse(txtPProiPosX1.Text) * scaleW1);
                        mouse_dwnY1 = (Single.Parse(txtPProiPosY1.Text) * scaleH1);
                        h1 = (Single.Parse(txtPProiHeight1.Text)) * scaleH1;
                        w1 = (Single.Parse(txtPProiWidth1.Text)) * scaleW1;

                        re = new Rectangle((int)(mouse_dwnX1), (int)(mouse_dwnY1), (int)(w1), (int)(h1));
                    }


                }

                p.Width = 1;

                e.Graphics.DrawRectangle(p, re);
                paint1 = false;

            }
            catch (System.Exception ex) { }
        }

        private void pictureBoxInspect1_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            try
            {
                if (!dragging1) return;
                if (e.Button == MouseButtons.Left)
                {
                    w1 = (Single)e.X - (Single)mouse_dwnX1;
                    h1 = (Single)e.Y - (Single)mouse_dwnY1;
                    scaleW1 = (Single)pictureBoxInspect1.Width / (Single)pictureBoxInspect1.Image.Width;
                    scaleH1 = (Single)pictureBoxInspect1.Height / (Single)pictureBoxInspect1.Image.Height;
                    if (bROI1[0])
                    {

                        txtPProiWidth1.Text = ((int)((w1) / scaleW1)).ToString();
                        //txtPProiHeight1.Text = ((int)((w1) / scaleW1)).ToString();
                        txtPProiHeight1.Text = ((int)((e.Y - mouse_dwnY1) / scaleH1)).ToString();
                    }

                    pictureBoxInspect1.Refresh();
                }
            }
            catch (System.Exception ex) { }
        }

        private void pictureBoxInspect1_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            mouse_upX1 = e.X;
            mouse_upY1 = e.Y;
            dragging1 = false;
            scaleW1 = (Single)pictureBoxInspect1.Width / (Single)pictureBoxInspect1.Image.Width;
            scaleH1 = (Single)pictureBoxInspect1.Height / (Single)pictureBoxInspect1.Image.Height;

            if (bROI1[0])
            {
                txtPProiWidth1.Text = ((int)((w1) / scaleW1)).ToString();
                txtPProiHeight1.Text = ((int)((h1) / scaleH1)).ToString();
            }

            pictureBoxInspect1.Refresh();
        }

        private void pictureBoxInspect1_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            mouse_dwnX1 = e.X;
            mouse_dwnY1 = e.Y;
            dragging1 = true;
            onload1 = false;

            scaleW1 = (Single)pictureBoxInspect1.Width / (Single)pictureBoxInspect1.Image.Width;
            scaleH1 = (Single)pictureBoxInspect1.Height / (Single)pictureBoxInspect1.Image.Height;

            if (bROI1[0])
            {
                txtPProiPosX1.Text = ((int)(mouse_dwnX1 / scaleW1)).ToString();
                txtPProiPosY1.Text = ((int)((mouse_dwnY1 / scaleH1))).ToString();
                //txtPProiWidth1.Text = "0"; ;
                //txtPProiHeight1.Text = "0";
            }
            
            pictureBoxInspect1.Refresh();
        }
        public string[] RegionFoundFrontBrSave = new string[1];
        //public string[] RegionFoundFrontPlSave = new string[1];
        
        public int RegIndexFront = 0;
        //public int RegIndex2Front = 0;
        RegionFound[] BreakeRegionsFront;
        RegionFound[] PealRegionsFront;
        public void ShowRegionsFront1(ViDi2.IRedView redview, RegionFound[] regionFound2, int index2, string cn2, int Iindex, int CycleNum, int reg)
        {
            if (redview.Regions.Count != 0)
            {
                foreach (ViDi2.IRegion item in redview.Regions)
                {
                    regionFound2[index2].area = item.Area;

                    regionFound2[index2].width = item.Width;
                    regionFound2[index2].height = item.Height;
                    regionFound2[index2].center = item.Center;
                    regionFound2[index2].score = item.Score;
                    regionFound2[index2].className = cn2;  // item.Name; region name
                    regionFound2[index2].classColor = item.Color;
                    regionFound2[index2].compactness = item.Compactness;
                    regionFound2[index2].covers = item.Covers;
                    regionFound2[index2].outer = item.Outer;
                    regionFound2[index2].perimeter = item.Perimeter;
                    regionFound2[index2].X0 = item.Center.X;
                    regionFound2[index2].Y0 = item.Center.Y;
                    regionFound2[index2].H = item.Height;
                    regionFound2[index2].W = item.Width;
                    string[] res1 = new string[redview.Regions.Count];
                    res1[index2] = (CycleNum + 1).ToString("00") + " " 
                        //+ index2.ToString() + " "
                        + "Break:" + " Score=" + regionFound2[index2].score.ToString() + " " +
                        "Area=" + regionFound2[index2].area.ToString() + " " +
                        "Perimeter=" + regionFound2[index2].perimeter.ToString() + " " +
                        "Ourer=" + regionFound2[index2].outer.Count.ToString() + " " +
                         "X0=" + regionFound2[index2].X0.ToString() + " " +
                         "Y0=" + regionFound2[index2].Y0.ToString() + " " +
                          "H=" + regionFound2[index2].H.ToString() + " " +
                           "W=" + regionFound2[index2].W.ToString();
                    listBox11.Items.Add(res1[index2]);
                    
                    if (reg == 1)
                    {
                        RegionFoundFrontBrSave[RegionFoundFrontBrSave.Length - 1] = res1[index2];
                        Array.Resize<String>(ref RegionFoundFrontBrSave, RegionFoundFrontBrSave.Length + 1);
                    }
                   

                    //Array.Resize<String>(ref RegionFound1BrSave, RegionFound1BrSave.Length + 1);
                    Iindex++;
                    index2++;
                    RegIndexFront++;
                    bDefectFoundInFrontInspection = true;
                }
                PealRegionsFront = regionFound2;
            }
        }

        //public void ShowRegionsFront2(ViDi2.IRedView redview, RegionFound[] regionFound, int index, string cn, int Iindex, int CycleNum, int reg)
        //{
        //    if (redview.Regions.Count != 0)
        //    {
        //        foreach (ViDi2.IRegion item in redview.Regions)
        //        {
        //            regionFound[index].area = item.Area;
        //            regionFound[index].width = item.Width;
        //            regionFound[index].height = item.Height;
        //            regionFound[index].center = item.Center;
        //            regionFound[index].score = item.Score;
        //            regionFound[index].className = cn;  // item.Name; region name
        //            regionFound[index].classColor = item.Color;
        //            regionFound[index].compactness = item.Compactness;
        //            regionFound[index].covers = item.Covers;
        //            regionFound[index].outer = item.Outer;
        //            regionFound[index].perimeter = item.Perimeter;
        //            regionFound[index].X0 = item.Center.X;
        //            regionFound[index].Y0 = item.Center.Y;
        //            regionFound[index].H = item.Height;
        //            regionFound[index].W = item.Width;
        //            string[] res1 = new string[redview.Regions.Count];
        //            res1[index] = (CycleNum + 1).ToString("00") + " "
        //                //+ index.ToString() + " " 
        //                + "Peels: " + "Score=" + regionFound[index].score.ToString() + " " +
        //                "Area=" + regionFound[index].area.ToString() + " " +
        //                "Perimeter=" + regionFound[index].perimeter.ToString() + " " +
        //                "Ourer=" + regionFound[index].outer.Count.ToString() + " " +
        //                "X0=" + regionFound[index].X0.ToString() + " " +
        //                 " Y0=" + regionFound[index].Y0.ToString() + " " +
        //                  "H=" + regionFound[index].H.ToString() + " " +
        //                   "W=" + regionFound[index].W.ToString();
        //            listBox11.Items.Add(res1[index]);
        //            if (reg == 1) { RegionFoundFrontBrSave[RegionFoundFrontBrSave.Length - 1] = res1[index]; Array.Resize<String>(ref RegionFoundFrontBrSave, RegionFoundFrontBrSave.Length + 1); }
                    
        //            //RegionFound1PlSave[RegIndex] = res1[index];
        //            //Array.Resize<String>(ref RegionFound1PlSave, RegionFound1PlSave.Length + 1);
        //            Iindex++;
        //            index++;
        //            RegIndexFront++;
        //        }
        //        BreakeRegionsFront = regionFound;
        //    }
        //}
        public void getRagionFront(ISample samp, string modelName, int index, int Iindex, RegionFound[] regionFound, int CycleNum, int reg)
        {
            Dictionary<string, IMarking> mark = samp.Markings;

            Dictionary<string, IMarking>.KeyCollection MarkKey = mark.Keys;

            IMarking TryM = mark["Analyze"];

            ViDi2.IView View = TryM.Views[0];// mm.Marking.Views[0];

            ViDi2.IRedView redview = (ViDi2.IRedView)View;

            regionFound = new RegionFound[redview.Regions.Count];

            ViDi2.Runtime.IRedTool tool = (ViDi2.Runtime.IRedTool)grunTimeWorkapace.StreamDict[modelName].Tools.First();

            var knownClasses = tool.KnownClasses;

            string className = knownClasses[0];
            string[] s = className.Split('_');
            string cn = "";

            if (s.GetLength(0) > 0)
            {
                if (s.GetLength(0) > 1)
                    cn = s[1];
                else
                    cn = s[0];
            }
            this.Invoke((Action)(() =>
            {
                //if (modelName == Model1NameFront)
                    ShowRegionsFront1(redview, regionFound, index, cn, Iindex, CycleNum, reg);
                //else
                //    ShowRegionsFront2(redview, regionFound, index, cn, Iindex, CycleNum, reg);
            }));
            //if (modelName == Model1Name)
            //    ShowRegions1(redview, regionFound, index, cn, Iindex);
            //else
            //    ShowRegions2(redview, regionFound, index, cn, Iindex);
        }
        public void ApplyROIRectFront(bool xNoSave, Rectangle ImageDimensions, bool xAutoROIU, System.Windows.Rect rect, ISample mySamp, int roi)
        {
            ViDi2.IManualRegionOfInterest redROI01 = (ViDi2.IManualRegionOfInterest)StreamAll.Tools.First().RegionOfInterest;
            redROI01.Parameters.Units = ViDi2.UnitsMode.Pixel;
            if (!chkFullImg1.Checked)
            {
                if (roi == 1)
                {
                    double ROIXpos = 0;
                    double ROIYpos = 0;
                    double ROIwidth = 0;
                    double ROIheight = 0;
                    double ROIangle = 0;
                    if (!xAutoROIU)
                    {
                        ROIXpos = EndData.roiPosXfront;
                        ROIYpos = EndData.roiPosYfront;
                        ROIwidth = EndData.roiWidthfront;
                        ROIheight = EndData.roiHeightfront;
                        ROIangle = 0;
                    }
                    else //auto-mode
                    {
                        ROIXpos = rect.X;
                        ROIYpos = rect.Y;
                        ROIwidth = rect.Width;
                        ROIheight = rect.Height;
                        ROIangle = 0;
                    }

                    redROI01.Parameters.Offset = new ViDi2.Point(ROIXpos, ROIYpos);
                    redROI01.Parameters.Size = new ViDi2.Size(ROIwidth, ROIheight);

                    currentROI.X = redROI01.Parameters.Offset.X;
                    currentROI.Y = redROI01.Parameters.Offset.Y;

                    currentROI.Width = redROI01.Parameters.Size.Width;
                    currentROI.Height = redROI01.Parameters.Size.Height;

                    ViDi2.Size size = redROI01.Parameters.Scale;

                    size.Height = 1;
                    size.Width = 1;

                    redROI01.Parameters.Scale = size; //tested, size = roi scale with respect to image;

                    if (currentROI.Height != 1 && currentROI.Width != 1 && (xNoSave))
                    {
                        IniFileClass AppliIni = new IniFileClass(INIPath);
                        AppliIni.WriteValue("roi", "x", currentROI.X.ToString());
                        AppliIni.WriteValue("roi", "y", currentROI.Y.ToString());
                        AppliIni.WriteValue("roi", "width", currentROI.Width.ToString());
                        AppliIni.WriteValue("roi", "height", currentROI.Height.ToString());

                        AppliIni.WriteValue("roi", "used", chkUsePPevaluationROI.Checked.ToString());
                    }

                }

               
            }
            else
            {
                if (!(currentROI.Height == 1 && currentROI.Width == 1))
                {
                    IniFileClass AppliIni = new IniFileClass(INIPath);
                    AppliIni.WriteValue("roi", "used", chkUsePPevaluationROI.Checked.ToString());
                }
            }
        }

        private void pictureBoxInspect1_Click(object sender, EventArgs e)
        {

        }

        public void listBox1_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            //try
            //{
            //    MyE.txt = listBox1.GetItemText(listBox1.SelectedItem);
            //    this.ListClicked(this, MyE);
            //}
            //catch (System.Exception ex) { }
        }

        private void btnDefect_Click(object sender, EventArgs e)
        {

        }

        private void btnNewCatalogItem1_Click(object sender, EventArgs e)
        {
            NewCatalogItem("");
        }

        public void NewCatalogItem(string sCatalogueNumber)
        {
            if(bIsOperatorMode)
            {
                System.Windows.Forms.MessageBox.Show("Please Contact a Technologist to create a new catalogue item. New catalogue item NOT created");
                return;
            }

            string result = "";
            if (sCatalogueNumber == "")
            {
                string sEndmillName = "New Edmil " + (new Random()).Next(10000, 99999).ToString();
                result = PromptForInput("Creating a new Catalog item. The new catalog item will be based on the current one", sEndmillName, true);
            }
            else
            {
                result = PromptForInput("Creating a new Catalog item. The new catalog item will be based on the current one", sCatalogueNumber, false);
            }

            if (result != null)
            {
                AddEndmill(result, JassonDataClass.getJassonParameters(CmbCatNum.Text).ImagePath);//,
                    //JassonDataClass.getJassonParameters(CmbCatNum.Text).ImagePathFront);
            }

        }

        private void txtNumberOfTopImages_TextChanged(object sender, EventArgs e)
        {
            //mFrmM
            //BeckhoffBasler.
        }

        private void txtFolderPath_Validating(object sender, CancelEventArgs e)
        {
        }


        private void CmbCatNum_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (bDataInControlsChanged)
            {
                DialogResult aDialogResult = System.Windows.Forms.MessageBox.Show(
                    "Data changes not saved! Changing Catalogue number without saving will lose changes. Do you want to save?",
                    "Data changes not saved! Save Changes?", MessageBoxButtons.YesNo);
                if (aDialogResult == DialogResult.Yes)
                {
                    SaveROI(sender,e);
                    SaveROI1(sender,e);
                }
                ClearControlsChanged();
            }

        }
        public string lstStr = "";
        public async void AddList(string item)
        {
            try
            {
                
                lstStr = lstStr + item + "\r\n";
                               
                _ = Task.Run(() => this.Invoke((Action)(() => { txtListBox1.Text = lstStr;
                    if (txtListBox1.Lines.Length > 0)
                    {
                        txtListBox1.SelectionStart = txtListBox1.TextLength;
                        txtListBox1.ScrollToCaret();
                    }
                })));
                
            }
            catch (System.Exception ex) { }
        }

        private void txtListBox1_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            //inv.settxt(txtListBox1, "");
            //lstStr = "";
        }
        public bool txtListBox1Disable = false;
        private void txtListBox1_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {

            try
            {
                if (txtListBox1Disable) return;
                int lineNumber = txtListBox1.GetLineFromCharIndex(txtListBox1.SelectionStart);
                int startIndex = txtListBox1.GetFirstCharIndexFromLine(lineNumber);
                int length = txtListBox1.Lines[lineNumber].Length;

                // now select that range
                txtListBox1.Select(startIndex, length);
                txtListBox1.Refresh();
                string str = txtListBox1.Lines[lineNumber];
                MyE.txt = str;
                this.ListClicked(this, MyE);
            }
            catch ( System.Exception ex) { }
            
        }

        private void btnInit_Click(object sender, EventArgs e)
        {
            try
            {


                btnInit.Enabled = false;
                StartImageProcessingTask();

                sDirpath = AppDomain.CurrentDomain.BaseDirectory;
                
                StringBuilder sb = new StringBuilder(sDirpath);
                sb.Replace("BeckhoffBaslerTasks", @"runTimeApp 18.03.25 DArrayP");
                sDirpath = sb.ToString();
                //EndData = JassonDataClass.getJassonParameters("Emdmill1");
                btnStartEval.Enabled = false;
                string INIpath = sDirpath + @"Data\Models.ini";
                INIPath = sDirpath + @"Data\INI.ini";

                gmodels = getModels(INIpath);
                //var control = new ViDi2.Runtime.Local.Control(ViDi2.GpuMode.Deferred);
                //var control = new ViDi2.Runtime.Local.Control(ViDi2.GpuMode.SingleDevicePerTool);
                // Initializes all CUDA devices
                //control.InitializeComputeDevices(ViDi2.GpuMode.SingleDevicePerTool, new List<int>() { });
                // Turns off optimized GPU memory since high-detail mode doesn't support it
                //var computeDevices = control.ComputeDevices;
                //control.OptimizedGPUMemory(0);//0
                //this.Control = control;
                var StreamDict = new Dictionary<string, ViDi2.Runtime.IStream>();
                string gpuID = "default/red_HDM_20M_5472x3648/0";
                string wsName = Model1Name;
                //string wsPath = sDirpath + @"Data\final models\Proj_021_201223_104500_21122023_104445.vrws";
                string wsPath = sDirpath + @"Data\final models\TopEdge3_8_25WithNewImages.vrws";

                string wsName2 = Model2Name;
                //NPNP
                
                string wsPath2 = sDirpath + @"\Data\final models\TopTrainingPeels_31_07_25_77ImagesLabeledYellowTrayAndSupriseBoxRemovedWhiteUndistinct.vrws";

                string wsNameFront = Model1NameFront;
               
                string wsPathFront = sDirpath + @"Data\final models\FrontCSInspectionFullArea3LightingsWith002DefectQuick.vrws";
                
                string wsPath2Front = sDirpath + @"\Data\final models\FrontCSInspectionFullArea3Lightings.vrws";


                //StreamDict.Add(wsName, control.Workspaces.Add(wsName, wsPath, gpuID).Streams["default"]);
                //StreamDict.Add(wsName2, control.Workspaces.Add(wsName2, wsPath2, gpuID).Streams["default"]);

                //StreamDict.Add(wsNameFront, control.Workspaces.Add(wsNameFront, wsPathFront, gpuID).Streams["default"]);
                

                grunTimeWorkapace.gpuId01 = 0;
                grunTimeWorkapace.StreamDict = StreamDict;
                string jsonContent = File.ReadAllText(JassonPath);
                List<Dictionary<string, object>> endmillData = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonContent);
                List<string> endmillNames = new List<string>();

                //foreach (var data in endmillData)
                //{
                //    if (data.ContainsKey("EndmillName"))
                //    {
                //        string endmillName = data["EndmillName"].ToString();
                //        endmillNames.Add(endmillName);
                //    }
                //}
                //LoadEndmills();
                //LoadEndmills1();
                //CmbCatNum.DataSource = endmillNames;
                //CmbCatNum1.DataSource = endmillNames;
                //CmbCatNumText = CmbCatNum.Text;
                //CmbCatNumText1 = CmbCatNum1.Text;

                //initProporties();
                //loadModels();

                //this.Text = this.Text + " Version " + Assembly.GetExecutingAssembly().GetName().Version?.ToString();

                //PrepairEval(new WpfImage(@"C:\Project\4.2.2025\InspectSolution\runTimeApp 18.03.25 DArrayP\BeckhoffBasler\bin\Debug\snap8.jpg"), true);
                //PrepairEval(new WpfImage(@"C:\Project\4.2.2025\InspectSolution\runTimeApp 18.03.25 DArrayP\BeckhoffBasler\bin\Debug\snap8.jpg"), false);
                //EndData = JassonDataClass.getJassonParameters(CmbCatNum.Text);
                //UpdateTexts(EndData);
                //if (chkShowImage.Checked)
                //{
                //    using (var file = new FileStream(EndData.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Inheritable))
                //    {
                //        onload = true;
                //        pictureBoxInspect.Image = (Bitmap)Bitmap.FromStream(file); // Image.FromFile(EndData.ImagePath);
                //        file.Close();

                //    }
                //}

                btnInit.Enabled = true;



            }
            catch (System.Exception ex) { btnInit.Enabled = true; }
        }


        string m_sTechnicianPassWord = "WordPass";
        private List<System.Windows.Forms.Control> _operatorAllowed = new List<System.Windows.Forms.Control>();

        private void btnOperatorTechnician_Click(object sender, EventArgs e)
        {
            if (bIsOperatorMode)
            {
                if (PromptForInput("Enter Password for Technician Mode", "PassWord") == m_sTechnicianPassWord)
                {
                    bIsOperatorMode = false;
                    btnOperatorTechnician.Text = "Operator";
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("Wrong Password");
                }
            }
            else
            {
                bIsOperatorMode = true;
                btnOperatorTechnician.Text = "Technologist";
            }
            ApplyRecursive(this);
        }
        ///////////


        ///

    }





}

