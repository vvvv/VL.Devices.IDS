using Microsoft.Extensions.Logging;
using System.ComponentModel;
using peak.core;
using VL.Lib.Basics.Video;
using VL.Model;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using VL.Devices.IDS.Advanced;

namespace VL.Devices.IDS
{
    [ProcessNode]
    public class VideoIn : IVideoSource2, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IDisposable _idsPeakLibSubscription;
        private readonly BehaviorSubject<Acquisition?> _aquicitionStarted = new BehaviorSubject<Acquisition?>(null);

        private int _changedTicket;
        private DeviceDescriptor? _device; 
        private Int2 _resolution;
        private int _fps;
        private IConfiguration? _configuration;
        private bool _enabled;
        private string _defaultUserSet;

        internal string Info { get; set; } = "";
        internal Spread<PropertyInfo> PropertyInfos { get; set; } = new SpreadBuilder<PropertyInfo>().ToSpread();

        public VideoIn([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
        {
            _logger = nodeContext.GetLogger();
            _idsPeakLibSubscription = IdsPeakLibrary.Use();
        }

        [return: Pin(Name = "Output")]
        public VideoIn? Update(
            IDSDevice? device,
            [DefaultValue("640, 480")] Int2 resolution,
            [DefaultValue("30")] int FPS,
            IConfiguration configuration,
            [DefaultValue("true")] bool enabled,
            [DefaultValue("Default")] string defaultUserSet,
            out string Info)
        {
            // By comparing the descriptor we can be sure that on re-connect of the device we see the change
            if (device?.Tag != _device || resolution != _resolution || FPS != _fps || configuration != _configuration || enabled != _enabled)
            {
                _device = device?.Tag as DeviceDescriptor;
                _resolution = resolution;
                _fps = FPS;
                _configuration = configuration;
                _enabled = enabled;
                _defaultUserSet = defaultUserSet;
                _changedTicket++;
            }

            Info = this.Info;

            if (!enabled) return null;

            return this;
        }

        internal IObservable<Acquisition> AcquisitionStarted => _aquicitionStarted.Where(a => a != null && !a.IsDisposed)!;

        IVideoPlayer? IVideoSource2.Start(VideoPlaybackContext ctx)
        {
            var device = _device;
            if (device is null)
                return null;

            try
            {
                var result = Acquisition.Start(this, device, _logger, _resolution, _fps, _configuration, _defaultUserSet);
                _aquicitionStarted.OnNext(result);
                return result;
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
