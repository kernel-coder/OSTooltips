using System;
using System.Windows.Threading;

namespace Silverlight.Controls.ToolTips
{
    internal sealed class ToolTipTimer : DispatcherTimer
    {
        private const int timerInterval = 50;

        /// <summary>
        /// This event occurs when the timer has stopped.
        /// </summary>
        public event EventHandler Stopped;

        public ToolTipTimer(TimeSpan maximumTicks, TimeSpan initialDelay)
        {
            InitialDelay = initialDelay;
            MaximumTicks = maximumTicks;
            Interval = TimeSpan.FromMilliseconds(timerInterval);
            Tick += OnTick;
        }

        /// <summary>
        /// Stops the ToolTipTimer.
        /// </summary>
        public new void Stop()
        {
            base.Stop();
            if (Stopped != null)
            {
                Stopped(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Resets the ToolTipTimer and starts it.
        /// </summary>
        public void StartAndReset()
        {
            CurrentTick = 0;
            Start();
        }

        /// <summary>
        /// Stops the ToolTipTimer and resets its tick count.
        /// </summary>
        public void StopAndReset()
        {
            Stop();
            CurrentTick = 0;
        }

        /// <summary>
        /// Gets the maximum number of seconds for this timer.
        /// When the maximum number of ticks is hit, the timer will stop itself.
        /// </summary>
        /// <remarks>The minimum number of seconds is 1.</remarks>
        public TimeSpan MaximumTicks { get; private set; }

        /// <summary>
        /// Gets the initial delay for this timer in seconds.
        /// When the maximum number of ticks is hit, the timer will stop itself.
        /// </summary>
        /// <remarks>The default delay is 0 seconds.</remarks>
        public TimeSpan InitialDelay { get; private set; }

        /// <summary>
        /// Gets the current tick of the ToolTipTimer.
        /// </summary>
        public int CurrentTick { get; private set; }

        private void OnTick(object sender, EventArgs e)
        {
            CurrentTick += timerInterval;
            if (CurrentTick >= (MaximumTicks.TotalMilliseconds + InitialDelay.TotalMilliseconds))
            {
                Stop();
            }
        }
    }
}
