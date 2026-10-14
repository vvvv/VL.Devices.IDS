using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace VL.Devices.IDS
{
    /// <summary>
    /// Sets a property on the running camera without restarting the acquisition (unlike ConfigProperty).
    /// The value is written whenever it changes and again after every (re)start of the acquisition.
    /// Numeric values get clamped to the current range of the property, e.g. ExposureTime is limited by the frame rate.
    /// Only works for properties which are writeable during acquisition (e.g. ExposureTime, Gain, not Width/Height/PixelFormat).
    /// </summary>
    [ProcessNode(Name = "SetProperty")]
    public class SetPropertyNode<T>
    {
        private readonly ILogger logger;

        private Acquisition? appliedAcquisition;
        private string? name;
        private T? value;
        private T? actualValue;
        private bool success;
        private string message = "";

        public SetPropertyNode([Pin(Visibility = Model.PinVisibility.Hidden)] NodeContext nodeContext)
        {
            logger = nodeContext.GetLogger();
        }

        /// <param name="input">The VideoIn whose running acquisition should be modified.</param>
        /// <param name="name">The name of the property, e.g. ExposureTime.</param>
        /// <param name="value">The value to set. Enumeration entries can be given by full name (EnumEntry_ExposureAuto_Off) or symbolic value (Off).</param>
        /// <param name="enabled">If false, nothing gets written.</param>
        /// <param name="actualValue">The value read back from the camera after the last write.</param>
        /// <param name="success">Whether the last write on the current acquisition succeeded.</param>
        /// <param name="applied">True in the frame the value got written. Frames already in the pipeline may still use the previous value.</param>
        /// <param name="message">Adjustments (clamping) or error of the last write.</param>
        public void Update(
            VideoIn? input,
            string name,
            T value,
            [DefaultValue("true")] bool enabled,
            out T? actualValue,
            out bool success,
            out bool applied,
            out string message)
        {
            applied = false;

            var acquisition = enabled ? input?.CurrentAcquisition : null;
            if (acquisition is null)
            {
                // Write again as soon as an acquisition is (re)started or enabled
                appliedAcquisition = null;
                this.success = false;
                this.message = input is null ? "No input." : !enabled ? "Disabled." : "Waiting for acquisition.";
            }
            else if (acquisition != appliedAcquisition || name != this.name || !EqualityComparer<T>.Default.Equals(value, this.value))
            {
                appliedAcquisition = acquisition;
                this.name = name;
                this.value = value;
                applied = Write(acquisition, name, value);
            }

            actualValue = this.actualValue;
            success = this.success;
            message = this.message;
        }

        private bool Write(Acquisition acquisition, string name, T value)
        {
            try
            {
                string writeMessage = "";
                if (!acquisition.TryAccessNodeMap(m => NodeMapAccess.WriteValue(m, name, value, out writeMessage), out var readBack))
                {
                    // Acquisition got stopped in the meantime, try again with the next one
                    appliedAcquisition = null;
                    success = false;
                    message = "Acquisition stopped.";
                    return false;
                }

                actualValue = ConvertReadBack(readBack);
                success = true;
                message = writeMessage;
                return true;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to set property {name}", name);
                success = false;
                message = e.Message;
                return false;
            }
        }

        private static T? ConvertReadBack(object readBack)
        {
            if (readBack is T t)
                return t;
            try
            {
                return (T)Convert.ChangeType(readBack, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return default;
            }
        }
    }
}
