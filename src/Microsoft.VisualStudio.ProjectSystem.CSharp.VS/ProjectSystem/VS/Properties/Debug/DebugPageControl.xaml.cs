using Microsoft.VisualStudio.ProjectSystem.VS.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    /// <summary>
    /// Interaction logic for DebugPageControl.xaml
    /// </summary>
    //[Guid("0273C280-1882-4ED0-9308-52914672E3AA")]
    internal partial class DebugPageControl : PropertyPageControl
    {
        public DebugPageControl()
        {
            InitializeComponent();
            this.DataContextChanged += DebugPageControlControl_DataContextChanged;
        }

        void DebugPageControlControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue != null && e.OldValue is DebugPageViewModel)
            {
                DebugPageViewModel viewModel = e.OldValue as DebugPageViewModel;
                viewModel.FocusEnvironmentVariablesGridRow -= OnFocusEnvironmentVariableGridRow;
                viewModel.ClearEnvironmentVariablesGridError -= OnClearEnvironmentVariableGridError;
            }

            if (e.NewValue != null && e.NewValue is DebugPageViewModel)
            {
                DebugPageViewModel viewModel = e.NewValue as DebugPageViewModel;
                viewModel.FocusEnvironmentVariablesGridRow += OnFocusEnvironmentVariableGridRow;
                viewModel.ClearEnvironmentVariablesGridError += OnClearEnvironmentVariableGridError;
            }
        }

        
        private void OnClearEnvironmentVariableGridError(object sender, EventArgs e)
        {
            //ClearGridError(dataGridEnvironmentVariables);
        }

        private void OnFocusEnvironmentVariableGridRow(object sender, EventArgs e)
        {
            /*if (this.DataContext != null && this.DataContext is DebugPageViewModel)
            {
                this.Dispatcher.BeginInvoke(new DispatcherOperationCallback((param) =>
                {
                    if ((this.DataContext as DebugPageViewModel).EnvironmentVariables.Count > 0)
                    {
                        // get the new cell, set focus, then open for edit
                        var cell = WpfHelper.GetCell(dataGridEnvironmentVariables, (this.DataContext as DebugPageViewModel).EnvironmentVariables.Count - 1, 0);
                        cell.Focus();
                        dataGridEnvironmentVariables.BeginEdit();
                    }
                    return null;
                }), DispatcherPriority.Background, new object[] { null });
            }*/
        }

    }
}