using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;

namespace RuntimeMultiGPU2
{
    class Bmp2IImage
    {



        public ViDi2.ByteImage Bitmap2ViDi2ByteImage(Bitmap bmp)
        {
            MemoryStream ms0 = new MemoryStream();
            bmp.Save(ms0, System.Drawing.Imaging.ImageFormat.Bmp);
            byte[] bmpBytes = ms0.ToArray();
            int lineWidth = (bmp.Width * 24 + 7) / 8; //ok, check agenst line 1531 libraryImage. for 24bppRgb pixel format       

            //PixelFormat pf = bmp.PixelFormat;

            //ViDi 
            int colorChannels = 3;
            ViDi2.ByteImage byteImage = new ViDi2.ByteImage(bmp.Width, bmp.Height, colorChannels, ViDi2.ImageChannelDepth.Depth8, bmpBytes, lineWidth);

            return byteImage;
        }
    }
}
