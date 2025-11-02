using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Inspection
{
    public class DataIniFile
    {
        public DataIniFile() { } //constructor
       
        ////////////////////////////////////windows inifile [,] array//////////////////////////////////
       
        //////////////////////////////// NEW INI FILE [][] array of sections overload ///////////////////////////////////

        public bool ReadIniFile(string filename, ref string[][] filearr)
        {
            //create file array from file
            string line;
            // Read and display lines from the file until the end of 

            if (!File.Exists(filename)) return false;//no file exist,create new one
            StreamReader sr = new StreamReader(filename);

            int j = -1;
            int jj = 0;
            
            try
            {
                while (!sr.EndOfStream)
                {
                    
                    if (!String.IsNullOrEmpty(line = sr.ReadLine()))
                    {
                        string sSection = Regex.Replace(line, " ", "");
                        if (sSection.StartsWith("[") & sSection.EndsWith("]"))
                        {
                            jj = 0;
                            j++;
                            if (filearr.Length < j + 1)
                            {
                                Array.Resize<string[]>(ref filearr, filearr.Length + 1);
                                Array.Resize<string>(ref filearr[j], 1);
                            }
                            //CheckArraySize(ref filearr, j, 1);
                            if (filearr[j].Length < 1) { Array.Resize<string>(ref filearr[j], 1); }
                            filearr[j][0] = sSection.Trim();// Regex.Replace(sSection.Trim(), " ", "");
                            jj++; 
                        }
                        else
                        {
                            if (filearr[j].Length < jj + 1)
                            {
                                Array.Resize<string>(ref filearr[j], filearr[j].Length + 1);
                            }
                            //CheckArraySize(ref filearr, j, jj+1);
                            filearr[j][jj++] = line.Trim();// Regex.Replace(line.Trim(), " ", ""); 
                        }
                    }
                }
                sr.Close();
                return (true);
            }
            catch //(Exception ex)
            { return false; }
        }

        public string GetKeyValueArrINI(string sSection, string sKey, string[][] file_arr)
        {
            int i; int ii;

            for (i = 0; i < file_arr.Length ; i++)
            {
                if (!String.IsNullOrEmpty(file_arr[i][0]))//section
                {
                    string Section = file_arr[i][0];
                    if (Section.Trim().ToLower() == "[" + sSection.Trim().ToLower() + "]") //fined sSection
                    {
                        //search in section
                        try
                        {
                            for (ii = 1; ii < file_arr[i].Length ; ii++)
                            {
                                string[] fvars = file_arr[i][ii].Split('=');
                                if (fvars[0].Trim() == sKey.Trim())
                                {
                                    return fvars[1];//value
                                }
                            }
                        }
                        catch { return ""; }
                    }
                }
            }
            return "";
        }

        public string GetKeyValue1(string filename, string sSection, string sKey,out string error)
        {
            //fined KeyValue in sSection in file
            error = "";
            string[][] arrnew = new string[1][];
            arrnew[0] = new string[0];
            filename = filename.Trim();
            if (String.IsNullOrEmpty(filename)) return "";

            try
            {
                if (!ReadIniFile(filename, ref arrnew)) return "";
                return GetKeyValueArrINI(sSection, sKey, arrnew);
            }
            catch (Exception err) { error = err.ToString(); return ""; }
        }

        public bool CreateKeyValueArr(string sSection, string sKey, string sKeyValue, ref string[][] arr, Single min, Single max, bool Number, ref string mess)
        //string sSection, string sKey, string sKeyValue, return array "arr" of strings
        {
            Single result;
            try
            {
                if (string.IsNullOrEmpty(sKeyValue)) { mess = sKey + "; " + sKeyValue + "FIELD IS EMPTY"; return false; }
                if (Number)//check number
                {
                    if (!Single.TryParse(sKeyValue, out result)) { mess = sKey + "; " + sKeyValue + " NOT A NUMBER"; return false; }
                    if (max > min)
                    {
                        if ((Single.Parse(sKeyValue) < min) || (Single.Parse(sKeyValue) > max))
                        {
                            mess = sKey + "; " + sKeyValue + " WRONG DATA";
                            return false;
                        }
                        sKeyValue = sKeyValue.Trim();
                    }
                }
                int i;//create section array
                for (i = 0; i < arr.Length; i++)
                {
                    if (string.IsNullOrEmpty(arr[i][ 0])|| (arr[i][0].Trim() == "[" + sSection.Trim() + "]"))
                    {arr[i][0] = "[" + sSection.Trim() + "]";  break; }
                    //if (CheckArraySize(ref arr, i, 1))
                    //{
                    //    arr[++i][0] = "[" + sSection.Trim() + "]";
                    //    break; 
                    //}
                    if (i >= arr.Length - 1)
                    {
                        Array.Resize<string[]>(ref arr, arr.Length + 1);
                        Array.Resize<string>(ref arr[i + 1], 1);
                        arr[++i][0] = "[" + sSection.Trim() + "]";
                        break;
                    }
                }
                
                for (int j = 0; j < arr[i].Length; j++)//create key array
                {
                    if (string.IsNullOrEmpty(arr[i][j]) || (arr[i][j].Trim() == sKey.Trim()))//next elelment
                    { 
                        arr[i][j] = sKey.Trim() + "=" + sKeyValue.Trim();
                        break; 
                    }
                    //if (CheckArraySize(ref arr, i, arr[i].Length))
                    //{
                    //    arr[i][j + 1] = sKey.Trim() + "=" + sKeyValue.Trim();
                    //    break;
                    //}
                    if (j >= arr[i].Length - 1)
                    {
                        Array.Resize<string>(ref arr[i], arr[i].Length + 1);
                        arr[i][j + 1] = sKey.Trim() + "=" + sKeyValue.Trim();
                        break;
                    }
                }
                return true;
            }
            catch (Exception err) {
                mess = sKey + "; " + sKeyValue + " WRONG DATA " + err.ToString();
                return false;
            }
        }
        
        public bool WriteIniFile(string filename, string[][] arr_temp, out string  error)
        {
            //create inifile array from array arrsave+inifile

            //read inifile
            error="";
            string[][] file_temp = new string[1][];
            file_temp[0] = new string[0];

            if (!ReadIniFile(filename, ref file_temp))
            {
                //save to new file
                if (!WriteToFile(filename, arr_temp, out error)) 
                    return false;
                else 
                    return true;
            }
            else
            {
                //compare files
                
                //Array.Resize<string[]>(ref arr_temp, 100);
                //for (int k = 0; k < arr_temp.Length; k++) { Array.Resize<string>(ref arr_temp[k], 100); }
                //Array.Resize<string[]>(ref file_temp, 100);
                //for (int k = 0; k < file_temp.Length; k++) { Array.Resize<string>(ref file_temp[k], 100); }

                int i; int j; int ii; int jj;
                try
                {
                    for (i = 0; i < arr_temp.Length; i++)//create sections
                    {
                        if (!String.IsNullOrEmpty(arr_temp[i][0]))
                        {
                            for (j = 0; j < file_temp.Length; j++)
                            {
                                if (!String.IsNullOrEmpty(file_temp[j][0]))
                                {
                                    string sArr_temp = arr_temp[i][0].Trim().ToLower();
                                    string sFile_temp = file_temp[j][0].Trim().ToLower();
                                    if (Regex.Replace(sArr_temp, " ", "") != Regex.Replace(sFile_temp, " ", ""))
                                    {
                                        if (j == file_temp.Length - 1)
                                        {
                                            //CheckArraySize(ref file_temp, file_temp.Length, 1);

                                            Array.Resize<string[]>(ref file_temp, file_temp.Length + 1);
                                            Array.Resize<string>(ref file_temp[j + 1], 1);
                                            file_temp[file_temp.Length - 1][0] = arr_temp[i][0].Trim();//add sections
                                            break;
                                        }
                                    }
                                    else { break; }
                                }
                            }
                        }
                        else { break;}
                    }
                    //keys
                    for (i = 0; i < arr_temp.Length; i++) //for every section
                    {
                        if (!String.IsNullOrEmpty(arr_temp[i][0]))
                        {
                            for (j = 0; j < file_temp.Length; j++)
                            {
                                if (!String.IsNullOrEmpty(file_temp[j][0]))
                                {
                                    string sArr_temp = arr_temp[i][0].Trim().ToLower();
                                    string sFile_temp = file_temp[j][0].Trim().ToLower();
                                    if (Regex.Replace(sArr_temp, " ", "") == Regex.Replace(sFile_temp, " ", "")) //fined sSection
                                    {
                                        for (ii = 1; ii < arr_temp[i].Length; ii++)//looking for key
                                        {
                                            if (!String.IsNullOrEmpty(arr_temp[i][ii]))
                                            {
                                                string[] vars = arr_temp[i][ii].Split('=');
                                                for (jj = 1; jj < file_temp[j].Length; jj++)//looking for key
                                                {
                                                    if (!String.IsNullOrEmpty(file_temp[j][jj]))
                                                    {
                                                        string[] fvars = file_temp[j][jj].Split('=');
                                                        if (vars[0].Trim().ToLower() == (fvars[0].Trim().ToLower()))
                                                        { 
                                                            file_temp[j][jj] = arr_temp[i][ii];
                                                            break; 
                                                        }
                                                        if (jj == file_temp[j].Length - 1)//key not found ; add string
                                                        {
                                                            Array.Resize<string>(ref file_temp[j], file_temp[j].Length+1);
                                                            file_temp[j][file_temp[j].Length-1] = arr_temp[i][ii];
                                                        }
                                                    }
                                                }
                                                if (jj >= file_temp[j].Length )//add keys
                                                {
                                                    Array.Resize<string>(ref file_temp[j], jj+1 );
                                                    file_temp[j][jj] = arr_temp[i][ii];
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception err) { return false; }
                
                //string error = "";
                if (!WriteToFile(filename, file_temp, out error)) 
                    return false;
                else 
                    return true;
            }
        }

        public bool WriteToFile(string filename, string[][] filearr, out string Error)
        {
            Error = "";
            //########### korea1 ##################
            string[] ext = filename.Split('.');
            string filebackup = filename.Replace("." + ext[ext.Length - 1], "_lbackup." + ext[ext.Length - 1]);

            //create file for read if not exist
            if (!File.Exists(filename)) { StreamWriter sw = new StreamWriter(filename); sw.Close(); }
            //delete backup file
            if (File.Exists(filebackup)) { File.Delete(filebackup); }

            StreamWriter sw1 = new StreamWriter(filebackup);
            try
            {
                //foreach (string tmp in filearr)
                    for (int i=0;i<filearr.Length;i++)
                        for (int j = 0; j < filearr[i].Length; j++)
                {
                    if (!String.IsNullOrEmpty(filearr[i][j]))
                    {
                        if (filearr[i][j].StartsWith("[")) sw1.WriteLine("");//insert empty string
                        sw1.WriteLine(filearr[i][j]);
                    }
                }

                sw1.Close();
                if (File.Exists(filename) && (File.Exists(filebackup)))
                {
                    File.Delete(filename);
                    File.Move(filebackup, filename);
                    return true;
                }
            }
            catch (Exception err)
            {
                sw1.Close();
                Error = err.ToString();
                return false;
            }
            return false;
        }
       
        private bool CheckArraySize(ref string[][] arr_temp, int i, int j)
        {
            if (arr_temp.Length < i + 1)
            {
                Array.Resize<string[]>(ref arr_temp, arr_temp.Length + 1);
                Array.Resize<string>(ref arr_temp[i], 1);
                arr_temp[i][0] = "";
                return (true);
            }
            if (arr_temp[i].Length < j + 1)
            {
                Array.Resize<string>(ref arr_temp[i], j);
                return (true);
            }
            return false;
        }

        /////////////////////////////////// LOG FILES /////////////////////////////

        public void WriteLogFile(string sLog)
        {
            //write log file
            //create name
            string filename = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + "\\Log\\" + DateTime.Now.ToString("yyyy.MM.dd_HH") + ".txt";
            //sLog to write
            string fLog = "";
            fLog = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss.fff");//with ms
            sLog = fLog + "   " + sLog;
            if (!File.Exists(filename))
            {
                try
                {
                    FileStream aFile = new FileStream(filename, FileMode.Create, FileAccess.Write);
                    StreamWriter sw = new StreamWriter(aFile);
                    sw.WriteLine(sLog);
                    sw.Close();
                } catch {return;}
            }
            else
            {
                try
                {
                    FileStream aFile = new FileStream(filename, FileMode.Append, FileAccess.Write);
                    StreamWriter sw = new StreamWriter(aFile);
                    sw.WriteLine(sLog);
                    sw.Close();
                } catch {return;}
            }
        }

        public void KillLogFiles()
        {
            try
            {
                int k = 5;//last 5 days in Log diretory
                for (int i = k; i < 31 - k; i++)
                {
                    string filename = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + "\\Log\\"
                                    + DateTime.Now.AddDays(-i).ToString("yyyy.MM.dd_HH") + ".txt";
                    if (File.Exists(filename)) { File.Delete(filename); }
                }
            } catch { }
        }

        public void WriteResultsFile(string filename,string sLog)
        {
            //write log file
            //create name
            //string filename = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + "\\Log\\"
            //    + DateTime.Now.ToString("yyyy.MM.dd_HH") + ".txt";
            //sLog to write
            //string fLog = "";
            //fLog = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss.fff");//with ms
            //sLog = fLog;// + "   " + sLog;
            if (!File.Exists(filename))
            {
                try
                {
                    FileStream aFile = new FileStream(filename, FileMode.Create, FileAccess.Write);
                    StreamWriter sw = new StreamWriter(aFile);
                    //string title = "num" + '\t' + "WorkOrderID" + '\t' + '\t' + "HC result"  + '\t' + '\t' +
                   //"MS result" +  '\t'+'\t' + "weight" + '\t' + '\t' + "DATE/TIME";
                   // sw.WriteLine(title);
                    sw.WriteLine(sLog);
                    sw.Close();
                } catch {return;}
            }
            else
            {
                try
                {
                    FileStream aFile = new FileStream(filename, FileMode.Append, FileAccess.Write);
                    StreamWriter sw = new StreamWriter(aFile);
                    sw.WriteLine(sLog);
                    sw.Close();
                } catch {return;}
            }
        }

        public bool ReadResultsFile(string filename, ref string[] filearr)
        {
            //create file array from file
            string line;
            // Read and display lines from the file until the end of 

            if (!File.Exists(filename)) return false;//no file exist,create new one
            StreamReader sr = new StreamReader(filename);

            //int j = -1;
            //int jj = 0;
            try
            {
                while (!sr.EndOfStream)
                {
                    if (!String.IsNullOrEmpty(line = sr.ReadLine()))
                    {
                        Array.Resize<string>(ref filearr, filearr.Length + 1);
                        filearr[filearr.Length - 1] = line;
                    }
                }
                sr.Close();
                return (true);
            }
            catch //(Exception ex)
            { return false; }
        }

        public bool ReadFile(string filename, ref string[] filearr)
        {
            //create file array from file
            string line;
            // Read and display lines from the file until the end of 

            if (!File.Exists(filename.Trim())) return false;//no file exist,create new one
            StreamReader sr = new StreamReader(filename);

            //int j = -1;
            //int jj = 0;
            try
            {
                while (!sr.EndOfStream)
                {
                    if (!String.IsNullOrEmpty(line = sr.ReadLine()))
                    {
                        string sSection = Regex.Replace(line, "\t", "");
                        sSection = Regex.Replace(sSection, "\0", "");
                       // Array.Resize<string>(ref filearr, filearr.Length + 1);
                        if (!String.IsNullOrEmpty(sSection) && sSection.IndexOf('/')<0)
                        {
                            Array.Resize<string>(ref filearr, filearr.Length + 1);
                            filearr[filearr.Length - 1] = sSection.Trim();// Regex.Replace(line.Trim(), " ", ""); 
                        }
                    }
                }
                sr.Close();
                return (true);
            }
            catch //(Exception ex)
            { return false; }
        }
        public bool WriteNewFile(string filename, string[] s)
        {
            //write log file
            //create name
            //string filename = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + "\\Log\\"
            //    + DateTime.Now.ToString("yyyy.MM.dd_HH") + ".txt";
            //sLog to write
            //string fLog = "";
            //fLog = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss.fff");//with ms
            //sLog = fLog + "   " + sLog;

            if (File.Exists(filename))
            {
                try
                {
                    //FileStream aFile = new FileStream(filename, FileMode.Create, FileAccess.Write);
                    //StreamWriter sw = new StreamWriter(aFile);
                    //sw.WriteLine(sLog);
                    //sw.Close();
                    File.Delete(filename);
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                FileStream aFile = new FileStream(filename, FileMode.Append, FileAccess.Write);
                StreamWriter sw = new StreamWriter(aFile);
                for (int i = 0; i < s.Length; i++)
                {
                    sw.WriteLine(s[i]);
                }
                sw.Close();
            }
            catch
            {
                return false;
            }

            return true;

        }

    }
}
