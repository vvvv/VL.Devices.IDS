using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VL.Model;

namespace VL.IDSPeak
{
    [ProcessNode]
    public class VideoIn
    {
        // Node context and logging
        private readonly NodeContext _nodeContext;
        private readonly ILogger _logger;

        private BackEnd backEnd { get; set; }
        public bool HasError { get; set; }
        public Bitmap bitmap { get; set; }

        public VideoIn([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
        {

            try
            {
                _nodeContext = nodeContext;
                _logger = nodeContext.GetLogger();
                _logger.Log(LogLevel.Debug, "VideoIn init");
                backEnd = new BackEnd(nodeContext);
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
