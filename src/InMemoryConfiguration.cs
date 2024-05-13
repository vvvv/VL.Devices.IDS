using peak.core;
using peak.core.nodes;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;

namespace VL.Devices.IDS
{
    [ProcessNode(Name = "SetProperty")]
    public class ConfigNode<T> : IConfiguration
    {
        private readonly ILogger logger;

        IConfiguration? input;
        string? key;
        T? value;
        FreshConfig? output;

        public ConfigNode([Pin(Visibility = Model.PinVisibility.Hidden)] NodeContext nodeContext)
        {
            this.logger = nodeContext.GetLogger();
        }

        public IConfiguration Update(IConfiguration input, string key, T value)
        {
            if (input != this.input || key != this.key || !EqualityComparer<T>.Default.Equals(value, this.value))
            {
                this.input = input;
                this.key = key;
                this.value = value;
                output = new FreshConfig(this);
            }
            return output!;
        }

        void IConfiguration.Configure(NodeMap nodeMap)
        {
            input?.Configure(nodeMap);

            Node p = nodeMap.FindNode(key);
            if (p is null)
            {
                logger.LogError("Property with name {key} not found.", key);
                return;
            }

            if (p.Type() == NodeType.Float)
            {
                var f = nodeMap.FindNodeFloat(p.Name());
                if (value is float fv)
                {
                    try
                    {
                        f.SetValue((double)fv);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to set value");
                    }
                }
                else
                {
                    logger.LogError("Failed to set value: type missmatch, expecting a float");
                }
            }

            if (p.Type() == NodeType.Integer)
            {
                var i = nodeMap.FindNodeInteger(p.Name());
                if (value is int iv)
                {
                    try
                    {
                        i.SetValue(iv);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to set value");
                    }
                }
                else
                {
                    logger.LogError("Failed to set value: type missmatch, expecting an integer");
                }
            }

            if (p.Type() == NodeType.Boolean)
            {
                var b = nodeMap.FindNodeBoolean(p.Name());
                if (value is bool bv)
                {
                    try
                    {
                        b.SetValue(bv);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to set value");
                    }
                }
                else
                {
                    logger.LogError("Failed to set value: type missmatch, expecting a boolean");
                }
            }

            if (p.Type() == NodeType.String)
            {
                var s = nodeMap.FindNodeString(p.Name());
                if (value is string sv)
                {
                    try
                    {
                        s.SetValue(sv);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to set value");
                    }
                }
                else
                {
                    logger.LogError("Failed to set value: type missmatch, expecting a string");
                }
            }

            if (p.Type() == NodeType.Enumeration)
            {
                var e = nodeMap.FindNodeEnumeration(p.Name());
                if (value is string ev)
                {
                    if (e.Entries().Select(x => x.Name()).Contains(ev))
                    {
                        try
                        {
                            e.SetCurrentEntry(e.Entries().FirstOrDefault(x => x.Name() == ev));
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to set value");
                        }
                    }
                    else
                    {
                        logger.LogError("Failed to set value: not a valid enum entry");
                    }
                    
                }
                else
                {
                    logger.LogError("Failed to set value: type missmatch, expecting a string");
                }
            }
        }
    }

    // Utility so downstream sinks see the change. Forwards the Config call.
    internal sealed class FreshConfig : IConfiguration
    {
        private readonly IConfiguration original;

        public FreshConfig(IConfiguration original)
        {
            this.original = original;
        }

        public void Configure(NodeMap nodeMap)
        {
            original.Configure(nodeMap);
        }
    }
}
