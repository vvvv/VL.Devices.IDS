using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Logging;
using peak.core;
using peak.core.nodes;
using peak.ipl;
using System.Text;
using VL.Devices.IDS.Advanced;
using VL.Lib.Basics.Resources;
using VL.Lib.Basics.Video;

namespace VL.Devices.IDS
{
    internal class Acquisition : IVideoPlayer
    {
        public static Acquisition? Start(VideoIn videoIn, DeviceDescriptor deviceDescriptor, ILogger logger, Int2 resolution, int fps, IConfiguration? configuration, string defaultUserSet, bool CorrectHotPixels)
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
                nodeMapRemoteDevice.FindNode<EnumerationNode>("UserSetSelector").SetCurrentEntry(defaultUserSet);
                nodeMapRemoteDevice.FindNode<CommandNode>("UserSetLoad").Execute();
                nodeMapRemoteDevice.FindNode<CommandNode>("UserSetLoad").WaitUntilDone();
            }
            catch
            {
                // UserSet is not available
            }

            // Get the payload size for correct buffer allocation
            var payloadSize = nodeMapRemoteDevice.FindNode<IntegerNode>("PayloadSize").Value();

            // get min and max fps
            var aquisitionFrameRateNode = nodeMapRemoteDevice.FindNode<FloatNode>("AcquisitionFrameRate");
            var minFPS = aquisitionFrameRateNode.Minimum();
            var maxFPS = aquisitionFrameRateNode.Maximum();

            // get max width and height
            var maxWidth = nodeMapRemoteDevice.FindNode<IntegerNode>("WidthMax").Value();
            var maxHeight = nodeMapRemoteDevice.FindNode<IntegerNode>("HeightMax").Value();

            // get min width and height
            var widthNode = nodeMapRemoteDevice.FindNode<IntegerNode>("Width");
            var heightNode = nodeMapRemoteDevice.FindNode<IntegerNode>("Height");
            var minWidth = widthNode.Minimum();
            var minHeight = heightNode.Minimum();

            var width = nodeMapRemoteDevice.FindNode<IntegerNode>("Width").Value();
            var height = nodeMapRemoteDevice.FindNode<IntegerNode>("Height").Value();

            //return debug info
            videoIn.Info = "Max. resolution (w x h): " + maxWidth + " x " + maxHeight
                          + $"\r\nMin. resolution (w x h): " + minWidth + " x " + minHeight
                          + $"\r\nIncrement (w/h): " + widthNode.Increment().ToString() + "/" + heightNode.Increment().ToString()
                          + $"\r\nCurrent resolution (w x h): " + width + " x " + height
                          + $"\r\nFramerate range: [{minFPS}, {maxFPS}], current FPS: {aquisitionFrameRateNode.Value()}" +
                            $"\r\n";

            // set resolution and fps
            widthNode.SetValue(Math.Max(Math.Min(maxWidth, resolution.X), minWidth));
            heightNode.SetValue(Math.Max(Math.Min(maxHeight, resolution.Y), minHeight));
            aquisitionFrameRateNode.SetValue(Math.Max(Math.Min(fps, maxFPS), minFPS));

            // Get the minimum number of buffers that must be announced
            var bufferCountMax = dataStream.NumBuffersAnnouncedMinRequired();

            // Allocate and announce image buffers and queue them
            for (var bufferCount = 0; bufferCount < bufferCountMax; ++bufferCount)
            {
                var buffer = dataStream.AllocAndAnnounceBuffer((uint)payloadSize, IntPtr.Zero);
                dataStream.QueueBuffer(buffer);
            }

            // Lock critical features to prevent them from changing during acquisition
            nodeMapRemoteDevice.FindNode<IntegerNode>("TLParamsLocked").SetValue(1);

            //apply static parameters
            configuration?.Configure(nodeMapRemoteDevice);

            //collect available properties
            var spb = new SpreadBuilder<PropertyInfo>();
            CollectPropertiesInfos(spb, nodeMapRemoteDevice);
            videoIn.PropertyInfos = spb.ToSpread();

            // Start the acquisition engine in the transport layer
            dataStream.StartAcquisition();

            // Start the acquisition in the Remote Device
            nodeMapRemoteDevice.FindNode<CommandNode>("AcquisitionStart").Execute();
            nodeMapRemoteDevice.FindNode<CommandNode>("AcquisitionStart").WaitUntilDone();

            /*
            //debug properites list
            string props = "";
            var allProps = nodeMapRemoteDevice.Nodes();
            foreach (var prop in allProps)
            {
                if (prop.IsFeature() && prop.AccessStatus() == NodeAccessStatus.ReadWrite)
                    //if (prop.Type() != NodeType.Command)
                    {
                        props += $"\r\n{prop.Name()} ({prop.Type()}) Description: {prop.Description()}";
                    }
            }
            videoIn.Info += props + $"\r\n";

            //debug properties tree
            var sb = new StringBuilder();
            foreach (var n in nodeMapRemoteDevice.Nodes())
            {
                TraverseCategories(sb, n, "");
            }
            videoIn.Info += $"\r\n" + sb.ToString();*/

            return new Acquisition(logger, device, dataStream, nodeMapRemoteDevice, new Int2((int)width, (int)height), CorrectHotPixels);
        }

        static void CollectPropertiesInfos(SpreadBuilder<PropertyInfo> spb, NodeMap propertyMap)
        {
            var props = propertyMap.Nodes()
                .Where(x => x.IsFeature())
                .Where(x => x.AccessStatus() == NodeAccessStatus.ReadWrite)
                .Where(x => x.Type() != NodeType.Command);
            foreach (var p in props)
            {
                switch (p.Type())
                {
                    case NodeType.Float:
                        var f = propertyMap.FindNodeFloat(p.Name());
                        spb.Add(new PropertyInfo(f.Name(), f.Value(), f.Description(), f.Minimum(), f.Maximum(), Spread<string>.Empty, f.Type().ToString(), f.AccessStatus().ToString()));
                        break;

                    case NodeType.Integer:
                        var i = propertyMap.FindNodeInteger(p.Name());
                        spb.Add(new PropertyInfo(i.Name(), i.Value(), i.Description(), i.Minimum(), i.Maximum(), Spread<string>.Empty, i.Type().ToString(), i.AccessStatus().ToString()));
                        break;

                    case NodeType.Boolean:
                        var b = propertyMap.FindNodeBoolean(p.Name());
                        spb.Add(new PropertyInfo(b.Name(), b.Value(), b.Description(), false, true, Spread<string>.Empty, b.Type().ToString(), b.AccessStatus().ToString()));
                        break;

                    case NodeType.String:
                        var s = propertyMap.FindNodeString(p.Name());
                        spb.Add(new PropertyInfo(s.Name(), s.Value(), s.Description(), "", "", Spread<string>.Empty, s.Type().ToString(), s.AccessStatus().ToString()));
                        break;

                    case NodeType.Enumeration:
                        var e = propertyMap.FindNodeEnumeration(p.Name());
                        spb.Add(new PropertyInfo(e.Name(), e.CurrentEntry().Name(), e.Description(), "", "", e.Entries().Select(x => x.Name()).ToSpread(), e.Type().ToString(), e.AccessStatus().ToString()));
                        break;

                    default:
                        // cannot set value
                        break;
                }
            }
        }

        static void TraverseCategories(StringBuilder sb, Node p, string offset)
        {
            if (p is CategoryNode c)
            {
                sb.AppendLine($"{offset}--{c.Name()} ({c.Type()}) Description: {c.Description()}");
                foreach (var cp in c.SubNodes())
                {
                    //if (!cp.IsFeature() && cp.AccessStatus() == NodeAccessStatus.ReadWrite)
                    {
                        if (cp.Type() != NodeType.Category)
                        {
                            sb.AppendLine($"{offset}    {cp.Name()} ({cp.Type()}) Description: {cp.Description()}");
                        }
                        else
                        {
                            TraverseCategories(sb, cp, offset + "    ");
                        }
                    }
                }
            }
            else
            {
                sb.AppendLine($"\r\n{offset}{p.Name()} ({p.Type()}) Description: {p.Description()}");
            }
            return;
        }

        private readonly IDisposable _idsPeakLibSubscription;
        private readonly ILogger _logger;
        private readonly Device _device; // DO NOT DELETE ME! Otherwise the finalizer will kill the whole graph!
        private readonly DataStream _dataStream;
        private readonly NodeMap _nodeMapRemoteDevice;
        private readonly ImageConverter _imageConverter;
        private readonly Int2 _resolution;
        private readonly bool _correctHotPixels;
        private readonly HotpixelCorrection? _m_hotpixelCorrection;

        public Acquisition(ILogger logger, Device device, DataStream dataStream, NodeMap nodeMapRemoteDevice, Int2 resolution, bool CorrectHotPixels)
        {
            _idsPeakLibSubscription = IdsPeakLibrary.Use();
            _logger = logger;
            _device = device;
            _dataStream = dataStream;
            _nodeMapRemoteDevice = nodeMapRemoteDevice;
            _imageConverter = new ImageConverter();
            _resolution = resolution;
            _correctHotPixels = CorrectHotPixels;
            if (_correctHotPixels) _m_hotpixelCorrection = new peak.ipl.HotpixelCorrection();
        }

        public PixelFormat PixelFormat { get; set; } = new PixelFormat(PixelFormatName.BGRa8);

        public bool IsDisposed { get; private set; }

        public NodeMap NodeMap => _device.RemoteDevice().NodeMaps()[0];

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            _logger.Log(LogLevel.Information, "Stopping image acquisition");

            try
            {
                _nodeMapRemoteDevice.FindNode<CommandNode>("AcquisitionStop").Execute();
                _nodeMapRemoteDevice.FindNode<CommandNode>("AcquisitionStop").WaitUntilDone();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected exception while stopping remote acquisition");
            }

            try
            {
                _dataStream.KillWait();
                _dataStream.StopAcquisition(AcquisitionStopMode.Default);
                _dataStream.Flush(DataStreamFlushMode.DiscardAll);

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
                _nodeMapRemoteDevice.FindNode<IntegerNode>("TLParamsLocked").SetValue(0);
                _nodeMapRemoteDevice.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected exception while unlocking parameters");
            }

            _imageConverter.Dispose();
            _device.Dispose();
            _idsPeakLibSubscription.Dispose();
            _m_hotpixelCorrection?.Dispose();
        }

        public unsafe IResourceProvider<VideoFrame>? GrabVideoFrame()
        {
            using var buffer = _dataStream.WaitForFinishedBuffer(1100); //should be long enough for the lowest frame rate

            if (!buffer.HasNewData() || buffer.IsIncomplete())
            {
                _dataStream.QueueBuffer(buffer);
                return null;
            }

            // Create IDS peak IPL
            using var bayerImage = new Image((PixelFormatName)buffer.PixelFormat(), buffer.BasePtr(),
                buffer.Size(), buffer.Width(), buffer.Height());

            Image bgraImage;

            //Apply hotpixel correction
            if (_correctHotPixels)
            {
                var vec = _m_hotpixelCorrection.Detect(bayerImage);
                using var correctedImage = _m_hotpixelCorrection.Correct(bayerImage, vec);

                // Debayering and convert IDS peak IPL Image to RGB8 format
                bgraImage = _imageConverter.Convert(correctedImage, PixelFormat);
            }
            else
            {
                // Debayering and convert IDS peak IPL Image to RGB8 format
                bgraImage = _imageConverter.Convert(bayerImage, PixelFormat);
            }

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
