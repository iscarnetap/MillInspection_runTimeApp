using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RuntimeMultiGPU2
{
    // CrossThreadCalls
    public static class inv
    {
        private delegate void SetAnyPropertyCallBack(Control c, string Property, object Value);
        public static void set(Control c, string Property, object Value) //SetAnyProperty
        {
            try
            {
                if (c.GetType().GetProperty(Property) != null)
                { //The given property exists
                    if (c.InvokeRequired)
                    {
                        SetAnyPropertyCallBack d = new SetAnyPropertyCallBack(set); //(SetAnyProperty)
                        c.BeginInvoke(d, c, Property, Value);
                    }
                    else
                    {
                        c.GetType().GetProperty(Property).SetValue(c, Value, null);
                    }
                }
            }
            catch (Exception ex) { }
        }

        private delegate object GetAnyPropertyCallBack(Control c, string Property);
        public static object get(Control c, string Property) //GetAnyProperty
        {
            if (c.GetType().GetProperty(Property) != null)
            { // The given property exists
                if (c.InvokeRequired)
                {
                    GetAnyPropertyCallBack d = new GetAnyPropertyCallBack(get); //(GetAnyProperty)
                    c.BeginInvoke(d, c, Property);
                }
                return c.GetType().GetProperty(Property).GetValue(c, null);
            }
            else
                return null;
        }

        private delegate void SetTextPropertyCallBack(Control c, string Value);
        public static void settxt(Control c, string Value) // SetTextProperty
        {
           
            if (c.InvokeRequired)
            {
                SetTextPropertyCallBack d = new SetTextPropertyCallBack(settxt); // (SetTextProperty);
                c.BeginInvoke(d, c, Value);
            }
            else
            {
                c.Text = Value;
                c.Refresh();
            }
        }

        private delegate string GetTextPropertyCallBack(Control c);
        public static string gettxt(Control c) // GetTextProperty
        {
            if (c.InvokeRequired)
            {
                GetTextPropertyCallBack d = new GetTextPropertyCallBack(gettxt); // (GetTextProperty);
                c.BeginInvoke(d, c);
            }
            return c.Text;
        }


        private delegate void ButtonPerformClickCallBack(Button c);
        public static void click(Button c) 
        {
            if (c.InvokeRequired)
            {
                ButtonPerformClickCallBack d = new ButtonPerformClickCallBack(click);
                c.BeginInvoke(d, c);
            }
            else
            {
                if (c.Enabled) c.PerformClick();
            }
        }
    }
}
