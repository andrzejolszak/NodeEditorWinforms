using NodeEditor;
using SampleCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MathSample
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            MathContext context = new MathContext();
            NodesGraph[] graphs = NodesGraph.Deserialize(File.ReadAllBytes("..\\..\\default.nds"), context);
            FormMathSample gui = new FormMathSample(context, graphs[0], null);
            gui.FormClosing += (e, s) => File.WriteAllBytes("..\\..\\default.nds", NodesGraph.Serialize(gui.mainGraph));

            Application.Run(gui);
        }
    }
}
