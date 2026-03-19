using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WinOptimizerHub.Helpers;
using WinOptimizerHub.Models;
using WinOptimizerHub.ViewModels;

namespace WinOptimizerHub.Views
{
    public partial class DuplicatesView : UserControl
    {
        private DuplicatesViewModel VM => DataContext as DuplicatesViewModel;

        public DuplicatesView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DuplicatesViewModel oldVm)
                oldVm.GroupsChanged -= RebuildDuplicateTree;

            if (e.NewValue is DuplicatesViewModel newVm)
                newVm.GroupsChanged += RebuildDuplicateTree;
        }

        private void RebuildDuplicateTree()
        {
            DuplicateTree.Items.Clear();
            if (VM == null) return;

            foreach (var group in VM.Groups)
            {
                long groupWaste = group.Skip(1).Sum(f => f.Size);
                var groupNode = new TreeViewItem
                {
                    Header = $"Group — {group.Count} files — {FormatHelper.FormatSize(group[0].Size)} each" +
                             $"  [{FormatHelper.FormatSize(groupWaste)} reclaimable]",
                    IsExpanded = true,
                };
                groupNode.SetResourceReference(TreeViewItem.ForegroundProperty, "TextPrimaryBrush");

                for (int i = 0; i < group.Count; i++)
                {
                    var file = group[i];
                    var cb = new CheckBox { IsChecked = file.IsMarkedForDeletion, Tag = file };
                    cb.Checked   += DupFile_CheckChanged;
                    cb.Unchecked += DupFile_CheckChanged;

                    var label = new TextBlock
                    {
                        Text = $"  {file.FileName}   {file.FullPath}   ({FormatHelper.FormatSize(file.Size)})",
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    label.SetResourceReference(TextBlock.FontFamilyProperty, "MonoFont");
                    label.FontSize = 11;

                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    row.Children.Add(cb);
                    row.Children.Add(label);

                    var ctx = new ContextMenu();
                    var miMarkDelete = new MenuItem { Header = "✗ Mark for deletion", Tag = file };
                    var miMarkKeep   = new MenuItem { Header = "✓ Keep this file",    Tag = file };
                    var miOpen       = new MenuItem { Header = "Open folder",          Tag = file };
                    miMarkDelete.Click += (s, _) => { file.IsMarkedForDeletion = true;  cb.IsChecked = true;  };
                    miMarkKeep.Click   += (s, _) => { file.IsMarkedForDeletion = false; cb.IsChecked = false; };
                    miOpen.Click       += (s, _) =>
                    {
                        try
                        {
                            string dir = System.IO.Path.GetDirectoryName(file.FullPath);
                            if (System.IO.Directory.Exists(dir))
                                System.Diagnostics.Process.Start("explorer.exe", dir);
                        }
                        catch  { AppLogger.Log(new Exception("Unhandled"), nameof(AppLogger)); }
                    };
                    ctx.Items.Add(miMarkDelete);
                    ctx.Items.Add(miMarkKeep);
                    ctx.Items.Add(new Separator());
                    ctx.Items.Add(miOpen);
                    row.ContextMenu = ctx;

                    var fileNode = new TreeViewItem { Header = row };
                    fileNode.SetResourceReference(TreeViewItem.ForegroundProperty, "TextPrimaryBrush");
                    groupNode.Items.Add(fileNode);
                }
                DuplicateTree.Items.Add(groupNode);
            }
        }

        private void DupFile_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is DuplicateFile file)
                file.IsMarkedForDeletion = cb.IsChecked == true;
        }
    }
}
