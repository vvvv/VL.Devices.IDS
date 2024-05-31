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

        public GetProperty([Pin(Visibility = Model.PinVisibility.Hidden)] NodeContext nodeContext)
        {
            logger = nodeContext.GetLogger();
        }

        public void Update(
            VideoIn? input,
            string name,
            out PropertyInfo? propertyInfo)
        {
            if (input is null)
            {
                propertyInfo = null;
                return;
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
