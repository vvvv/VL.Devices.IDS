/// Steps to implement your own enum based on this template:
/// 1) Rename "VideoOutput" to what your enum should be named
/// 2) Rename "VideoOutputDefinition" accordingly
/// 3) Implement the definitions GetEntries() 
/// 
/// For more details regarding the template, see:
/// https://thegraybook.vvvv.org/reference/extending/writing-nodes.html#dynamic-enums

using System.Reactive.Linq;
using VL.Core.CompilerServices;
using VL.Lib.Collections;
using VL.Lib;

using peak;
using peak.core;

namespace VL.IDSPeak;

[Serializable]
public class IDSDevice : DynamicEnumBase<IDSDevice, IDSDeviceDefinition>
{
    public IDSDevice(string value) : base(value)
    {
    }

    [CreateDefault]
    public static IDSDevice CreateDefault()
    {
        return CreateDefaultBase();
    }
}

public class IDSDeviceDefinition : DynamicEnumDefinitionBase<IDSDeviceDefinition>
{
    private IDisposable? _idsPeakLibrary;

    //Return the current enum entries
    protected override IReadOnlyDictionary<string, object> GetEntries()
    {
        // TODO: Review this line - we might want to kill the lib immediately because lifetime tied to dev session is stupid and leads to crash on shutdown
        if (_idsPeakLibrary is null)
            _idsPeakLibrary = IdsPeakLibrary.Use().DisposeBy(AppHost.Global);

        var deviceManager = DeviceManager.Instance();
        deviceManager.Update();

        var devices = new Dictionary<string, object>();
        
        foreach(var device in deviceManager.Devices())
        {
            var name = device.DisplayName();
            if(!devices.ContainsKey(name))
            {
                devices.Add(name, device);
            }
        }
        
        return devices;
    }

    //Optionally trigger a change of your enum. This will in turn call GetEntries() again
    protected override IObservable<object> GetEntriesChangedObservable()
    {
        // These two events seem to be interesting but I can't figure out how to make them trigger this function
        // _deviceManager.DeviceFoundEvent
        //_deviceManager.DeviceLostEvent
        return Observable.Empty<object>();
    }

    //Optionally disable alphabetic sorting
    protected override bool AutoSortAlphabetically => false; //true is the default
}