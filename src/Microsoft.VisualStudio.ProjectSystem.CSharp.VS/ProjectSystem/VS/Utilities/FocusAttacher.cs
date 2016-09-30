//--------------------------------------------------------------------------------------------
// FocusAttacher
//
// Allows instant focus on editable fields in DataGrid control
//
// Copyright(c) 2015 Microsoft Corporation
//--------------------------------------------------------------------------------------------

using System.Windows;
using System.Windows.Controls;

namespace Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities
{
    public class FocusAttacher
    {
        public static readonly DependencyProperty FocusProperty = DependencyProperty.RegisterAttached("Focus", typeof(bool), typeof(FocusAttacher), new PropertyMetadata(false, FocusChanged));
        public static bool GetFocus(DependencyObject d)
        {
            return (bool)d.GetValue(FocusProperty);
        }

        public static void SetFocus(DependencyObject d, bool value)
        {
            d.SetValue(FocusProperty, value);
        }

        private static void FocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                ((UIElement)sender).Focus();
                TextBox tb = sender as TextBox;
                if (tb != null)
                {
                    tb.SelectAll();
                }
            }
        }
    }
}
