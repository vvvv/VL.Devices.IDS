/*!
 * \file    BackEnd.cs
 * \author  IDS Imaging Development Systems GmbH
 * \date    2022-06-01
 * \since   1.1.6
 *
 * \version 1.1.0
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Drawing;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VL.Model;

namespace VL.IDSPeak;

public class BackEnd
{
    // Logging
    private readonly NodeContext _nodeContext;
    private readonly ILogger _logger;

    // Event which is raised if a new image was received
    public delegate void ImageReceivedEventHandler(object sender, Bitmap image);
    public event ImageReceivedEventHandler ImageReceived;

    // Event which is raised if the counters has changed
    public delegate void CounterChangedEventHandler(object sender, uint frameCounter, uint errorCounter);
    public event CounterChangedEventHandler CounterChanged;

    // Event which is raised if an Error or Exception has occurred
    public delegate void MessageBoxTriggerEventHandler(object sender, string messageTitle, string messageText);
    public event MessageBoxTriggerEventHandler MessageBoxTrigger;

    private AcquisitionWorker acquisitionWorker;
    private Thread acquisitionThread;

    private peak.core.Device device;
    private peak.core.DataStream dataStream;
    private peak.core.NodeMap nodeMapRemoteDevice;

    private bool isActive;

    public BackEnd(NodeContext nodeContext)
    {
        _nodeContext = nodeContext;
        _logger = nodeContext.GetLogger();

        _logger.Log(LogLevel.Debug, "IDS Backend init");
        Console.WriteLine("--- [BackEnd] Init");

        isActive = true;

        try
        {
            // Create acquisition worker thread that waits for new images from the camera
            acquisitionWorker = new AcquisitionWorker();
            acquisitionThread = new Thread(new ThreadStart(acquisitionWorker.Start));

            acquisitionWorker.ImageReceived += acquisitionWorker_ImageReceived;
            acquisitionWorker.CounterChanged += acquisitionWorker_CounterChanged;
            acquisitionWorker.MessageBoxTrigger += acquisitionWorker_MessageBoxTrigger;

            // Initialize peak library
            peak.Library.Initialize();
        }
        catch (Exception e)
        {
            Console.WriteLine("--- [BackEnd] Exception: " + e.Message);
        }
    }

    public bool start()
    {
        Console.WriteLine("--- [BackEnd] Start");
        if (!OpenDevice())
        {
            return false;
        }

        // Start thread execution
        acquisitionThread.Start();

        return true;
    }

    public void Stop()
    {
        Console.WriteLine("--- [BackEnd] Stop");
        isActive = false;
        acquisitionWorker.Stop();

        if (acquisitionThread.IsAlive)
        {
            acquisitionThread.Join();
        }

        CloseDevice();

        // Close peak library
        peak.Library.Close();
    }

    public bool OpenDevice()
    {
        Console.WriteLine("--- [BackEnd] Open device");
        try
        {
            // Create instance of the device manager
            var deviceManager = peak.DeviceManager.Instance();

            // Update the device manager
            deviceManager.Update();

            // Return if no device was found
            if (!deviceManager.Devices().Any())
            {
                Console.WriteLine("--- [BackEnd] Error: No device found");
                MessageBoxTrigger(this, "Error", "No device found");
                return false;
            }

            // Open the first openable device in the device manager's device list
            var deviceCount = deviceManager.Devices().Count();
            Console.WriteLine("--- [BackEnd] Found " + deviceCount + " device(s)");

            for (var i = 0; i < deviceCount; ++i)
            {
                if (deviceManager.Devices()[i].IsOpenable())
                {
                    device = deviceManager.Devices()[i].OpenDevice(peak.core.DeviceAccessType.Control);

                    // Stop after the first opened device
                    break;
                }
                else if (i == deviceCount - 1)
                {
                    Console.WriteLine("--- [BackEnd] Error: Device could not be openend");
                    MessageBoxTrigger(this, "Error", "Device could not be openend");
                    return false;
                }
            }

            if (device != null)
            {
                // Check if any datastreams are available
                var dataStreams = device.DataStreams();

                if (!dataStreams.Any())
                {
                    Console.WriteLine("--- [BackEnd] Error: Device has no DataStream");
                    MessageBoxTrigger(this, "Error", "Device has no DataStream");
                    return false;
                }

                try
                {
                    // Open standard data stream
                    dataStream = dataStreams[0].OpenDataStream();
                }
                catch (Exception e)
                {
                    Console.WriteLine("--- [BackEnd] Error: Failed to open DataStream");
                    MessageBoxTrigger(this, "Error", "Failed to open DataStream\n" + e.Message);
                    return false;
                }

                // Get nodemap of remote device for all accesses to the genicam nodemap tree
                nodeMapRemoteDevice = device.RemoteDevice().NodeMaps()[0];

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
                uint payloadSize = Convert.ToUInt32(nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("PayloadSize").Value());

                // Get the minimum number of buffers that must be announced
                var bufferCountMax = dataStream.NumBuffersAnnouncedMinRequired();

                // Allocate and announce image buffers and queue them
                for (var bufferCount = 0; bufferCount < bufferCountMax; ++bufferCount)
                {
                    var buffer = dataStream.AllocAndAnnounceBuffer(payloadSize, IntPtr.Zero);
                    dataStream.QueueBuffer(buffer);
                }

                // Configure worker
                acquisitionWorker.SetDataStream(dataStream);
                acquisitionWorker.SetNodemapRemoteDevice(nodeMapRemoteDevice);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("--- [BackEnd] Exception: " + e.Message);
            MessageBoxTrigger(this, "Exception", e.Message);
            return false;
        }

        return true;
    }

    public void CloseDevice()
    {
        Console.WriteLine("--- [BackEnd] Close device");
        // If device was opened, try to stop acquisition
        if (device != null)
        {
            try
            {
                var remoteNodeMap = device.RemoteDevice().NodeMaps()[0];
                remoteNodeMap.FindNode<peak.core.nodes.CommandNode>("AcquisitionStop").Execute();
                remoteNodeMap.FindNode<peak.core.nodes.CommandNode>("AcquisitionStop").WaitUntilDone();
            }
            catch (Exception e)
            {
                Console.WriteLine("--- [BackEnd] Exception: " + e.Message);
                MessageBoxTrigger(this, "Exception", e.Message);
            }
        }

        // If data stream was opened, try to stop it and revoke its image buffers
        if (dataStream != null)
        {
            try
            {
                dataStream.KillWait();
                dataStream.StopAcquisition(peak.core.AcquisitionStopMode.Default);
                dataStream.Flush(peak.core.DataStreamFlushMode.DiscardAll);

                foreach (var buffer in dataStream.AnnouncedBuffers())
                {
                    dataStream.RevokeBuffer(buffer);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("--- [BackEnd] Exception: " + e.Message);
                MessageBoxTrigger(this, "Exception", e.Message);
            }
        }

        try
        {
            // Unlock parameters after acquisition stop
            nodeMapRemoteDevice.FindNode<peak.core.nodes.IntegerNode>("TLParamsLocked").SetValue(0);
        }
        catch (Exception e)
        {
            Console.WriteLine("--- [BackEnd] Exception: " + e.Message);
            MessageBoxTrigger(this, "Exception", e.Message);
        }
    }

    private void acquisitionWorker_ImageReceived(object sender, Bitmap image)
    {
        ImageReceived(sender, image);
    }

    private void acquisitionWorker_CounterChanged(object sender, uint frameCounter, uint errorCounter)
    {
        CounterChanged(sender, frameCounter, errorCounter);
    }

    private void acquisitionWorker_MessageBoxTrigger(object sender, string messageTitle, string messageText)
    {
        MessageBoxTrigger(sender, messageTitle, messageText);
    }

    public bool IsActive()
    {
        return isActive;
    }
}