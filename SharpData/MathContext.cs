using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using NodeEditor;

namespace MathSample
{
    // Main context of the sample, each
    // method corresponds to a node by attribute decoration
    public class MathContext : BasicContext
    {
        public override List<NodeDescriptor> GetNodeDescriptors()
        {
            List<NodeDescriptor> descriptors = base.GetNodeDescriptors();

            descriptors.Add(new NodeDescriptor(
                "Add",
                (c, i) =>
                {
                    object[] res = new object[2];
                    res[0] = (float)i[0] + (float)i[1];
                    res[1] = Math.Sign((float)res[0]);
                    return res;
                })
                .WithInput<float>("a")
                .WithInput<float>("b")
                .WithOutput<float>("result")
                .WithOutput<int>("sign"));

            descriptors.Add(new NodeDescriptor(
                "Substract",
                (c, i) =>
                {
                    object[] res = new object[1];
                    res[0] = (float)i[0] - (float)i[1];
                    return res;
                })
                .WithInput<float>("a")
                .WithInput<float>("b")
                .WithOutput<float>("result"));

            descriptors.Add(new NodeDescriptor(
                "Multiplty",
                (c, i) =>
                {
                    object[] res = new object[1];
                    res[0] = (float)i[0] * (float)i[1];
                    return res;
                })
                .WithInput<float>("a")
                .WithInput<float>("b")
                .WithOutput<float>("result"));

            descriptors.Add(new NodeDescriptor(
                "Divid",
                (c, i) =>
                {
                    object[] res = new object[1];
                    res[0] = (float)i[0] / (float)i[1];
                    return res;
                })
                .WithInput<float>("a")
                .WithInput<float>("b")
                .WithOutput<float>("result"));

            return descriptors;
        }
    }
}
