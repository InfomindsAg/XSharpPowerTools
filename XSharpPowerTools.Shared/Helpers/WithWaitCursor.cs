using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace XSharpPowerTools.Helpers
{
    class WithWaitCursor : IDisposable
    {
        private readonly bool _CursorAvailable;
        private Cursor _PreviousCursor;

        /// <summary>
        /// Mouse.OverrideCursor builds WPF's input manager for the calling thread and therefore throws
        /// an InvalidOperationException on anything but an STA thread with a dispatcher. A wait cursor
        /// is cosmetic, so off the UI thread it is skipped instead of failing its caller.
        /// </summary>
        private static bool CanOverrideCursor =>
            Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
            && Dispatcher.FromThread(Thread.CurrentThread) != null;

        public WithWaitCursor()
        {
            _CursorAvailable = CanOverrideCursor;

            if (!_CursorAvailable)
                return;

            _PreviousCursor = Mouse.OverrideCursor;

            Mouse.OverrideCursor = Cursors.Wait;
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_CursorAvailable)
                Mouse.OverrideCursor = _PreviousCursor;
        }

        #endregion IDisposable Members    }
    }
}