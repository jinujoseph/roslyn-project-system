using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Controls;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem.VS.Extensibility;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    internal abstract partial class WpfBasedPropertyPage : PropertyPage
    {
        private PropertyPageElementHost host;
        private PropertyPageControl control;
        private PropertyPageViewModel viewModel;
        private readonly UnconfiguredProject _unconfiguredProject;
        private readonly IProjectThreadingService _threadHandling;
               

        public WpfBasedPropertyPage()
        {
            InitializeComponent();
        }

        // For unit testing
        internal WpfBasedPropertyPage(bool useJoinableTaskFactory) : base(useJoinableTaskFactory)
        {
            InitializeComponent();
        }

        protected abstract PropertyPageViewModel CreatePropertyPageViewModel();

        protected abstract PropertyPageControl CreatePropertyPageControl();

        protected async override Task OnSetObjects(bool isClosing)
        {
            if (isClosing)
            {
                control.DetachViewModel();
                return;
            }
            else
            {
                //viewModel can be non-null when the configuration is chaged. 
                if (control == null)
                {
                    control = CreatePropertyPageControl();
                }
            }

            viewModel = CreatePropertyPageViewModel();
            viewModel.UnconfiguredDotNetProject = UnconfiguredDotNetProject;
            viewModel.UnconfiguredProperties = UnconfiguredProperties;
            viewModel.ConfiguredProperties = ConfiguredProperties;
            await viewModel.Initialize();
            control.InitializePropertyPage(viewModel);
        }

        protected async override Task<int> OnApply()
        {
            return await control.Apply();
        }

        protected async override Task OnDeactivate()
        {
            if (IsDirty)
            {
                await OnApply();
            }
        }

        private void WpfPropertyPage_Load(object sender, EventArgs e)
        {
            SuspendLayout();

            host = new PropertyPageElementHost();
            host.AutoSize = false;
            host.Dock = DockStyle.Fill;

            if (control == null)
            {
                control = CreatePropertyPageControl();
            }

            ScrollViewer viewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            viewer.Content = control;
            host.Child = viewer;

            wpfHostPanel.Dock = DockStyle.Fill;
            wpfHostPanel.Controls.Add(host);

            ResumeLayout(true);
            control.StatusChanged += _control_OnControlStatusChanged;

        }

        private void _control_OnControlStatusChanged(object sender, EventArgs e)
        {
            if (IsDirty != control.IsDirty)
            {
                IsDirty = control.IsDirty;
            }
        }
    }
}

