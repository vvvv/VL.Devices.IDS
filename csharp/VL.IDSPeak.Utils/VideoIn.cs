using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using peak;
using peak.core;
using VL.Lib.Basics.Video;
using VL.Model;

namespace VL.IDSPeak
{
    [ProcessNode]
    public class VideoIn : IVideoSource2, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IDisposable _idsPeakLibSubscription;

        private int _changedTicket;
        private IDSDevice? _device;

        public VideoIn([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
        {
            _logger = nodeContext.GetLogger();
            _idsPeakLibSubscription = IdsPeakLibrary.Use();
        }

        [return: Pin(Name = "Output")]
        public IVideoSource Update(IDSDevice? device)
        {
            if (device != _device)
            {
                _device = device;
                _changedTicket++;
            }

            return this;
        }

        IVideoPlayer? IVideoSource2.Start(VideoPlaybackContext ctx)
        {
            var deviceDescriptor = _device?.Tag as DeviceDescriptor;
            if (deviceDescriptor is null)
                return null;

            try
            {
                return Acquisition.Start(deviceDescriptor, _logger);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to start image acquisition");
                return null;
            }
        }

        int IVideoSource2.ChangedTicket => _changedTicket;

        public void Dispose()
        {
            _idsPeakLibSubscription.Dispose();
        }
    }
}
