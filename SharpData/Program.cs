using Microsoft.Msagl.Layout.LargeGraphLayout;
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
            Task.Run(MainAvalonia);
            Task.Delay(2000).Wait();

            MainWinForm();
        }

        static void MainWinForm()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            MathContext context = new MathContext();
            NodesGraph[] graphs = NodesGraph.Deserialize(File.ReadAllBytes("..\\..\\..\\default.nds"), context);
            FormMathSample gui = new FormMathSample(context, graphs[0], null);
            gui.FormClosing += (e, s) => File.WriteAllBytes("..\\..\\..\\default.nds", NodesGraph.Serialize(gui.mainGraph));

            System.Windows.Forms.Application.Run(gui);
        }

        static int MainAvalonia()
        {
            /// Drawing with layers, e.g. CaretLayer
            /// TextArea.OffsetProperty for AffectsRender and AvaloniaProperty.Register

            Window Build()
            {
                MathContext context = new MathContext();
                NodesGraph[] graphs = NodesGraph.Deserialize(File.ReadAllBytes("..\\..\\..\\default.nds"), context);
                NodesControlAv control = new NodesControlAv(null);

                Window(out var window)
                    .Title("NXUI").Width(800).Height(650)
                    .Content(
                      StackPanel()
                        .Children(
                          TextBox(out var tb1)
                            .Text("NXUI"),
                          control
                          )
                        )
                    .Title(tb1.ObserveText().Select(x => x?.ToUpper()));

                window.Closed += (e, s) =>
                {
                    File.WriteAllBytes("..\\..\\..\\default.nds", NodesGraph.Serialize(graphs[0]));
                };

                control.Unloaded += (e, s) =>
                {
                    control.Owner?.ResetSocketsCache();
                };
                control.Loaded += (e, s) =>
                {
                    control.Initialize(context, graphs[0]);
                    control.OnSubgraphOpenRequest += Control_OnSubgraphOpenRequest;
                };

                return window;
            }

            return AppBuilder.Configure<Avalonia.Application>()
              .UsePlatformDetect()
              .UseFluentTheme()
              .StartWithClassicDesktopLifetime(Build, null);
        }

        private static void Control_OnSubgraphOpenRequest(NodeVisual obj)
        {
            // TODO: open a new one with the given owner, attach the control events

        }
    }
}
