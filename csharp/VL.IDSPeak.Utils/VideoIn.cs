using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VL.IDSPeak
{
    [ProcessNode]
    public class VideoIn
    {
        private BackEnd backEnd { get; set; }
        public bool HasError { get; set; }
        public Bitmap bitmap { get; set; }

        public VideoIn()
        {
            try
            {
                Console.WriteLine("Init");
                backEnd = new BackEnd();
                backEnd.ImageReceived += BackendImageReceived;
                backEnd.CounterChanged += BackendCounterChanged;
                backEnd.MessageBoxTrigger += BackendMessageBoxTrigger;

                if (backEnd.start())
                {
                    Console.WriteLine("All good");
                    HasError = false;
                }
                else
                {
                    Console.WriteLine("Grrrrr");
                    HasError = true;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("--- [FormWindow] Exception: " + e.Message);
                BackendMessageBoxTrigger(this, "Exception", e.Message);
            }
        }

        private void BackendImageReceived(object sender, Bitmap image) 
        {
            Console.WriteLine("I have received something bro");
            bitmap = image;
        }

        private void BackendCounterChanged(object sender, uint frameCounter, uint errorCounter) { }

        private void BackendMessageBoxTrigger(object sender, string messageTitle, string messageText) { }

        public Bitmap Update() 
        {
            return bitmap;
        }
    }
}
