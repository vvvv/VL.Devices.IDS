// For examples, see:
// https://thegraybook.vvvv.org/reference/extending/writing-nodes.html#examples

using peak;

namespace IO.IDSPeak;

public class ListDevices
{
    private DeviceManager _deviceManager;

    public ListDevices()
    {
        peak.Library.Initialize();
        _deviceManager = peak.DeviceManager.Instance();
        _deviceManager.Update();
    }

    public void Update(bool Update)
    {
        foreach (var deviceDescriptor in _deviceManager.Devices())
        {
            Console.WriteLine(deviceDescriptor.ModelName() + " ("
                      + deviceDescriptor.ParentInterface().DisplayName() + "; "
                      + deviceDescriptor.ParentInterface().ParentSystem().DisplayName() + " v."
                      + deviceDescriptor.ParentInterface().ParentSystem().Version() + ")");
        }
    }
}