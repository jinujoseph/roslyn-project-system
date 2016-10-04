//--------------------------------------------------------------------------------------------
// GetProfileNameDialog.xaml.cs
//
// Prompts the user for an item name
//
// Copyright(c) 2015 Microsoft Corporation
//--------------------------------------------------------------------------------------------
using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.ProjectSystem.VS;
using Microsoft.VisualStudio.Shell;
using resources= Microsoft.VisualStudio.Resources;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    [ExcludeFromCodeCoverage]
    public partial class GetProfileNameDialog : DialogWindow
    {
        private Predicate<string> Validator { get; set; }
        public string ProfileName { get; set; }
        private SVsServiceProvider _serviceProvider { get; set; }
        private IProjectThreadingService _threadingService { get; set; }

        public GetProfileNameDialog(SVsServiceProvider sp, IProjectThreadingService threadingService, string suggestedName, Predicate<string> validator)
            : base()// Pass help topic to base if there is one
        {
            InitializeComponent();
            DataContext = this;
            ProfileName = suggestedName;
            Validator = validator;
            _serviceProvider = sp;
            _threadingService = threadingService;
        }

        //------------------------------------------------------------------------------
        // Validate the name is valid
        //------------------------------------------------------------------------------
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            string newName = ProfileName;
            newName = newName?.Trim();
            UserNotificationServices notifyService = new UserNotificationServices(_serviceProvider, _threadingService);

            if (string.IsNullOrEmpty(newName))
            {
                notifyService.ShowMessageBox(resources.ProfileNameRequired, null,  OLEMSGICON.OLEMSGICON_CRITICAL, 
                                                  OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            else if (!Validator(newName))
            {
                notifyService.ShowMessageBox(resources.ProfileNameInvalid, null,  OLEMSGICON.OLEMSGICON_CRITICAL, 
                                                  OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            else
            {
                ProfileName = newName;
                DialogResult = true;
            }
        }

        //------------------------------------------------------------------------------
        // Returns the name of the current product we are instantiated in from the appropriate resource
        // Used for dialog title binding
        //------------------------------------------------------------------------------
        public string DialogCaption
        {
            get
            {
                return resources.NewProfileCaption;
            }
        }
        //------------------------------------------------------------------------------
        // Called when window loads. Use it to set focus on the text box correctly.
        //------------------------------------------------------------------------------
        delegate void SetFocusCallback();
        private void GetProfileNameDialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // We need to schedule this to occur later after databinding has completed, otherwise
            // focus appears in the textbox, but at the start of the suggested name rather than at
            // the end.
            Dispatcher.BeginInvoke(
                    (SetFocusCallback)delegate ()
                    {
                        ProfileNameTextBox.Select(0, ProfileNameTextBox.Text.Length);
                        ProfileNameTextBox.Focus();
                    }, System.Windows.Threading.DispatcherPriority.DataBind, null);
        }
    }
}


