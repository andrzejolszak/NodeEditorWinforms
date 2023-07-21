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
        [Node]
        public void Add(float a, float b, out float result, out int sign)
        {
            result = a + b;
            sign = Math.Sign(result);
        }

        [Node]
        public void Substract(float a, float b, out float result)
        {
            result = a - b;
        }

        [Node]
        public void Multiplty(float a, float b, out float result)
        {
            result = a * b;
        }

        [Node]
        public void Divid(float a, float b, out float result)
        {
            result = a / b;
        }
    }
}
