using Microsoft.Extensions.Logging;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace VL.Devices.IDS
{
    [ProcessNode(Name = "GetProperty")]
    public class GetProperty : IDisposable
    {
        private readonly ILogger logger;
        private readonly SerialDisposable serialDisposable = new();
        private bool readErrorLogged;

        public GetProperty([Pin(Visibility = Model.PinVisibility.Hidden)] NodeContext nodeContext)
        {
            logger = nodeContext.GetLogger();
        }

        /// <param name="live">If true, reads the current value from the running camera instead of the snapshot taken at acquisition start. Falls back to the snapshot while no acquisition is running.</param>
        public void Update(
            VideoIn? input,
            string name,
            out PropertyInfo? propertyInfo,
            [Pin(Visibility = Model.PinVisibility.Optional)] bool live = false)
        {
            if (input is null)
            {
                propertyInfo = null;
                return;
            }

            if (live && input.CurrentAcquisition is { } acquisition)
            {
                try
                {
                    if (acquisition.TryAccessNodeMap(m => NodeMapAccess.ReadPropertyInfo(m, name), out var info))
                    {
                        propertyInfo = info;
                        readErrorLogged = false;
                        return;
                    }
                }
                catch (Exception e)
                {
                    // Log only once per failure streak, this runs every frame
                    if (!readErrorLogged)
                        logger.LogError(e, "Failed to read property {name}", name);
                    readErrorLogged = true;
                }
            }

            propertyInfo = input.PropertyInfos.FirstOrDefault(x => x.Name == name);
            return;
        }

        public void Dispose()
        {
            serialDisposable.Dispose();
        }
    }
}
