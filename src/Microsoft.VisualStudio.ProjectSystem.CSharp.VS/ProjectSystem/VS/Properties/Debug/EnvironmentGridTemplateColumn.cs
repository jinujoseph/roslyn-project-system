//--------------------------------------------------------------------------------------------
// EnvironmentDataGridTemplateColumn
//
// Select the content of a textbox when preparing the cell for editting.
// 
// Copyright(c) 2015 Microsoft Corporation
//--------------------------------------------------------------------------------------------
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    public class EnvironmentDataGridTemplateColumn : DataGridTemplateColumn
    {
        protected override object PrepareCellForEdit(FrameworkElement frameworkElement, RoutedEventArgs routedEventArgs)
        {
            if (frameworkElement != null && frameworkElement is ContentPresenter)
            {
                var contentPresenter = frameworkElement as ContentPresenter;
                var textBox = WpfHelper.GetVisualChild<TextBox>(contentPresenter);
                if (textBox != null)
                {
                    textBox.SelectAll();
                }
            }
            return null;
        }
    }
}
