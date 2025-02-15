﻿using AvaloniaEdit.CodeCompletion;
using NodeEditor;

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
            MainAvalonia();
        }

        static int MainAvalonia()
        {
            /// Drawing with layers, e.g. CaretLayer
            /// TextArea.OffsetProperty for AffectsRender and AvaloniaProperty.Register

            Window Build()
            {
                MathContext context = new MathContext();
                NodesGraph[] graphs = NodesGraph.Deserialize(File.ReadAllBytes("..\\..\\..\\default.nds"), context);
                foreach(NodeVisual node in graphs[0].Nodes)
                {
                    // if (node.CustomEditor is Label asLabel)
                    // {
                    //     node.CustomEditorAv = new Avalonia.Controls.Label() { Content = asLabel.Text, Background = Avalonia.Media.Brushes.Transparent };
                    //     node.CustomEditorAv.Height = node.CustomEditorAv.MinHeight = node.CustomEditor.Height;
                    //     node.CustomEditorAv.Width = node.CustomEditorAv.MinWidth = node.CustomEditor.Width;
                    //     node.CustomEditor = null;
                    // }
                }

                NodesControlAv control = new NodesControlAv(null);

                Window(out var window)
                    .Styles(GetWindowCompletionStyles())
                    .Title("NXUI").Width(800).Height(650)
                    .Content(
                      StackPanel()
                        .Children(
                          TextBox(out var tb1)
                            .Text("NXUI"),
                          control
                          )
                        )
                    .Title("Test");

                window.Closed += (e, s) =>
                {
                    File.WriteAllBytes("..\\..\\..\\default.nds", NodesGraph.Serialize(control.MainGraph));
                };

                control.AttachedToVisualTree += (e, s) =>
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

        public static IStyle[] GetWindowCompletionStyles() =>
            new IStyle[]{
                new StyleInclude((Uri?)null) { Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml") },
                Style(out var completionStyle)
                    .Selector(x => x.OfType<CompletionList>().Template().OfType<CompletionListBox>()
                      .Name("PART_ListBox"))
                    .SetAutoCompleteBoxItemTemplate(new DataTemplate() { DataType = typeof(ICompletionData), Content = new TextBlock() })};
    }
}
