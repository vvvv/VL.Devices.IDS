using Microsoft.Extensions.Logging;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace VL.Devices.IDS
{
    [ProcessNode(Name = "AvailableProperties")]
    public class AvailableProperties : IDisposable
    {
        private readonly ILogger logger;
        private readonly SerialDisposable serialDisposable = new();

        public AvailableProperties([Pin(Visibility = Model.PinVisibility.Hidden)] NodeContext nodeContext)
        {
            logger = nodeContext.GetLogger();
        }

        public void Update(
            VideoIn? input,
            out Spread<string> properties)
        {
            if (input is null)
            {
                properties = Spread<string>.Empty;
                return;
            }
            properties = input.PropertyInfos.Select(x => x.Name).OrderBy(x => x).ToSpread();
            return;            
        }

        public void Dispose()
        {
            serialDisposable.Dispose();
        }
    }
}
