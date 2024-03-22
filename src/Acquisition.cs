using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Logging;
using peak.core;
using peak.ipl;
using VL.Lib.Basics.Resources;
using VL.Lib.Basics.Video;

namespace VL.Devices.IDS
{
    internal class Acquisition : IVideoPlayer
    {
        public static Acquisition? Start(VideoIn videoIn, DeviceDescriptor deviceDescriptor, ILogger logger, Int2 resolution, int fps)
        {
            logger.Log(LogLevel.Information, "Starting image acquisition on {device}", deviceDescriptor.DisplayName());

            var device = deviceDescriptor.OpenDevice(DeviceAccessType.Control);
            if (device is null)
            {
                logger.LogError("Failed to open device");
                return null;
            }

            var dataStreamDescriptor = device.DataStreams()?.FirstOrDefault();
            if (dataStreamDescriptor is null)
            {
                logger.LogError("There're no data streams");
                return null;
            }

            DataStream dataStream;
            try
            {
                dataStream = dataStreamDescriptor.OpenDataStream();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to open data stream");
                return null;
            }

            // Get nodemap of remote device for all accesses to the genicam nodemap tree
            var nodeMapRemoteDevice = device.RemoteDevice().NodeMaps()[0];

            // To prepare for untriggered continuous image acquisition, load the default user set if available
            // and wait until execution is finished
            try
            {
                nodeMapRemoteDevice.FindNode<peak.core.nodes.EnumerationNode>("UserSetSelector").SetCurrentEntry("Default");
                nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("UserSetLoad").Execute();
                nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("UserSetLoad").WaitUntilDone();
            }
            catch
            {
                // UserSet is not available
            }

            // Get the payload size for correct buffer allocation
            var payloadSize = nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("PayloadSize").Value();

            // get min and max fps
            var minFPS = nodeMapRemoteDevice.FindNode<peak.core.nodes.FloatNode>("AcquisitionFrameRateMinFunc").Value();
            var maxFPS = nodeMapRemoteDevice.FindNode<peak.core.nodes.FloatNode>("AcquisitionFrameRateMaxFunc").Value();

            // get max width and height
            var maxWidth = nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("WidthMax").Value();
            var maxHeight = nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("HeightMax").Value();

            // get min width and height
            var minWidth = nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("WidthMinReg").Value();
            var minHeight = nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("HeightMinReg").Value();

            // set resolution and fps
            nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("Width").SetValue(Math.Max(Math.Min(maxWidth, resolution.X), minWidth));
            nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("Height").SetValue(Math.Max(Math.Min(maxHeight, resolution.Y), minHeight));
            nodeMapRemoteDevice.FindNode<peak.core.nodes.FloatNode>("AcquisitionFrameRate").SetValue(Math.Max(Math.Min(fps, maxFPS), minFPS));


            // Get the minimum number of buffers that must be announced
            var bufferCountMax = dataStream.NumBuffersAnnouncedMinRequired();

            // Allocate and announce image buffers and queue them
            for (var bufferCount = 0; bufferCount < bufferCountMax; ++bufferCount)
            {
                var buffer = dataStream.AllocAndAnnounceBuffer((uint)payloadSize, IntPtr.Zero);
                dataStream.QueueBuffer(buffer);
            }

            // Lock critical features to prevent them from changing during acquisition
            nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("TLParamsLocked").SetValue(1);

            // Start the acquisition engine in the transport layer
            dataStream.StartAcquisition();

            // Start the acquisition in the Remote Device
            nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("AcquisitionStart").Execute();
            nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("AcquisitionStart").WaitUntilDone();

            var width = nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("Width").Value();
            var height = nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("Height").Value();

            //return debug info
            videoIn.Info = "Max. resolution (w x h): " + maxWidth + " x " + maxHeight
                          + $"\r\nMin. resolution (w x h): " + minWidth + " x " + minHeight
                          + $"\r\nCurrent resolution (w x h): " + width + " x " + height
                          + $"\r\nFramerate range: [{minFPS}, {maxFPS}], current FPS: {nodeMapRemoteDevice.FindNode<peak.core.nodes.FloatNode>("AcquisitionFrameRate").Value()}";

            return new Acquisition(logger, device, dataStream, nodeMapRemoteDevice, new Int2((int)width, (int)height));
        }

        private readonly IDisposable _idsPeakLibSubscription;
        private readonly ILogger _logger;
        private readonly Device _device; // DO NOT DELETE ME! Otherwise the finalizer will kill the whole graph!
        private readonly DataStream _dataStream;
        private readonly NodeMap _nodeMapRemoteDevice;
        private readonly ImageConverter _imageConverter;
        private readonly Int2 _resolution;

        public Acquisition(ILogger logger, Device device, DataStream dataStream, NodeMap nodeMapRemoteDevice, Int2 resolution)
        {
            _idsPeakLibSubscription = IdsPeakLibrary.Use();
            _logger = logger;
            _device = device;
            _dataStream = dataStream;
            _nodeMapRemoteDevice = nodeMapRemoteDevice;
            _imageConverter = new ImageConverter();
            _resolution = resolution;
        }

        public PixelFormat PixelFormat { get; set; } = new PixelFormat(PixelFormatName.BGRa8);

        public void Dispose()
        {
            _logger.Log(LogLevel.Information, "Stopping image acquisition");

            try
            {
                _nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("AcquisitionStop").Execute();
                _nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("AcquisitionStop").WaitUntilDone();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected exception while stopping remote acquisition");
            }

            try
            {
                _dataStream.KillWait();
                _dataStream.StopAcquisition(peak.core.AcquisitionStopMode.Default);
                _dataStream.Flush(peak.core.DataStreamFlushMode.DiscardAll);

                foreach (var buffer in _dataStream.AnnouncedBuffers())
                {
                    _dataStream.RevokeBuffer(buffer);
                }

                _dataStream.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected exception while closing data stream");
            }

            try
            {
                // Unlock parameters after acquisition stop
                _nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("TLParamsLocked").SetValue(0);
                _nodeMapRemoteDevice.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected exception while unlocking parameters");
            }

            _imageConverter.Dispose();
            _device.Dispose();
            _idsPeakLibSubscription.Dispose();
        }

        public unsafe IResourceProvider<VideoFrame>? GrabVideoFrame()
        {
            using var buffer = _dataStream.WaitForFinishedBuffer(100);

            if (!buffer.HasNewData() || buffer.IsIncomplete())
            {
                _dataStream.QueueBuffer(buffer);
                return null;
            }

            // Create IDS peak IPL
            using var bayerImage = new peak.ipl.Image((peak.ipl.PixelFormatName)buffer.PixelFormat(), buffer.BasePtr(),
                buffer.Size(), buffer.Width(), buffer.Height());

            // Debayering and convert IDS peak IPL Image to RGB8 format
            var bgraImage = _imageConverter.Convert(bayerImage, PixelFormat);

            var width = (int)bgraImage.Width();
            var height = (int)bgraImage.Height();
            var stride = (int)bgraImage.PixelFormat().CalculateStorageSizeOfPixels((uint)width);

            // Queue buffer so that it can be used again 
            _dataStream.QueueBuffer(buffer);

            var memoryOwner = new UnmanagedMemoryManager<BgraPixel>(bgraImage.Data(), stride * height);
            var pitch = stride - width * sizeof(BgraPixel);
            var memory = memoryOwner.Memory.AsMemory2D(0, height, width, pitch);
            var videoFrame = new VideoFrame<BgraPixel>(memory);
            return ResourceProvider.Return(videoFrame, (memoryOwner, bgraImage),
                static x =>
                {
                    ((IDisposable)x.memoryOwner).Dispose();
                    x.bgraImage.Dispose();
                });
        }
    }
}
