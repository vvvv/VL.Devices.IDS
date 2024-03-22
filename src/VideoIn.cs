using Microsoft.Extensions.Logging;
using System.ComponentModel;
using peak.core;
using VL.Lib.Basics.Video;
using VL.Model;

namespace VL.Devices.IDS
{
    [ProcessNode]
    public class VideoIn : IVideoSource2, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IDisposable _idsPeakLibSubscription;

        private int _changedTicket;
        private DeviceDescriptor? _device; 
        private Int2 _resolution;
        private int _fps;

        internal string Info { get; set; } = "";

        public VideoIn([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
        {
            _logger = nodeContext.GetLogger();
            _idsPeakLibSubscription = IdsPeakLibrary.Use();
        }

        [return: Pin(Name = "Output")]
        public IVideoSource Update(
            IDSDevice? device,
            [DefaultValue("640, 480")] Int2 resolution,
            [DefaultValue("30")] int fps,
            out string Info)
        {
            // By comparing the descriptor we can be sure that on re-connect of the device we see the change
            if (device?.Tag != _device || resolution != _resolution || fps != _fps)
            {
                _device = device?.Tag as DeviceDescriptor;
                _resolution = resolution;
                _fps = fps;
                _changedTicket++;
            }

            Info = this.Info;

            return this;
        }

        IVideoPlayer? IVideoSource2.Start(VideoPlaybackContext ctx)
        {
            var device = _device;
            if (device is null)
                return null;

            try
            {
                return Acquisition.Start(this, device, _logger, _resolution, _fps);
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
