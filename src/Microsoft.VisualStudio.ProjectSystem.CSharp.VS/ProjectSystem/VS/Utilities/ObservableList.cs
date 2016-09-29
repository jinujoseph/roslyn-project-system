using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities
{
    internal class ObservableList<T> : ObservableCollection<T>
    {
        public event EventHandler ValidationStatusChanged;

        public ObservableList()
        {

        }

        public ObservableList(List<T> list)
        {
            foreach (T item in list)
            {
                this.Add(item);
            }

            foreach (INotifyPropertyChanged item in list)
                item.PropertyChanged += item_PropertyChanged;
        }

        void item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(new PropertyChangedEventArgs("Items"));
        }

        public void RaiseValidationStatus(bool validationSuccessful)
        {
            if (ValidationStatusChanged == null) return;
            ValidationStatusChanged(this, new ValidationStatusChangedEventArgs(validationSuccessful));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            HasChanged = true;
            // Reset Event Handlers
            if (e != null)
            {
                if (e.OldItems != null)
                    foreach (INotifyPropertyChanged item in e.OldItems)
                        item.PropertyChanged -= item_PropertyChanged;

                if (e.NewItems != null)
                    foreach (INotifyPropertyChanged item in e.NewItems)
                        item.PropertyChanged += item_PropertyChanged;
            }

            base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            HasChanged = true;
            base.OnPropertyChanged(e);
        }

        internal List<T> ToList()
        {
            return new List<T>(Items);
        }

        public bool HasChanged { get; set; }
    }

    public class ObservableCollectionEventArgs : PropertyChangedEventArgs
    {
        public bool ValidationSuccessful { get; set; }

        public ObservableCollectionEventArgs(string propertyName, bool validationSuccessful)
            : base(propertyName)
        {
            ValidationSuccessful = validationSuccessful;
        }
    }

    public class ValidationStatusChangedEventArgs : EventArgs
    {
        public bool ValidationStatus { get; set; }
        public ValidationStatusChangedEventArgs(bool validationStatus)
        {
            ValidationStatus = validationStatus;
        }
    }
}
