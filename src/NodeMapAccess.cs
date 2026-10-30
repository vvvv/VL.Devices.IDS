using IDSImaging.Peak.API.Core;
using IDSImaging.Peak.API.Core.Nodes;

namespace VL.Devices.IDS
{
    /// <summary>
    /// Shared read/write helpers for properties of the remote device node map.
    /// </summary>
    internal static class NodeMapAccess
    {
        public static PropertyInfo? ToPropertyInfo(NodeMap nodeMap, Node p)
        {
            switch (p.Type())
            {
                case NodeType.Float:
                    var f = nodeMap.FindNodeFloat(p.Name());
                    return new PropertyInfo(f.Name(), f.Value(), f.Description(), f.Minimum(), f.Maximum(), Spread<string>.Empty, f.Type().ToString(), f.AccessStatus().ToString());

                case NodeType.Integer:
                    var i = nodeMap.FindNodeInteger(p.Name());
                    return new PropertyInfo(i.Name(), i.Value(), i.Description(), i.Minimum(), i.Maximum(), Spread<string>.Empty, i.Type().ToString(), i.AccessStatus().ToString());

                case NodeType.Boolean:
                    var b = nodeMap.FindNodeBoolean(p.Name());
                    return new PropertyInfo(b.Name(), b.Value(), b.Description(), false, true, Spread<string>.Empty, b.Type().ToString(), b.AccessStatus().ToString());

                case NodeType.String:
                    var s = nodeMap.FindNodeString(p.Name());
                    return new PropertyInfo(s.Name(), s.Value(), s.Description(), "", "", Spread<string>.Empty, s.Type().ToString(), s.AccessStatus().ToString());

                case NodeType.Enumeration:
                    var e = nodeMap.FindNodeEnumeration(p.Name());
                    return new PropertyInfo(e.Name(), e.CurrentEntry().Name(), e.Description(), "", "", e.Entries().Select(x => x.Name()).ToSpread(), e.Type().ToString(), e.AccessStatus().ToString());

                default:
                    // cannot set value
                    return null;
            }
        }

        /// <summary>
        /// Reads the current state of a readable property. Returns null if the property doesn't exist or isn't readable.
        /// </summary>
        public static PropertyInfo? ReadPropertyInfo(NodeMap nodeMap, string name)
        {
            if (string.IsNullOrEmpty(name) || !nodeMap.HasNode(name))
                return null;

            var p = nodeMap.FindNode(name);
            if (!p.IsReadable())
                return null;

            return ToPropertyInfo(nodeMap, p);
        }

        /// <summary>
        /// Writes a value to a property of a running device. Numeric values get clamped to the current range
        /// and snapped to the increment of the property. Returns the value read back from the device.
        /// Throws with a descriptive message on failure.
        /// </summary>
        public static object WriteValue(NodeMap nodeMap, string name, object? value, out string message)
        {
            message = "";

            if (string.IsNullOrEmpty(name) || !nodeMap.HasNode(name))
                throw new InvalidOperationException($"Property with name {name} not found.");

            var p = nodeMap.FindNode(name);
            if (!p.IsWriteable())
                throw new InvalidOperationException($"Property {name} is not writeable (access status: {p.AccessStatus()}).");

            switch (p.Type())
            {
                case NodeType.Float:
                    {
                        var f = nodeMap.FindNodeFloat(name);
                        var requested = ToDouble(value, name);
                        var min = f.Minimum();
                        var max = f.Maximum();
                        var v = Math.Clamp(requested, min, max);
                        if (f.HasConstantIncrement())
                        {
                            var inc = f.Increment();
                            if (inc > 0)
                                v = Math.Min(max, min + Math.Round((v - min) / inc) * inc);
                        }
                        if (v != requested)
                            message = $"Requested {requested} adjusted to {v} (range [{min}, {max}]).";
                        f.SetValue(v);
                        return f.Value();
                    }

                case NodeType.Integer:
                    {
                        var i = nodeMap.FindNodeInteger(name);
                        var requested = ToInt64(value, name);
                        var min = i.Minimum();
                        var max = i.Maximum();
                        var v = Math.Clamp(requested, min, max);
                        var inc = i.Increment();
                        if (inc > 1)
                            v = Math.Min(max, min + (long)Math.Round((double)(v - min) / inc) * inc);
                        if (v != requested)
                            message = $"Requested {requested} adjusted to {v} (range [{min}, {max}], increment {inc}).";
                        i.SetValue(v);
                        return i.Value();
                    }

                case NodeType.Boolean:
                    {
                        var b = nodeMap.FindNodeBoolean(name);
                        if (value is not bool bv)
                            throw new InvalidOperationException($"Type mismatch for {name}: expecting a boolean.");
                        b.SetValue(bv);
                        return b.Value();
                    }

                case NodeType.String:
                    {
                        var s = nodeMap.FindNodeString(name);
                        if (value is not string sv)
                            throw new InvalidOperationException($"Type mismatch for {name}: expecting a string.");
                        s.SetValue(sv);
                        return s.Value();
                    }

                case NodeType.Enumeration:
                    {
                        var e = nodeMap.FindNodeEnumeration(name);
                        if (value is not string ev)
                            throw new InvalidOperationException($"Type mismatch for {name}: expecting a string.");
                        // Accept the full entry name (as shown by GetProperty, e.g. EnumEntry_ExposureAuto_Off)
                        // as well as the symbolic value (e.g. Off)
                        var entry = e.Entries().FirstOrDefault(x => x.Name() == ev)
                            ?? e.Entries().FirstOrDefault(x => x.SymbolicValue() == ev);
                        if (entry is null)
                            throw new InvalidOperationException($"{ev} is not a valid entry of {name}.");
                        e.SetCurrentEntry(entry);
                        return e.CurrentEntry().Name();
                    }

                default:
                    throw new InvalidOperationException($"Property {name} of type {p.Type()} cannot be set.");
            }
        }

        static double ToDouble(object? value, string name) => value switch
        {
            float x => x,
            double x => x,
            int x => x,
            long x => x,
            _ => throw new InvalidOperationException($"Type mismatch for {name}: expecting a number.")
        };

        static long ToInt64(object? value, string name) => value switch
        {
            int x => x,
            long x => x,
            float x => (long)Math.Round(x),
            double x => (long)Math.Round(x),
            _ => throw new InvalidOperationException($"Type mismatch for {name}: expecting a number.")
        };
    }
}
