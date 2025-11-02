using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using RuntimeMultiGPU2;
using System.Windows.Forms;

namespace BeckhoffBasler
{
    public class JassonClass
    {
        //static string path = @"C:\Users\inspmachha\Desktop\setUpApplication - Copy\projSampaleViewer\bin\x64\Debug\Data\DataBase\EndmillsData.Jason";
        static string path=frmMain.JassonPath;
        public static List<Endmill> GetEndmillData()
        {
            List<Endmill> endmillData;
            try
            {
                string jsonFilePath = path;

                using (StreamReader file = File.OpenText(jsonFilePath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    endmillData = (List<Endmill>)serializer.Deserialize(file, typeof(List<Endmill>));
                    return endmillData;
                }
            }
            catch (Exception ex) { return null; }
        }

        public Endmill getJassonParameters(string endmillName)
        {
            Endmill EndData = new Endmill();
            try
            {
                List<Endmill> endmillData = JassonClass.GetEndmillData();
                Endmill currEndmill = endmillData.Find(e => e.EndmillName == endmillName);

                if (currEndmill != null)
                {
                    EndData.EndmillName = currEndmill.EndmillName;
                    EndData.CatalogNo = currEndmill.CatalogNo;
                    EndData.BladesNo = currEndmill.BladesNo;
                    EndData.Diameter = currEndmill.Diameter;
                    EndData.Length = currEndmill.Length;
                    EndData.FracLowerThreshold = currEndmill.FracLowerThreshold;
                    EndData.FracUpperThreshold = currEndmill.FracUpperThreshold;
                    EndData.PeelLowerThreshold = currEndmill.PeelLowerThreshold;
                    EndData.PeelUpperThreshold = currEndmill.PeelUpperThreshold;
                    EndData.roiPosX1 = currEndmill.roiPosX1;
                    EndData.roiPosY1 = currEndmill.roiPosY1;
                    EndData.roiWidth1 = currEndmill.roiWidth1;
                    EndData.roiHeight1 = currEndmill.roiHeight1;
                    EndData.roiAngle1 = currEndmill.roiAngle1;
                    EndData.roiRatio1 = currEndmill.roiRatio1;
                    EndData.roiPosX2 = currEndmill.roiPosX2;
                    EndData.roiPosY2 = currEndmill.roiPosY2;
                    EndData.roiWidth2 = currEndmill.roiWidth2;
                    EndData.roiHeight2 = currEndmill.roiHeight2;
                    EndData.roiAngle2 = currEndmill.roiAngle2;
                    EndData.roiRatio2 = currEndmill.roiRatio2;
                    EndData.roiPosX3 = currEndmill.roiPosX3;
                    EndData.roiPosY3 = currEndmill.roiPosY3;
                    EndData.roiWidth3 = currEndmill.roiWidth3;
                    EndData.roiHeight3 = currEndmill.roiHeight3;
                    EndData.roiAngle3 = currEndmill.roiAngle3;
                    EndData.roiRatio3 = currEndmill.roiRatio3;
                    EndData.ImagePath = currEndmill.ImagePath;
                    EndData.IsFractions1 = currEndmill.IsFractions1;
                    EndData.IsPeels1 = currEndmill.IsPeels1;
                    EndData.IsFractions2 = currEndmill.IsFractions2;
                    EndData.IsPeels2 = currEndmill.IsPeels2;
                    EndData.IsFractions3 = currEndmill.IsFractions3;
                    EndData.IsPeels3 = currEndmill.IsPeels3;
                    EndData.Roi1 = currEndmill.Roi1;
                    EndData.Roi2 = currEndmill.Roi2;
                    EndData.Roi3 = currEndmill.Roi3;

                    EndData.FracLowerArea = currEndmill.FracLowerArea;
                    EndData.FracUpperArea = currEndmill.FracUpperArea;
                    EndData.PeelLowerArea = currEndmill.PeelLowerArea;
                    EndData.PeelUpperArea = currEndmill.PeelUpperArea;

                    EndData.roiPosXfront = currEndmill.roiPosXfront;
                    EndData.roiPosYfront = currEndmill.roiPosYfront;
                    EndData.roiWidthfront = currEndmill.roiWidthfront;
                    EndData.roiHeightfront = currEndmill.roiHeightfront;
                    EndData.RoiFront = currEndmill.RoiFront;
                    EndData.IsFractionsFront = currEndmill.IsFractionsFront;
                    EndData.IsPeelsFront = currEndmill.IsPeelsFront;

                    EndData.PeelLowerThresholdFront = currEndmill.PeelLowerThresholdFront;
                    EndData.PeelUpperThresholdFront = currEndmill.PeelUpperThresholdFront;
                    EndData.FracLowerThresholdFront = currEndmill.FracLowerThresholdFront;
                    EndData.FracUpperThresholdFront = currEndmill.FracUpperThresholdFront;

                    EndData.PeelLowerAreaFront = currEndmill.PeelLowerAreaFront;
                    EndData.PeelUpperAreaFront = currEndmill.PeelUpperAreaFront;
                    EndData.FracLowerAreaFront = currEndmill.FracLowerAreaFront;
                    EndData.FracUpperAreaFront = currEndmill.FracUpperAreaFront;
                    EndData.ImagePathFront = currEndmill.ImagePathFront;

                    //NPNP
                    EndData.iNumberOfTopImages = currEndmill.iNumberOfTopImages;
                    EndData.iExposureTimeDefectInspection = currEndmill.iExposureTimeDefectInspection;
                    EndData.iExposureTimeDefectInspectionFront = currEndmill.iExposureTimeDefectInspectionFront;

                }
                return EndData;
            }
            catch (Exception ex) { return EndData; }
        }

        public void setNewValue(Endmill newEndmill, bool newitem=true)
        {
            frmMain frmEndmill = new frmMain();
            string jsonString = File.ReadAllText(path);
            List<Endmill> endmillList = JsonConvert.DeserializeObject<List<Endmill>>(jsonString);
            string desiredEndmillName = newEndmill.EndmillName;
            bool doesExist = endmillList.Exists(endmill => endmill.EndmillName == desiredEndmillName);
            int index= endmillList.FindIndex(endmill => endmill.EndmillName == desiredEndmillName);
            if(doesExist && index >= 0 && !newitem)
            {
                //remove existing item
                endmillList[index] = newEndmill;
                string updatedJsonString = JsonConvert.SerializeObject(endmillList, Formatting.Indented);
                File.WriteAllText(path, updatedJsonString);

               //System.Windows.Forms.MessageBox.Show("Saved Succefuly ", "OK", MessageBoxButtons.OK,
               //      MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
                return;

            }
            else if (doesExist && newitem)
            {
                System.Windows.Forms.MessageBox.Show("Another section with the same name already exists. Please Enter Other Endmill Name");
            }
            else
            {
                endmillList.Add(newEndmill);
                string updatedJsonString = JsonConvert.SerializeObject(endmillList, Formatting.Indented);
                File.WriteAllText(path, updatedJsonString);

                //System.Windows.Forms.MessageBox.Show("Saved Succefuly");
            }
        }
    }

    public class Endmill
    {
        public string EndmillName;
        public int CatalogNo { get; set; }
        public int BladesNo { get; set; }
        public int Diameter { get; set; }
        public int Length { get; set; }
        public float FracLowerThreshold { get; set; }
        public float FracUpperThreshold { get; set; }
        public float PeelLowerThreshold { get; set; }
        public float PeelUpperThreshold { get; set; }
        public int roiPosX1 { get; set; }
        public int roiPosY1 { get; set; }
        public int roiWidth1 { get; set; }
        public int roiHeight1 { get; set; }
        public float roiAngle1 { get; set; }
        public float roiRatio1 { get; set; }
        public int roiPosX2 { get; set; }
        public int roiPosY2 { get; set; }
        public int roiWidth2 { get; set; }
        public int roiHeight2 { get; set; }
        public float roiAngle2 { get; set; }
        public float roiRatio2 { get; set; }
        public int roiPosX3 { get; set; }
        public int roiPosY3 { get; set; }
        public int roiWidth3 { get; set; }
        public int roiHeight3 { get; set; }
        public float roiAngle3 { get; set; }
        public float roiRatio3 { get; set; }
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
        

        public float FracLowerArea { get; set; }
        public float FracUpperArea { get; set; }
        public float PeelLowerArea { get; set; }
        public float PeelUpperArea { get; set; }

        public string RoiFront { get; set; }
        public int roiPosXfront { get; set; }
        public int roiPosYfront { get; set; }
        public int roiWidthfront { get; set; }
        public int roiHeightfront { get; set; }
        public string IsFractionsFront { get; set; }
        public string IsPeelsFront { get; set; }
        public float FracLowerAreaFront { get; set; }
        public float FracUpperAreaFront { get; set; }
        public float PeelLowerAreaFront { get; set; }
        public float PeelUpperAreaFront { get; set; }
        public float FracLowerThresholdFront { get; set; }
        public float FracUpperThresholdFront { get; set; }
        public float PeelLowerThresholdFront { get; set; }
        public float PeelUpperThresholdFront { get; set; }
        public float PeelScore { get; set; }
        public float FracScore { get; set; }
        public string ImagePathFront { get; set; }
        public int iNumberOfTopImages { get; set; }
        public int iExposureTimeDefectInspection { get; set; }
        public int iExposureTimeDefectInspectionFront { get; set; }

    }
}
