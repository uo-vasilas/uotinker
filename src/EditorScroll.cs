using System.Drawing;

namespace UOTinker;

public static class EditorScroll
{
    public static void Attach(UserControl host)
    {
        host.AutoScroll = true;
        bool busy = false;

        void Fit()
        {
            if (busy || !host.IsHandleCreated)
            {
                return;
            }
            busy = true;
            try
            {
                host.AutoScrollMinSize = Size.Empty;
                host.PerformLayout();
                int h = 0;
                foreach (Control c in host.Controls)
                {
                    if (c.Dock is DockStyle.Top or DockStyle.Bottom)
                    {
                        h += c.Height;
                    }
                }
                host.AutoScrollMinSize = new Size(0, h);
            }
            finally
            {
                busy = false;
            }
        }

        host.SizeChanged += (_, _) => Fit();
        host.HandleCreated += (_, _) => host.BeginInvoke(Fit);
        host.VisibleChanged += (_, _) => Fit();
    }
}
