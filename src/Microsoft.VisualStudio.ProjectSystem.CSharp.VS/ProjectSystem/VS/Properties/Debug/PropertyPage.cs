using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Collections.Generic;
using Microsoft.VisualStudio.ProjectSystem.VS;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    public abstract partial class PropertyPage : UserControl, IPropertyPage,IVsDebuggerEvents
    {
        private IPropertyPageSite _site = null;
        private bool _isDirty = false;
        private bool _ignoreEvents = false;
        private bool _useJoinableTaskFactory = true;
        private IVsDebugger _debugger;
        private uint _debuggerCookie;

        // WIN32 Constants
        private const int
            WM_KEYFIRST = 0x0100,
            WM_KEYLAST = 0x0108,
            WM_MOUSEFIRST = 0x0200,
            WM_MOUSELAST = 0x020A,
            SW_HIDE = 0;

        internal static class NativeMethods
        {
            public const int
                S_OK = 0x00000000;
        }

        protected abstract string PropertyPageName { get; }

        internal PropertyPage()
        {
            this.AutoScroll = false;
        }

        // For unit testing
        internal PropertyPage(bool useJoinableTaskFactory)
        {
            _useJoinableTaskFactory = useJoinableTaskFactory;
        }

        internal IPropertyProvider UnconfiguredProperties { get; set; }
        internal IPropertyProvider[] ConfiguredProperties { get; set; }
        internal IUnconfiguredProjectVsServices UnconfiguredDotNetProject { get; set; }
        //internal IDotNetThreadHandling ThreadHandling { get; set; }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// Property. Gets or sets whether the page is dirty. Dirty status is pushed to owner property sheet
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        protected bool IsDirty
        {
            get { return _isDirty; }
            set
            {
                // Only process real changes
                if (value != _isDirty && !_ignoreEvents)
                {
                    _isDirty = value;
                    // If dirty, this causes Apply to be called
                    if (_site != null)
                        _site.OnStatusChange((uint)(this._isDirty ? PROPPAGESTATUS.PROPPAGESTATUS_DIRTY : PROPPAGESTATUS.PROPPAGESTATUS_CLEAN));
                }
            }
        }

        /// <summary>
        /// Helper to wait on async tasks
        /// </summary>
        private T WaitForAsync<T>(Func<Task<T>> asyncFunc)
        {
            if (!_useJoinableTaskFactory)
            {
                // internal test usage
                Task<T> t = asyncFunc();
                return t.Result;
            }
            //Debug.Assert(ThreadHandling != null);
            return UnconfiguredDotNetProject.ThreadingService.ExecuteSynchronously<T>(asyncFunc);
        }

        /// <summary>
        /// Helper to wait on async tasks
        /// </summary>
        private void WaitForAsync(Func<Task> asyncFunc)
        {
            // Real VS execution
            if (!_useJoinableTaskFactory)
            {
                // internal test usage
                asyncFunc().Wait();
                return;
            }

            //Debug.Assert(ThreadHandling != null);
            //UnconfiguredDotNetProject.ThreadingService.ExecuteSynchronously(asyncFunc);
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// IPropertyPage
        /// This is called before our form is shown but after SetObjects is called.
        /// This is the place from which the form can populate itself using the information available
        /// in CPS.
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public void Activate(IntPtr hWndParent, RECT[] pRect, int bModal)
        {
            AdviseDebugger();
            this.SuspendLayout();
            // Initialization can cause some events to be fired when we change some values
            // so we use this flag (_ignoreEvents) to notify IsDirty to ignore
            // any changes that happen during initialization
            Win32Methods.SetParent(this.Handle, hWndParent);
            this.ResumeLayout();

        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// IPropertyPage
        /// This is where the information entered in the form should be saved in CPS
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public int Apply()
        {
            return WaitForAsync<int>(OnApply);
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// IPropertyPage
        /// Called when the page is deactivated
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public void Deactivate()
        {
            WaitForAsync(OnDeactivate);
            UnadviseDebugger();
            Dispose(true);
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// IPropertyPage
        /// Returns a stuct describing our property page
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public void GetPageInfo(PROPPAGEINFO[] pPageInfo)
        {
            PROPPAGEINFO info = new PROPPAGEINFO();

            info.cb = (uint)Marshal.SizeOf(typeof(PROPPAGEINFO));
            info.dwHelpContext = 0;
            info.pszDocString = null;
            info.pszHelpFile = null;
            info.pszTitle = this.PropertyPageName;
            // set the size to 0 so the host doesn't use scroll bars
            // we want to do that within our own container.
            info.SIZE.cx = 0;
            info.SIZE.cy = 0;
            if (pPageInfo != null && pPageInfo.Length > 0)
                pPageInfo[0] = info;
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// IPropertyPage
        /// Returns the help context
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public void Help(string pszHelpDir)
        {
            return;
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// IPropertyPage
        //  Return S_OK for Dirty, S_FALSE for not dirty
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public int IsPageDirty()
        {
            if (IsDirty)
                return VSConstants.S_OK;
            return VSConstants.S_FALSE;
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// IPropertyPage
        //  Called when the page is moved or sized
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public new void Move(Microsoft.VisualStudio.OLE.Interop.RECT[] pRect)
        {
            if (pRect == null || pRect.Length <= 0)
                throw new ArgumentNullException("pRect");

            Microsoft.VisualStudio.OLE.Interop.RECT r = pRect[0];

            this.Location = new Point(r.left, r.top);
            this.Size = new Size(r.right - r.left, r.bottom - r.top);
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// Notification that debug mode changed
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public int OnModeChange(DBGMODE dbgmodeNew)
        {
            Enabled = (dbgmodeNew == DBGMODE.DBGMODE_Design);
            return NativeMethods.S_OK;
        }

        /// <summary>
        /// Get the unconfigured property provider for the project
        /// </summary>
        internal virtual UnconfiguredProjectVsServices GetUnconfiguredDotNetProject(IVsHierarchy hier)
        {
            //var provider = PropertyProviderBase.GetExport<IProjectExportProvider>(hier);
            return null; //  provider.GetExport<UnconfiguredProjectVsServices>(hier);
        }

        /// <summary>
        /// Get the configured property provider for the project
        /// </summary>
        internal virtual IPropertyProvider GetConfiguredPropertyProvider(IVsHierarchy hier, string configurationName)
        {
            // return ConfiguredPropertyProvider.GetProvider(hier, configurationName);
            return null;
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// IPropertyPage
        /// This should query the IUnknown for the Interfaces we may be interested in. If called with 0, then we need to release those interfaces
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public void SetObjects(uint cObjects, object[] ppunk)
        {
            // If asked to, release our cached selected Project object(s)
            UnconfiguredProperties = null;
            ConfiguredProperties = null;
            UnconfiguredDotNetProject = null;
            if (cObjects == 0)
            {
                // If we have never configured anything (maybe a failure occurred on open so app designer is closing us). In this case
                // do nothing
                //if (ThreadHandling != null)
                {
                    SetObjects(true);
                }
                return;
            }

            if (ppunk.Length < cObjects)
                throw new ArgumentOutOfRangeException("cObjects");

            List<IPropertyProvider> configuredProperties = new List<IPropertyProvider>();
            List<string> configurations = new List<string>();
            // Look for an IVsBrowseObject
            for (int i = 0; i < cObjects; ++i)
            {
                IVsBrowseObject browseObj = null;
                browseObj = ppunk[i] as IVsBrowseObject;

                if (browseObj != null)
                {
                    IVsHierarchy hier = null;
                    uint itemid;
                    int hr;
                    hr = browseObj.GetProjectItem(out hier, out itemid);
                    //Debug.Assert(itemid == VSConstants.VSITEMID_ROOT, "Selected object should be project root node");

                    if (hr == VSConstants.S_OK && itemid == VSConstants.VSITEMID_ROOT)
                    {
                        UnconfiguredDotNetProject = GetUnconfiguredDotNetProject(hier);
                        //UnconfiguredProperties = UnconfiguredDotNetProject.PropertyProvider;

                        // We need to save ThreadHandling because the appdesigner will call SetObjects with null, and then call
                        // Deactivate(). We need to run Async code during Deactivate() which requires ThreadHandling.
                        //ThreadHandling = UnconfiguredDotNetProject.ThreadingService;

                        IVsProjectCfg2 pcg = ppunk[i] as IVsProjectCfg2;
                        if (pcg != null)
                        {
                            string configName;
                            pcg.get_CanonicalName(out configName);

                            var provider = GetConfiguredPropertyProvider(hier, configName);
                            if (provider != null)
                            {
                                configuredProperties.Add(provider);
                            }
                        }
                    }
                }

                ConfiguredProperties = configuredProperties.ToArray();
            }

            SetObjects(false);
        }

        /// <summary>
        /// Informs derived classes that configuration has changed
        /// </summary>
        internal void SetObjects(bool isClosing)
        {
            WaitForAsync(async () => await OnSetObjects(isClosing));
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// IPropertyPage
        /// Site for our page
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public void SetPageSite(IPropertyPageSite pPageSite)
        {
            _site = pPageSite;
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// IPropertyPage
        /// Show/Hide the page
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public void Show(uint nCmdShow)
        {
            if (nCmdShow != SW_HIDE)
            {
                this.Show();
            }
            else
            {
                this.Hide();
            }
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// IPropertyPage
        /// Handles mneumonics
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        public int TranslateAccelerator(MSG[] pMsg)
        {
            if (pMsg == null)
                return VSConstants.E_POINTER;

            System.Windows.Forms.Message m = System.Windows.Forms.Message.Create(pMsg[0].hwnd, (int)pMsg[0].message, pMsg[0].wParam, pMsg[0].lParam);
            bool used = false;

            // Preprocessing should be passed to the control whose handle the message refers to.
            Control target = Control.FromChildHandle(m.HWnd);
            if (target != null)
                used = target.PreProcessMessage(ref m);

            if (used)
            {
                pMsg[0].message = (uint)m.Msg;
                pMsg[0].wParam = m.WParam;
                pMsg[0].lParam = m.LParam;
                // Returning S_OK indicates we handled the message ourselves
                return VSConstants.S_OK;
            }


            // Returning S_FALSE indicates we have not handled the message
            int result = 0;
            if (this._site != null)
                result = _site.TranslateAccelerator(pMsg);
            return result;
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// Initialize and listen to debug mode changes
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        internal void AdviseDebugger()
        {
            System.IServiceProvider sp = _site as System.IServiceProvider;
            if (sp != null)
            {
                //_debugger = sp.GetService<IVsDebugger, IVsDebugger>();
                if (_debugger != null)
                {
                    _debugger.AdviseDebuggerEvents(this, out _debuggerCookie);
                    DBGMODE[] dbgMode = new DBGMODE[1];
                    _debugger.GetMode(dbgMode);
                    ((IVsDebuggerEvents)this).OnModeChange(dbgMode[0]);
                }
            }
        }

        ///--------------------------------------------------------------------------------------------
        /// <summary>
        /// Quit listening to debug mode changes
        /// </summary>
        ///--------------------------------------------------------------------------------------------
        private void UnadviseDebugger()
        {
            if (_debuggerCookie != 0 && _debugger != null)
            {
                _debugger.UnadviseDebuggerEvents(_debuggerCookie);
            }
            _debugger = null;
            _debuggerCookie = 0;
        }
        protected abstract Task<int> OnApply();
        protected abstract Task OnDeactivate();
        protected abstract Task OnSetObjects(bool isClosing);
    }
}