/*!
 * \file    AcquisitionWorker.cs
 * \author  IDS Imaging Development Systems GmbH
 * \date    2022-06-01
 * \since   1.1.6
 *
 * \brief   The AcquisitionWorker class is used in a worker thread to capture
 *          images from the device continuously and do an image conversion into
 *          a desired pixel format.
 *
 * \version 1.1.1
 *
 * Copyright (C) 2020 - 2023, IDS Imaging Development Systems GmbH.
 *
 * The information in this document is subject to change without notice
 * and should not be construed as a commitment by IDS Imaging Development Systems GmbH.
 * IDS Imaging Development Systems GmbH does not assume any responsibility for any errors
 * that may appear in this document.
 *
 * This document, or source code, is provided solely as an example of how to utilize
 * IDS Imaging Development Systems GmbH software libraries in a sample application.
 * IDS Imaging Development Systems GmbH does not assume any responsibility
 * for the use or reliability of any portion of this document.
 *
 * General permission to copy or modify is hereby granted.
 */

using System.Drawing;
using System.Diagnostics;
using peak;
using peak.core;
using peak.core.nodes;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using VL.Core;
using Microsoft.Extensions.Logging;

namespace VL.IDSPeak
{
    public class AcquisitionWorker
    {
        // Event which is raised if a new image was received
        public delegate void ImageReceivedEventHandler(object sender, Bitmap image);
        public event ImageReceivedEventHandler ImageReceived;

        // Event which is raised if the counters has changed
        public delegate void CounterChangedEventHandler(object sender, uint frameCounter, uint errorCounter);
        public event CounterChangedEventHandler CounterChanged;

        // Event which is raised if an Error or Exception has occurred
        public delegate void MessageBoxTriggerEventHandler(object sender, String messageTitle, String messageText);
        public event MessageBoxTriggerEventHandler MessageBoxTrigger;

        private peak.core.DataStream dataStream;
        private peak.core.NodeMap nodeMapRemoteDevice;

        private bool running;
        private uint frameCounter;
        private uint errorCounter;

        private readonly NodeContext _nodeContext;
        private readonly ILogger _logger;

        public AcquisitionWorker(NodeContext nodeContext)
        {
            _nodeContext = nodeContext;
            _logger = nodeContext.GetLogger();

            _logger.Log(LogLevel.Information, "IDSPeak Acquisition worker is initializing");
            running = false;
            frameCounter = 0;
            errorCounter = 0;
        }

        public void Start()
        {
            _logger.Log(LogLevel.Information, "IDSPeak Acquisition worker is starting");
            try
            {
                // Lock critical features to prevent them from changing during acquisition
                nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("TLParamsLocked").SetValue(1);

                // Start acquisition
                dataStream.StartAcquisition();
                nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("AcquisitionStart").Execute();
                nodeMapRemoteDevice.FindNode<peak.core.nodes.CommandNode>("AcquisitionStart").WaitUntilDone();
            }
            catch (Exception e)
            {
                _logger.Log(message: "IDSPeak Acquisition worker threw an exception while start", logLevel: LogLevel.Error, exception: e);
                MessageBoxTrigger(this, "Exception", e.Message);
            }

            running = true;

            while (running)
            {
                try
                {
                    // Get buffer from device's datastream
                    var buffer = dataStream.WaitForFinishedBuffer(1000);

                    // Create IDS peak IPL
                    var iplImg = new peak.ipl.Image((peak.ipl.PixelFormatName)buffer.PixelFormat(), buffer.BasePtr(),
                        buffer.Size(), buffer.Width(), buffer.Height());

                    // Debayering and convert IDS peak IPL Image to RGB8 format
                    iplImg = iplImg.ConvertTo(peak.ipl.PixelFormatName.BGRa8);

                    var width = Convert.ToInt32(iplImg.Width());
                    var height = Convert.ToInt32(iplImg.Height());
                    var stride = Convert.ToInt32(iplImg.PixelFormat().CalculateStorageSizeOfPixels(iplImg.Width()));

                    // Queue buffer so that it can be used again 
                    dataStream.QueueBuffer(buffer);

                    var data = iplImg.Data();

                    var image = new Bitmap(width, height, stride,
                        System.Drawing.Imaging.PixelFormat.Format32bppRgb, data);

                    // Create a deep copy of the Bitmap, so it doesn't use memory of the IDS peak IPL Image.
                    // Warning: Don't use image.Clone(), because it only creates a shallow copy!
                    var imageCopy = new Bitmap(image);

                    // The other images are not needed anymore.
                    image.Dispose();
                    iplImg.Dispose();

                    if (ImageReceived != null)
                    {
                        ImageReceived(this, imageCopy);
                    }

                    frameCounter++;
                }
                catch (Exception e)
                {
                    errorCounter++;
                    _logger.Log(message: "IDSPeak Acquisition worker threw an exception while acquiring a frame", logLevel: LogLevel.Error, exception: e);
                    MessageBoxTrigger(this, "Exception", e.Message);
                }

                // Raise event with current frame and error counter
                CounterChanged(this, frameCounter, errorCounter);
            }
        }

        public void Stop()
        {
            _logger.Log(LogLevel.Information, "IDSPeak Acquisition worker is stopping");
            running = false;
        }

        public void SetDataStream(peak.core.DataStream dataStream)
        {
            this.dataStream = dataStream;
        }

        public void SetNodemapRemoteDevice(peak.core.NodeMap nodeMap)
        {
            nodeMapRemoteDevice = nodeMap;
        }
    }
}
