using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using peak.core;
using VL.Lib.Basics.Video;
using VL.Model;

namespace VL.IDSPeak
{
    [ProcessNode]
    public class VideoIn : IVideoSource2
    {
        // Node context and logging
        private readonly ILogger _logger;

        private int _changedTicket;
        private IDSDevice? _device;
        private Connection? _currentConnection;

        public VideoIn([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
        {
            _logger = nodeContext.GetLogger();
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
            if (_device is null)
                return null;

            var deviceDescriptor = _device.Tag as DeviceDescriptor;
            if (deviceDescriptor is null)
                return null;

            if (_currentConnection != null)
            {
                _currentConnection.IsDisposed.WaitOne(1000);
                _currentConnection = null;
            }

            try
            {
                return _currentConnection = Connection.Start(deviceDescriptor, _logger);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to start image acquisition");
                return null;
            }
        }

        int IVideoSource2.ChangedTicket => _changedTicket;
    }
}
