//--------------------------------------------------------------------------------------------
// PropertyPageControl
//
// Base WPF-based UserControl class which implements loading/saving from a view model
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    internal class PropertyPageControl : UserControl
    {
        private bool isDirty;
        private bool ignoreEvents;

        public PropertyPageControl()
        {
        }

        public event EventHandler StatusChanged;

        public PropertyPageViewModel ViewModel
        {
            get
            {
                return DataContext as PropertyPageViewModel;
            }
            set
            {
                DataContext = value;
            }
        }

        public bool IsDirty
        {
            get { return isDirty; }
            set
            {
                // Only process real changes
                if (value != isDirty && !ignoreEvents)
                {
                    isDirty = value;
                    OnStatusChanged(new EventArgs());
                }
            }
        }

        public virtual void InitializePropertyPage(PropertyPageViewModel viewModel)
        {
            ignoreEvents = true;
            IsDirty = false;
            ViewModel = viewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.ParentControl = this;
            ignoreEvents = false;
        }

        public virtual void DetachViewModel()
        {
            ignoreEvents = true;
            IsDirty = false;
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

            // Let the view model know we are done.
            ViewModel.ViewModelDetached();
            ViewModel.ParentControl = null;
            ViewModel = null;
            ignoreEvents = false;
        }

        public async Task<int> Apply()
        {
            int result = VSConstants.S_OK;

            if (IsDirty)
            {
                result = await OnApply();
                if (result == VSConstants.S_OK)
                {
                    IsDirty = false;
                }
            }

            return result;
        }

        protected virtual Task<int> OnApply() { return ViewModel.Save(); }
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!ignoreEvents && !ViewModel.IgnoreEvents)
            {
                IsDirty = true;
            }
        }

        protected virtual void OnStatusChanged(EventArgs args)
        {
            EventHandler handler = StatusChanged;
            if (handler != null)
            {
                handler.Invoke(this, args);
            }
        }
    }
}
