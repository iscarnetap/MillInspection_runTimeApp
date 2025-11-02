using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SuaKITEvaluatorBatch
{
    //12/10/2021
    public static class invy
    {



        private delegate void LablePerformRefresh(Label c);
        public static void Refresh(Label c)
        {
            if (c.InvokeRequired)
            {
                //LablePerformRefresh d = new LablePerformRefresh(Refresh);
                //c.BeginInvoke(d, c);
                c.Invoke(new Action(() => c.Refresh()));
            }
            else
            {
                c.Refresh();
            }
        }

        //CheckedListBox
        private delegate void CheckedListBoxLablePerformClear(CheckedListBox c);
        public static void ClearCheckedListBox(CheckedListBox c)
        {
            if (c.InvokeRequired)
            {
                //CheckedListBoxLablePerformClear d = new CheckedListBoxLablePerformClear(ClearCheckedListBox);
                //c.BeginInvoke(d, c);
                c.Invoke(new Action(() => c.Items.Clear()));

            }
            else
            {
                c.Items.Clear();
            }
        }

        //ListBox clear
        private delegate void ListBoxPerformClear(ListBox c);
        public static void ClearListBox(ListBox c)
        {
            if (c.InvokeRequired)
            {
                //ListBoxPerformClear d = new ListBoxPerformClear(ClearListBox);
                //c.BeginInvoke(d, c);
                c.Invoke(new Action(() => c.Items.Clear()));
            }
            else
            {
                c.Items.Clear();
            }
        }

        //add item
        private delegate void ListBoxAddItem(ListBox c, string NewVal);
        public static void ListBoxaddItem(ListBox c, string NewVal)
        {
            
            if (c.InvokeRequired)
            {
               c.Invoke(new Action(() => c.Items.Add(NewVal)));
                
            }
            else
            {
                
                c.Items.Add(NewVal);
               
            }
        }

        private delegate void ListBoxRefresh(Label c);
        public static void ListBoxPerformRefresh(ListBox c)
        {
            if (c.InvokeRequired)
            {
       
         //ListBoxRefresh d = new ListBoxRefresh(Refresh);
                //c.Invoke(d, c);
                c.Invoke(new Action(() => c.Refresh()));
            }
            else
            {
                c.Refresh();
            }
        }

        //PictureBox
        private delegate void PictureBoxRefresh(PictureBox c);
        public static void PBRefresh(PictureBox c)
        {
            if (c.InvokeRequired)
            {
                //PictureBoxRefresh d = new PictureBoxRefresh(PBRefresh);
                //c.BeginInvoke(d, c);
                c.Invoke(new Action(() => c.Refresh()));
            }
            else
            {
                c.Refresh();
            }
        }
        //add row
        private delegate void DataGridViewAddRow(DataGridView c, string NewVal);
        public static void DGVaddRow(DataGridView c, string NewVal)
        {
            if (c.InvokeRequired)
            {
                //DataGridViewAddRow d = new DataGridViewAddRow(DGVaddRow);
                //c.BeginInvoke(new Action(() => c.Rows.Add(NewVal)));
                c.Invoke(new Action(() => c.Rows.Add(NewVal)));
            }
            else
            {
                c.Rows.Add(NewVal);
            }
        }
        //add row
        private delegate void DataGridViewAddRow01(DataGridView c, DataGridViewRow NewRow);
        public static void DGVaddRow01(DataGridView c, DataGridViewRow NewRow)
        {
            if (c.InvokeRequired)
            {
                //DataGridViewAddRow01 d = new DataGridViewAddRow01(DGVaddRow01);
                //c.BeginInvoke(new Action(() => c.Rows.Add(NewRow)));
                c.Invoke(new Action(() => c.Rows.Add(NewRow)));
            }
            else
            {
                c.Rows.Add(NewRow);
            }
        }
        //dgv set cell in row[n]
        private delegate void DataGridViewSetCellRown(DataGridView c, int rowNumber,int Value);
        public static void DGSetCellVal(DataGridView c, int rowNumber, int Value)
        {
            if (c.InvokeRequired)
            {
                //DataGridViewSetCellRown d = new DataGridViewSetCellRown(DGSetCellVal);
                //c.BeginInvoke(new Action(() => c.Rows[rowNumber].Cells[1].Value = Value.ToString()));
                c.Invoke(new Action(() => c.Rows[rowNumber].Cells[1].Value = Value.ToString()));
            }
            else
            {
                c.Rows[rowNumber].Cells[1].Value = Value.ToString();
            }
        }
        // string[] zr2 = Class2Zones();
        //((DataGridViewComboBoxCell) row3.Cells[2]).Items.AddRange(zr2);
        //DGV add combo data
        private delegate void DataGridViewAddComboData(DataGridView c, int rowNumber, string[] ValuesRange);
        public static void DGSetCombo(DataGridView c, int rowNumber, string[] ValuesRange)
        {
            if (c.InvokeRequired)
            {
                //DataGridViewAddComboData d = new DataGridViewAddComboData(DGSetCombo);
                //c.BeginInvoke(new Action(() => ((DataGridViewComboBoxCell)c.Rows[rowNumber].Cells[2]).Items.AddRange(ValuesRange)));
                c.Invoke(new Action(() => ((DataGridViewComboBoxCell)c.Rows[rowNumber].Cells[2]).Items.AddRange(ValuesRange)));
            }
            else
            {
                ((DataGridViewComboBoxCell)c.Rows[rowNumber].Cells[2]).Items.AddRange(ValuesRange);
            }
        }

        private delegate void DataGridViewReplaceRow(DataGridView c, int rowNumber, DataGridViewRow ReplacmentRow);
        public static void DGVaddReplaceRow(DataGridView c, int rowNumber, DataGridViewRow ReplacmentRow)
        {
            if (c.InvokeRequired)
            {
                //DataGridViewReplaceRow d = new DataGridViewReplaceRow(DGVaddReplaceRow);
                //c.BeginInvoke(new Action(() => c.Rows[rowNumber].SetValues(new object[] { ReplacmentRow.Cells[0].Value, ReplacmentRow.Cells[1].Value, ((DataGridViewComboBoxCell)ReplacmentRow.Cells[2]).Items })));  // as DataGridViewComboBoxCell).Items })));
                c.Invoke(new Action(() => c.Rows[rowNumber].SetValues(new object[] { ReplacmentRow.Cells[0].Value, ReplacmentRow.Cells[1].Value, ((DataGridViewComboBoxCell)ReplacmentRow.Cells[2]).Items })));  // as DataGridViewComboBoxCell).Items })));
            }
            else
            {
                
                c.Rows[rowNumber].SetValues(new object[] { ReplacmentRow.Cells[0].Value, ReplacmentRow.Cells[1].Value, (ReplacmentRow.Cells[2] as DataGridViewComboBoxCell).Items });
            }
        }
        //dgv.Rows.RemoveAt(2);
        private delegate void DataGridViewRemoveRow(DataGridView c, int rowNumber);
        public static void DGVaddRemoveRow(DataGridView c, int rowNumber)
        {
            if (c.InvokeRequired)
            {
                //DataGridViewRemoveRow d = new DataGridViewRemoveRow(DGVaddRemoveRow);
                //c.BeginInvoke(new Action(() => c.Rows.RemoveAt(rowNumber)));
                c.Invoke(new Action(() => c.Rows.RemoveAt(rowNumber)));
            }
            else
            {

                c.Rows.RemoveAt(rowNumber);
            }
        }


        //clear dgv
        private delegate void DataGridViewClear(DataGridView c);
        public static void DGVClear(DataGridView c)
        {
            if (c.InvokeRequired)
            {
                //DataGridViewClear d = new DataGridViewClear(DGVClear);
                //c.BeginInvoke(new Action(() => c.Rows.Clear()));
                c.Invoke(new Action(() => c.Rows.Clear()));
            }
            else
            {
                c.Rows.Clear();
            }
        }

        private delegate object GetAnyPropertyCallBack(Control c, string Property);
        public static object get(Control c, string Property) //GetAnyProperty
        {
            if (c.GetType().GetProperty(Property) != null)
            { // The given property exists
                if (c.InvokeRequired)
                {
                    //GetAnyPropertyCallBack d = new GetAnyPropertyCallBack(get); //(GetAnyProperty)
                    //c.BeginInvoke(d, c, Property);
                    c.Invoke(new Action(() => c.GetType().GetProperty(Property).GetValue(c, null)));

                    return c;
                }
                else
                {
                    return c.GetType().GetProperty(Property).GetValue(c, null);
                }
            }
            else
                return null;
        }
      
    }

    //public class ThreadIndependentMB
    //{
    //    private readonly System.Windows.Threading.Dispatcher uiDisp;
    //    private readonly Window ownerWindow;

    //    public ThreadIndependentMB(Dispatcher UIDispatcher, Window owner)
    //    {
    //        uiDisp = UIDispatcher;
    //        ownerWindow = owner;
    //    }

    //    public MessageBoxResult Show(string msg, string caption = "",
    //        MessageBoxButton buttons = MessageBoxButton.OK,
    //        MessageBoxImage image = MessageBoxImage.Information)
    //    {
    //        MessageBoxResult resmb = new MessageBoxResult();
    //        if (ownerWindow != null)
    //            uiDisp.Invoke(new Action(() =>
    //            {
    //                resmb = MessageBox.Show(ownerWindow, msg, caption, buttons, image);

    //            }));
    //        else
    //            uiDisp.Invoke(new Action(() =>
    //            {
    //                resmb = MessageBox.Show(msg, caption, buttons, image);

    //            }));
    //        return resmb;
    //    }


    //}
}
