using System.Reactive.Linq;
using System.Reactive.Disposables;
using peak;
using peak.core;
using VL.Core.CompilerServices;
using VL.Lib;


namespace VL.Devices.IDS.Advanced;

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

    protected override void Initialize()
    {
        try
        {
            _idsPeakLibrary = IdsPeakLibrary.Use().DisposeBy(AppHost.Global);

            var deviceManager = DeviceManager.Instance();
            deviceManager.Update();
        }
        catch
        {

        }

        base.Initialize();
    }

    //Return the current enum entries
    protected override IReadOnlyDictionary<string, object> GetEntries()
    {
        if (_idsPeakLibrary is null)
        {
            return new Dictionary<string, object>()
            {
                { "Default", null! }
            };
        }

        var deviceManager = DeviceManager.Instance();

        var devices = new Dictionary<string, object>()
        {
            { "Default", deviceManager.Devices().FirstOrDefault()! }
        };
        
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
        if (_idsPeakLibrary is null)
            return Observable.Empty<object>();

        return Observable.Using(
            resourceFactory: () =>
            {
                if (OperatingSystem.IsWindows())
                    return HardwareChangedEvents.HardwareChanged
                        .Subscribe(_ =>
                        {
                            var deviceManager = DeviceManager.Instance();
                            deviceManager.Update();
                        });
                else
                    return Disposable.Empty;
            },
            observableFactory: _ =>
            {
                var deviceManager = DeviceManager.Instance();

                return Observable.Merge<object>(
                    Observable.FromEventPattern<DeviceDescriptor>(deviceManager, nameof(deviceManager.DeviceFoundEvent)),
                    Observable.FromEventPattern<string>(deviceManager, nameof(deviceManager.DeviceLostEvent)));
            });
    }

    //Optionally disable alphabetic sorting
    protected override bool AutoSortAlphabetically => false; //true is the default
}