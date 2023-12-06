// For examples, see:
// https://thegraybook.vvvv.org/reference/extending/writing-nodes.html#examples

using peak;
using peak.core;

namespace IO.IDSPeak;

[ProcessNode]
public class ListDevices
{
    private DeviceManager _deviceManager;
    private Spread<DeviceDescriptor> devices;

    public ListDevices()
    {
        peak.Library.Initialize();
        _deviceManager = peak.DeviceManager.Instance();
        _deviceManager.Update();
    }

    public Spread<DeviceDescriptor> Update(bool Update)
    {
        if (Update)
        {
            devices = _deviceManager.Devices().ToSpread<DeviceDescriptor>();
        }
        return devices;
    }
}