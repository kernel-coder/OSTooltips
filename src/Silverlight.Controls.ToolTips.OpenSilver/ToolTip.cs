using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Animation;
using System.Diagnostics;

namespace Silverlight.Controls.ToolTips
{
    /// <summary>
    /// Represents a control that creates a pop-up window that displays information for an element in the UI.
    /// </summary>
    public class ToolTip : System.Windows.Controls.ToolTip, INotifyPropertyChanged
    {
        ///<summary>
        /// This event is raised when the OpenAnimation storyboard has been started by the ToolTipService.
        ///</summary>
        public event EventHandler OpenAnimationStarted;

        ///<summary>
        /// This event is raised when the CloseAnimation storyboard has been started by the ToolTipService.
        ///</summary>
        public event EventHandler CloseAnimationStarted;

        private const string errorMessageNotAToolTipObject = "You can only set {0} on a ToolTip object.";

        /// <summary>
        /// Identifies the ToolTip.DisplayTime dependency property.
        /// </summary>
        /// <remarks>Default value is 5 seconds.</remarks>
        public static readonly DependencyProperty DisplayTimeProperty
            = DependencyProperty.RegisterAttached("DisplayTime", typeof(Duration), typeof(ToolTip),
                                                  new PropertyMetadata(new Duration(TimeSpan.FromSeconds(5)), OnDisplayTimePropertyChanged));

        /// <summary>
        /// Identifies the ToolTip.InitialDelay dependency property.
        /// </summary>
        /// <remarks>Default value is 1 second.</remarks>
        public static readonly DependencyProperty InitialDelayProperty
            = DependencyProperty.RegisterAttached("InitialDelay", typeof(Duration), typeof(ToolTip),
                                                  new PropertyMetadata(new Duration(TimeSpan.FromSeconds(1)), OnInitialDelayPropertyChanged));
        /// <summary>
        /// Identifies the ToolTip.CloseAnimation dependency property.
        /// </summary>
        /// <remarks>Default value is null.</remarks>
        public static readonly DependencyProperty CloseAnimationProperty
            = DependencyProperty.RegisterAttached("CloseAnimation", typeof(Storyboard), typeof(ToolTip),
                                                  new PropertyMetadata(null, OnCloseAnimationPropertyChanged));

        /// <summary>
        /// Identifies the ToolTip.OpenAnimation dependency property.
        /// </summary>
        /// <remarks>Default value is null.</remarks>
        public static readonly DependencyProperty OpenAnimationProperty
            = DependencyProperty.RegisterAttached("OpenAnimation", typeof(Storyboard), typeof(ToolTip),
                                                  new PropertyMetadata(null, OnOpenAnimationPropertyChanged));
        /// <summary>
        /// The FrameworkElement owner of the ToolTip
        /// </summary>        
        public FrameworkElement Owner { get; private set; }
        internal ToolTipTimer Timer { get; private set; }

        private static void OnCloseAnimationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is ToolTip))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, errorMessageNotAToolTipObject, "ToolTip.CloseAnimationProperty"));
            }
        }
        private static void OnOpenAnimationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is ToolTip))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, errorMessageNotAToolTipObject, "ToolTip.OpenAnimationProperty"));
            }
        }
        private static void OnInitialDelayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var toolTip = (ToolTip)d;

            if (toolTip == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, errorMessageNotAToolTipObject, "ToolTip.DisplayTimeProperty"));
            }

            UpdateToolTipTimer(toolTip);
        }
        private static void OnDisplayTimePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var toolTip = (ToolTip)d;

            if (toolTip == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, errorMessageNotAToolTipObject, "ToolTip.DisplayTimeProperty"));
            }

            UpdateToolTipTimer(toolTip);
        }

        private static void UpdateToolTipTimer(ToolTip toolTip)
        {
            toolTip.SetToolTipTimer();
        }

        internal void SetToolTipTimer()
        {
            // substract the duration of the close animation from the display time,
            // to match with the value set by the user in the ToolTip.DisplayTime property
            // var tooltipDisplayDuration = DisplayTime;
            // if (CloseAnimation != null)
            //     tooltipDisplayDuration = tooltipDisplayDuration.Subtract(CloseAnimation.Duration);

            var maximumTime = !DisplayTime.HasTimeSpan ? TimeSpan.MaxValue : DisplayTime.TimeSpan;
            var timer = new ToolTipTimer(maximumTime, InitialDelay.TimeSpan);
            if (Timer != null)
            {
                // clean up old instance
                Timer.StopAndReset();
                timer.Tick -= ToolTipService.OnTimerTick;
                timer.Stopped -= ToolTipService.OnTimerStopped;
            }
            timer.Stopped += ToolTipService.OnTimerStopped;
            timer.Tick += ToolTipService.OnTimerTick;
            Timer = timer;
        }
        internal void SetOwner(FrameworkElement owner)
        {
            Owner = owner;
            InvokePropertyChanged("Owner");
        }

        internal void InvokeOpenAnimationStarted(EventArgs e)
        {
            if (OpenAnimationStarted != null)
                OpenAnimationStarted(this, e);
        }
        internal void InvokeCloseAnimationStarted(EventArgs e)
        {
            if (CloseAnimationStarted != null)
                CloseAnimationStarted(this, e);
        }

        /// <summary>
        /// Gets or sets the display time of this ToolTip instance in seconds.
        /// </summary>
        /// <remarks>
        /// The default value is 5 seconds.
        /// </remarks>
        public Duration DisplayTime
        {
            get { return (Duration)GetValue(DisplayTimeProperty); }
            set { SetValue(DisplayTimeProperty, value); }
        }

        /// <summary>
        /// Gets or sets the Storyboard to execute when closing the ToolTip.
        /// </summary>
        public Storyboard CloseAnimation
        {
            get { return (Storyboard)GetValue(CloseAnimationProperty); }
            set { SetValue(CloseAnimationProperty, value); }
        }

        /// <summary>
        /// Gets or sets the Storyboard to execute when opening the ToolTip.
        /// </summary>
        public Storyboard OpenAnimation
        {
            get { return (Storyboard)GetValue(OpenAnimationProperty); }
            set { SetValue(OpenAnimationProperty, value); }
        }

        /// <summary>
        /// Gets or sets the initial delay for the tooltip to show in seconds.
        /// </summary>
        /// <remarks>
        /// The default value is 1 second.
        /// </remarks>
        public Duration InitialDelay
        {
            get { return (Duration)GetValue(InitialDelayProperty); }
            set { SetValue(InitialDelayProperty, value); }
        }

        #region INotifyPropertyChanged event

        ///<summary>
        ///Occurs when a property value changes.
        ///</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for
        /// a given property.
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        protected void InvokePropertyChanged(string propertyName)
        {
            //validate the property name in debug builds
            VerifyProperty(propertyName);

            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// Verifies whether the current class provides a property with a given
        /// name. This method is only invoked in debug builds, and results in
        /// a runtime exception if the <see cref="InvokePropertyChanged"/> method
        /// is being invoked with an invalid property name. This may happen if
        /// a property's name was changed but not the parameter of the property's
        /// invocation of <see cref="InvokePropertyChanged"/>.
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        [Conditional("DEBUG")]
        private void VerifyProperty(string propertyName)
        {
            var type = GetType();

            //look for a *public* property with the specified name
            var pi = type.GetProperty(propertyName);

            //there is no matching property - notify the developer
            var msg = "InvokePropertyChanged was invoked with invalid property name {0}: ";
            msg += "{0} is not a public property of {1}.";
            msg = String.Format(msg, propertyName, type.FullName);

            Debug.Assert(pi != null, msg);
        }

        #endregion
    }
}
