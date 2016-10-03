//--------------------------------------------------------------------------------------------
// DebugPageControl
//
// Interaction logic for DebugPageControl.xaml
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    /// <summary>
    /// Interaction logic for DebugPageControl.xaml
    /// </summary>
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

        private void NumberValidation(object sender, TextCompositionEventArgs e)
        {
            var textbox = sender as TextBox;
            e.Handled = !e.Text.IsDigital();
        }

        private void OnClearEnvironmentVariableGridError(object sender, EventArgs e)
        {
            ClearGridError(dataGridEnvironmentVariables);
        }

        private void OnFocusEnvironmentVariableGridRow(object sender, EventArgs e)
        {
            if (this.DataContext != null && this.DataContext is DebugPageViewModel)
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
            }
        }

        private void ClearGridError(DataGrid dataGrid)
        {
            try
            {
                BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
                PropertyInfo cellErrorInfo = dataGrid.GetType().GetProperty("HasCellValidationError", bindingFlags);
                PropertyInfo rowErrorInfo = dataGrid.GetType().GetProperty("HasRowValidationError", bindingFlags);
                cellErrorInfo.SetValue(dataGrid, false, null);
                rowErrorInfo.SetValue(dataGrid, false, null);
            }
            catch (Exception) { /* Silently eat up exception => Worstcase user need to refresh the document window*/ }
        }
    }
}
