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

        private bool HasError { get; set; }
        private Bitmap Bitmap { get; set; }
        private string Status { get; set; }
        private int FrameCounter { get; set; }

        public VideoIn([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
        {

            try
            {
                _nodeContext = nodeContext;
                _logger = nodeContext.GetLogger();
                _logger.Log(LogLevel.Information, "IDSPeak VideoIn is initializing");

                backEnd = new BackEnd(nodeContext);
                backEnd.ImageReceived += BackendImageReceived;
                backEnd.CounterChanged += BackendCounterChanged;
                backEnd.MessageBoxTrigger += BackendMessageBoxTrigger;

                if (backEnd.start())
                {
                    HasError = false;
                    _logger.Log(LogLevel.Information, "IDS Backend has started");
                }
                else
                {
                    HasError = true;
                    _logger.Log(LogLevel.Error, "IDS Backend could not start");
                }
            }
            catch (Exception e)
            {
                _logger.Log(message:"Exception caught while initializing IDS Camera", logLevel: LogLevel.Error, exception: e);
                BackendMessageBoxTrigger(this, "Exception", e.Message);
            }
        }

        private void BackendImageReceived(object sender, Bitmap image) 
        {
            Bitmap = image;
        }

        private void BackendCounterChanged(object sender, uint frameCounter, uint errorCounter) 
        {
            FrameCounter = (int)frameCounter;
        }

        private void BackendMessageBoxTrigger(object sender, string messageTitle, string messageText) 
        {
            Status = messageText;
        }

        public Bitmap Update(out int FrameCounter, out string Status) 
        {
            FrameCounter = this.FrameCounter;
            Status = this.Status;
            return Bitmap;
        }

        public void Dispose()
        {
            _logger.Log(LogLevel.Information, "IDSPeak VideoIn node is disposing...");
            backEnd.ImageReceived -= BackendImageReceived;
            backEnd.CounterChanged -= BackendCounterChanged;
            backEnd.MessageBoxTrigger -= BackendMessageBoxTrigger;
        }
    }
}
