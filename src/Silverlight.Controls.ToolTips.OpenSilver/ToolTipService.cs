using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Silverlight.Controls.ToolTips
{
    /// <summary>
    /// Represents a service that provides static methods to display a tooltip.
    /// </summary>
    public static class ToolTipService
    {
        private static readonly Dictionary<DependencyObject, ToolTip> elementsAndToolTips = new Dictionary<DependencyObject, ToolTip>();

        private static UIElement currentElement;
        private static FrameworkElement rootVisual;
        private static Size lastSize;
        private static readonly object locker = new object();
        private static bool isCloseAnimationInProgress;
        private static bool isOpenAnimationInProgress;

        #region Attached Dependency Properties

        ///<summary>
        /// An attached dependency property for the Placement of a ToolTip.
        ///</summary>
        public static readonly DependencyProperty PlacementProperty =
            DependencyProperty.RegisterAttached("Placement", typeof(PlacementMode), typeof(ToolTipService), new PropertyMetadata(PlacementMode.Mouse));

        ///<summary>
        /// An attached DependencyProperty for the PlacementTarget of a ToolTip.
        ///</summary>
        public static readonly DependencyProperty PlacementTargetProperty =
            DependencyProperty.RegisterAttached("PlacementTarget", typeof(UIElement), typeof(ToolTipService), new PropertyMetadata(null));

        #region DataContext Dependency Property

        /// <summary>
        /// Hidden dependency property that enables us to receive notifications when the source data context changes and 
        /// needs to be flushed into the context of the tooltip
        /// </summary>
        private static readonly DependencyProperty dataContextProperty =
            DependencyProperty.RegisterAttached("DataContext", typeof(object), typeof(ToolTipService), new PropertyMetadata(new PropertyChangedCallback(OnDataContextChanged)));

        /// <summary>
        /// When parent datacontext changes assign tooltip's datacontext to new datacontext
        /// </summary>
        public static void OnDataContextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var owner = sender as FrameworkElement;
            var toolTip = GetToolTip(owner);

            // make sure all non-relevant tooltips are closed!
            foreach (var kvp in elementsAndToolTips)
            {
                if (!(ReferenceEquals(kvp.Value, toolTip)))
                    kvp.Value.IsOpen = false;
            }

            Debug.Assert(!(ReferenceEquals(null, owner) ||
                           ReferenceEquals(null, toolTip)), "Unexpected null reference to attached FrameworkElement");

            if (toolTip is FrameworkElement)
            {
                ((FrameworkElement)toolTip).DataContext = owner.DataContext;
            }
        }

        #endregion DataContext Dependency Property

        #region ToolTip Dependency Property

        /// <summary>
        /// Identifies the ToolTipService.ToolTip dependency property.
        /// </summary>        
        public static readonly DependencyProperty ToolTipProperty
            = DependencyProperty.RegisterAttached("ToolTip", typeof(object), typeof(ToolTipService),
            new PropertyMetadata(new PropertyChangedCallback(OnToolTipPropertyChanged)));

        private static void OnToolTipPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var owner = (FrameworkElement)d;
            var newValue = e.NewValue;

            if (e.OldValue != null)
            {                
                UnregisterToolTip(owner);
            }
            if (newValue != null)
            {
                RegisterToolTip(owner, newValue);
            }
        }

        /// <summary>
        /// Gets the tooltip for an object.
        /// </summary>
        /// <param name="dependencyObject">The DependencyObject from which the property value is read.</param>
        public static object GetToolTip(DependencyObject dependencyObject)
        {
            return dependencyObject.GetValue(ToolTipProperty);
        }

        /// <summary>
        /// Sets the tooltip for an object.
        /// </summary>
        /// <param name="dependencyObject">The DependencyObject to which the attached property is written.</param>
        /// <param name="value">The value to set.</param>
        public static void SetToolTip(DependencyObject dependencyObject, object value)
        {
            dependencyObject.SetValue(ToolTipProperty, value);
        }

        #endregion ToolTip Depdendency Property

        #region ToolTipObject Dependency Property

        internal static readonly DependencyProperty ToolTipObjectProperty =
            DependencyProperty.RegisterAttached("ToolTipObject", typeof(object), typeof(ToolTipService), null);

        #endregion ToolTipObject Depdendency Property

        #endregion Attached Dependency Properties

        internal static Point MousePosition { get; set; }

        internal static FrameworkElement RootVisual
        {
            get
            {
                SetRootVisual();
                return rootVisual;
            }
        }

        internal static ToolTip CurrentToolTip { get; private set; }

        private static void OnElementIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!elementsAndToolTips.ContainsKey((UIElement)sender))
                return;

            var toolTipTimer = elementsAndToolTips[(UIElement)sender].Timer;
            if (!(bool)e.NewValue && toolTipTimer != null && toolTipTimer.IsEnabled)
            {
                toolTipTimer.StopAndReset();
            }
        }

        internal static void OnTimerStopped(object sender, EventArgs e)
        {
            if (CurrentToolTip == null)
                return;

            lock (locker)
            {
                if (CurrentToolTip.CloseAnimation != null)
                {
                    try
                    {
                        isCloseAnimationInProgress = true;
                        CurrentToolTip.CloseAnimation.Begin();
                        CurrentToolTip.InvokeCloseAnimationStarted(EventArgs.Empty);
                    }
                    catch (InvalidOperationException invalidOperationException)
                    {
                        Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "An exception of type {0} occured with the following message:\n{1}\nStacktrace:\n{2}",
                            invalidOperationException.GetType().Name, invalidOperationException.Message, invalidOperationException.StackTrace));
                    }
                }
                else CurrentToolTip.IsOpen = false;
            }
        }

        private static void OnElementMouseEnter(object sender, MouseEventArgs e)
        {
            MousePosition = e.GetPosition(null);
            lock (locker)
            {
                currentElement = (UIElement)sender;
                CurrentToolTip = elementsAndToolTips[currentElement];

                SetRootVisual();

                // do not trigger tooltips when there is no content defined for the tooltip
                if (CurrentToolTip.Content == null) return;

                if (CurrentToolTip.InitialDelay.TimeSpan.Ticks == 0 && CurrentToolTip.OpenAnimation == null)
                {
                    CurrentToolTip.IsOpen = true;
                }
                else if (CurrentToolTip.InitialDelay.TimeSpan.Ticks == 0 && CurrentToolTip.OpenAnimation != null)
                {
                    StartOpenAnimation();
                }

                if (isCloseAnimationInProgress && CurrentToolTip.CloseAnimation != null)
                {
                    CurrentToolTip.CloseAnimation.Stop();
                }

                if (CurrentToolTip.Timer == null)
                {
                    CurrentToolTip.SetToolTipTimer();
                }

                CurrentToolTip.Timer.StartAndReset();
            }
        }

        internal static void OnTimerTick(object sender, EventArgs e)
        {
            if (CurrentToolTip.IsOpen) return;

            var tooltipTimer = (ToolTipTimer)sender;
            if (tooltipTimer.IsEnabled && CurrentToolTip.InitialDelay.TimeSpan.TotalMilliseconds <= tooltipTimer.CurrentTick)
            {
                StartOpenAnimation();
            }
        }

        private static void StartOpenAnimation()
        {
            if (CurrentToolTip.DisplayTime.HasTimeSpan && CurrentToolTip.DisplayTime.TimeSpan.TotalSeconds == 0)
                return;

            CurrentToolTip.IsOpen = true;
            if (CurrentToolTip.OpenAnimation != null)
            {
                isOpenAnimationInProgress = true;
                CurrentToolTip.OpenAnimation.Begin();
                CurrentToolTip.InvokeOpenAnimationStarted(EventArgs.Empty);
            }
        }

        private static void OnElementMouseLeave(object sender, MouseEventArgs e)
        {
            var frameworkElement = (FrameworkElement)sender;
            var tooltip = elementsAndToolTips[frameworkElement];

            // if there is no content defined for the tooltip, nothing happened
            if (tooltip.Content == null) return;

            var toolTipTimer = tooltip.Timer;
            if (toolTipTimer != null && toolTipTimer.IsEnabled)
            {
                toolTipTimer.StopAndReset();
            }
            lock (locker)
            {
                if (GetToolTip(frameworkElement) != CurrentToolTip)
                {
                    return;
                }

                if (isOpenAnimationInProgress && CurrentToolTip.OpenAnimation != null)
                {
                    CurrentToolTip.OpenAnimation.Stop();
                }

                if (!isCloseAnimationInProgress)
                    CurrentToolTip.IsOpen = false;
            }
        }

        private static void OnRootVisualMouseMove(object sender, MouseEventArgs e)
        {
            // store the current mouse coordinates
            MousePosition = e.GetPosition(null);
        }

        private static void OnRootVisualSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (CurrentToolTip == null)
            {
                return;
            }
            if (CurrentToolTip.Parent == null)
            {
                return;
            }

            PerformPlacement(CurrentToolTip.HorizontalOffset, CurrentToolTip.VerticalOffset);
        }

        private static void OnRootVisualMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Whenever a user clicks somewhere in the application
            // and a ToolTip instance is open at that point in time,
            // the ToolTipService should trigger either the CloseAnimation
            // or set IsOpen to false
            // This simulates the standard Silverlight ToolTip behavior that closes a ToolTip upon MouseClick.
            CloseCurrentToolTip();
        }

        public static void CloseCurrentToolTip(bool skipCloseAnimation = false)
        {
            lock (locker)
            {
                if (CurrentToolTip != null)
                {
                    if (isOpenAnimationInProgress && CurrentToolTip.OpenAnimation != null)
                        CurrentToolTip.OpenAnimation.Stop();

                    if (!isCloseAnimationInProgress)
                    {
                        if (!skipCloseAnimation && CurrentToolTip.IsOpen && CurrentToolTip.CloseAnimation != null)
                            CurrentToolTip.CloseAnimation.Begin();
                        else CurrentToolTip.IsOpen = false;
                    }

                    var toolTipTimer = CurrentToolTip.Timer;
                    if (toolTipTimer != null)
                        toolTipTimer.StopAndReset();
                }
            }
        }

        private static void OnToolTipSizeChanged(object sender, SizeChangedEventArgs e)
        {
            lastSize = e.NewSize;
            if (CurrentToolTip.Parent != null)
            {
                PerformPlacement(CurrentToolTip.HorizontalOffset, CurrentToolTip.VerticalOffset);
            }
        }

        private static ToolTip ConvertToToolTip(object obj)
        {
            var toolTip = obj as ToolTip;
            if (toolTip == null)
            {
                var element = obj as FrameworkElement;
                if ((element != null) && ((toolTip = element.Parent as ToolTip) != null))
                {
                    return toolTip;
                }
                toolTip = new ToolTip { Content = obj };
            }
            return toolTip;
        }

        private static void PerformPlacement(double horizontalOffset, double verticalOffset)
        {
            PlacementMode placementMode = CurrentToolTip.Placement;
            UIElement placementTarget = CurrentToolTip.PlacementTarget;

            Popup parentPopup = (Popup)CurrentToolTip.Parent;

            switch (placementMode)
            {
                case PlacementMode.Mouse:
                    double offsetX = MousePosition.X + horizontalOffset;
                    double offsetY = MousePosition.Y + new TextBlock().FontSize + verticalOffset;

                    offsetX = Math.Max(2.0, offsetX);
                    offsetY = Math.Max(2.0, offsetY);

                    //get actual and previous dimensions
                    double actualHeight = RootVisual.ActualHeight;
                    double actualWidth = RootVisual.ActualWidth;
                    double lastHeight = lastSize.Height;
                    double lastWidth = lastSize.Width;

                    Rect lastRectangle = new Rect(offsetX, offsetY, lastWidth, lastHeight);
                    Rect actualRectangle = new Rect(0.0, 0.0, actualWidth, actualHeight);
                    actualRectangle.Intersect(lastRectangle);

                    if ((Math.Abs(actualRectangle.Width - lastRectangle.Width) < 2.0) && (Math.Abs(actualRectangle.Height - lastRectangle.Height) < 2.0))
                    {
                        parentPopup.VerticalOffset = offsetY;
                        parentPopup.HorizontalOffset = offsetX;
                    }
                    else
                    {
                        if ((offsetY + lastRectangle.Height) > actualHeight)
                        {
                            offsetY = (actualHeight - lastRectangle.Height) - 2.0;
                        }
                        if (offsetY < 0.0)
                        {
                            offsetY = 0.0;
                        }
                        if ((offsetX + lastRectangle.Width) > actualWidth)
                        {
                            offsetX = (actualWidth - lastRectangle.Width) - 2.0;
                        }
                        if (offsetX < 0.0)
                        {
                            offsetX = 0.0;
                        }
                        parentPopup.VerticalOffset = offsetY;
                        parentPopup.HorizontalOffset = offsetX;

                        var clippingHeight = ((offsetY + lastRectangle.Height) + 2.0) - actualHeight;
                        var clippingWidth = ((offsetX + lastRectangle.Width) + 2.0) - actualWidth;
                        if ((clippingWidth >= 2.0) || (clippingHeight >= 2.0))
                        {
                            clippingWidth = Math.Max(0.0, clippingWidth);
                            clippingHeight = Math.Max(0.0, clippingHeight);
                            PerformClipping(new Size(lastRectangle.Width - clippingWidth, lastRectangle.Height - clippingHeight));
                        }
                    }
                    break;
                case PlacementMode.Bottom:
                case PlacementMode.Right:
                case PlacementMode.Left:
                case PlacementMode.Top:
                    var plugin = new Rect(0.0, 0.0, Application.Current.Host.Content.ActualWidth, Application.Current.Host.Content.ActualHeight);
                    var translatedPoints = GetTranslatedPoints((FrameworkElement)placementTarget);
                    var toolTip = GetTranslatedPoints((FrameworkElement)parentPopup.Child);
                    Point popupLocation = PlacePopup(plugin, translatedPoints, toolTip, placementMode);

                    parentPopup.VerticalOffset = popupLocation.Y + verticalOffset;
                    parentPopup.HorizontalOffset = popupLocation.X + horizontalOffset;
                    break;
            }


        }

        private static Point[] GetTranslatedPoints(FrameworkElement frameworkElement)
        {
            var pointArray = new Point[4];

            if (frameworkElement != null)
            {
                ToolTip toolTip = frameworkElement as ToolTip;
                if (toolTip == null || toolTip.IsOpen)
                {
                    var generalTransform = frameworkElement.TransformToVisual(null);
                    pointArray[0] = generalTransform.Transform(new Point(0.0, 0.0));
                    pointArray[1] = generalTransform.Transform(new Point(frameworkElement.ActualWidth, 0.0));
                    pointArray[1].X--;
                    pointArray[2] = generalTransform.Transform(new Point(0.0, frameworkElement.ActualHeight));
                    pointArray[2].Y--;
                    pointArray[3] = generalTransform.Transform(new Point(frameworkElement.ActualWidth, frameworkElement.ActualHeight));
                    pointArray[3].X--;
                    pointArray[3].Y--;
                }
            }

            return pointArray;
        }

        private static Point PlacePopup(Rect plugin, Point[] target, Point[] toolTip, PlacementMode placement)
        {
            var bounds = GetBounds(target);
            var rect2 = GetBounds(toolTip);
            var width = rect2.Width;
            var height = rect2.Height;

            placement = ValidatePlacement(target, placement, plugin, width, height);

            var pointArray = GetPointArray(target, placement, plugin, width, height);
            var index = GetIndex(plugin, width, height, pointArray);
            var point = CalculatePoint(target, placement, plugin, width, height, pointArray, index, bounds);

            return point;
        }

        private static int GetIndex(Rect plugin, double width, double height, IList<Point> pointArray)
        {
            var num13 = width * height;
            var index = 0;
            var num15 = 0.0;
            for (var i = 0; i < pointArray.Count; i++)
            {
                var rect3 = new Rect(pointArray[i].X, pointArray[i].Y, width, height);
                rect3.Intersect(plugin);
                var d = rect3.Width * rect3.Height;
                if (double.IsInfinity(d))
                {
                    index = pointArray.Count - 1;
                    break;
                }
                if (d > num15)
                {
                    index = i;
                    num15 = d;
                }
                if (d == num13)
                {
                    index = i;
                    break;
                }
            }
            return index;
        }

        private static Point CalculatePoint(IList<Point> target, PlacementMode placement, Rect plugin, double width, double height, IList<Point> pointArray, int index, Rect bounds)
        {
            var x = pointArray[index].X;
            var y = pointArray[index].Y;
            if (index > 1)
            {
                if ((placement == PlacementMode.Left) || (placement == PlacementMode.Right))
                {
                    if (((y != target[0].Y) && (y != target[1].Y)) && (((y + height) != target[0].Y) && ((y + height) != target[1].Y)))
                    {
                        var num18 = bounds.Top + (bounds.Height / 2.0);
                        if ((num18 > 0.0) && ((num18 - 0.0) > (plugin.Height - num18)))
                        {
                            y = plugin.Height - height;
                        }
                        else
                        {
                            y = 0.0;
                        }
                    }
                }
                else if (((placement == PlacementMode.Top) || (placement == PlacementMode.Bottom)) && (((x != target[0].X) && (x != target[1].X)) && (((x + width) != target[0].X) && ((x + width) != target[1].X))))
                {
                    var num19 = bounds.Left + (bounds.Width / 2.0);
                    if ((num19 > 0.0) && ((num19 - 0.0) > (plugin.Width - num19)))
                    {
                        x = plugin.Width - width;
                    }
                    else x = 0.0;
                }
            }
            return new Point(x, y);
        }

        private static Point[] GetPointArray(IList<Point> target, PlacementMode placement, Rect plugin, double width, double height)
        {
            Point[] pointArray;
            switch (placement)
            {
                case PlacementMode.Bottom:
                    pointArray = new[] { new Point(target[2].X, Math.Max(0.0, target[2].Y + 1.0)), new Point((target[3].X - width) + 1.0, Math.Max(0.0, target[2].Y + 1.0)), new Point(0.0, Math.Max(0.0, target[2].Y + 1.0)) };
                    break;

                case PlacementMode.Right:
                    pointArray = new[] { new Point(Math.Max(0.0, target[1].X + 1.0), target[1].Y), new Point(Math.Max(0.0, target[3].X + 1.0), (target[3].Y - height) + 1.0), new Point(Math.Max(0.0, target[1].X + 1.0), 0.0) };
                    break;

                case PlacementMode.Left:
                    pointArray = new[] { new Point(Math.Min(plugin.Width, target[0].X) - width, target[1].Y), new Point(Math.Min(plugin.Width, target[2].X) - width, (target[3].Y - height) + 1.0), new Point(Math.Min(plugin.Width, target[0].X) - width, 0.0) };
                    break;

                case PlacementMode.Top:
                    pointArray = new[] { new Point(target[0].X, Math.Min(target[0].Y, plugin.Height) - height), new Point((target[1].X - width) + 1.0, Math.Min(target[0].Y, plugin.Height) - height), new Point(0.0, Math.Min(target[0].Y, plugin.Height) - height) };
                    break;

                default:
                    pointArray = new[] { new Point(0.0, 0.0) };
                    break;
            }
            return pointArray;
        }

        private static PlacementMode ValidatePlacement(IList<Point> target, PlacementMode placement, Rect plugin, double width, double height)
        {
            switch (placement)
            {
                case PlacementMode.Right:
                    var num5 = Math.Max(0.0, target[0].X - 1.0);
                    var num6 = plugin.Width - Math.Min(plugin.Width, target[1].X + 1.0);
                    if ((num6 < width) && (num6 < num5))
                    {
                        placement = PlacementMode.Left;
                    }
                    break;
                case PlacementMode.Left:
                    var num7 = Math.Min(plugin.Width, target[1].X + width) - target[1].X;
                    var num8 = target[0].X - Math.Max(0.0, target[0].X - width);
                    if ((num8 < width) && (num8 < num7))
                    {
                        placement = PlacementMode.Right;
                    }
                    break;
                case PlacementMode.Top:
                    var num9 = target[0].Y - Math.Max(0.0, target[0].Y - height);
                    var num10 = Math.Min(plugin.Height, plugin.Height - height) - target[2].Y;
                    if ((num9 < height) && (num9 < num10))
                    {
                        placement = PlacementMode.Bottom;
                    }
                    break;
                case PlacementMode.Bottom:
                    var num11 = Math.Max(0.0, target[0].Y);
                    var num12 = plugin.Height - Math.Min(plugin.Height, target[2].Y);
                    if ((num12 < height) && (num12 < num11))
                    {
                        placement = PlacementMode.Top;
                    }
                    break;
            }
            return placement;
        }

        private static Rect GetBounds(params Point[] interestPoints)
        {
            double num2;
            double num4;
            var x = num2 = interestPoints[0].X;
            var y = num4 = interestPoints[0].Y;
            for (var i = 1; i < interestPoints.Length; i++)
            {
                var num6 = interestPoints[i].X;
                var num7 = interestPoints[i].Y;
                if (num6 < x)
                {
                    x = num6;
                }
                if (num6 > num2)
                {
                    num2 = num6;
                }
                if (num7 < y)
                {
                    y = num7;
                }
                if (num7 > num4)
                {
                    num4 = num7;
                }
            }
            return new Rect(x, y, (num2 - x) + 1.0, (num4 - y) + 1.0);
        }

        private static void PerformClipping(Size size)
        {
            var child = VisualTreeHelper.GetChild(CurrentToolTip, 0) as Border;
            if (child == null)
            {
                return;
            }

            if (size.Width < child.ActualWidth)
            {
                child.Width = size.Width;
            }
            if (size.Height < child.ActualHeight)
            {
                child.Height = size.Height;
            }
        }

        private static void UnregisterToolTip(UIElement owner)
        {

            if (owner.GetValue(ToolTipObjectProperty) == null)
            {
                return;
            }

            if (owner is FrameworkElement)
            {
                ((FrameworkElement)owner).Unloaded -= FrameworkElementUnloaded;
            }
            owner.MouseEnter -= OnElementMouseEnter;
            owner.MouseLeave -= OnElementMouseLeave;

            var toolTip = (ToolTip)owner.GetValue(ToolTipObjectProperty);
            if (toolTip.IsOpen)
            {
                toolTip.IsOpen = false;
            }
            toolTip.SetOwner(null);
            owner.ClearValue(ToolTipObjectProperty);

            if (elementsAndToolTips.ContainsKey(owner))
            {
                elementsAndToolTips.Remove(owner);
            }
        }

        private static void RegisterToolTip(FrameworkElement owner, object toolTipObject)
        {
            bool needsConversion = false;
            ToolTip toolTip = toolTipObject as ToolTip;

            if (toolTip == null)
            {
                needsConversion = true;
                toolTip = ConvertToToolTip(toolTipObject);
            }

            // Avoid a memory leak by removing the element from the dictionary
            // when the owner is unloaded.
            owner.Unloaded += FrameworkElementUnloaded;
            owner.MouseEnter += OnElementMouseEnter;
            owner.MouseLeave += OnElementMouseLeave;

            if (ReferenceEquals(null, toolTip)) return;

            if (!needsConversion)
            {
                toolTip.DataContext = owner.DataContext;
                owner.SetBinding(dataContextProperty, new Binding());
            }

            owner.SetValue(ToolTipObjectProperty, toolTip);
            toolTip.SetOwner(owner);
            SetToolTipInternal(owner, toolTip);
        }

        private static void FrameworkElementUnloaded(object sender, RoutedEventArgs e)
        {
            UnregisterToolTip((FrameworkElement)sender);
        }

        private static void SetRootVisual()
        {
            if ((rootVisual != null) || (Application.Current == null))
            {
                return;
            }

            rootVisual = Application.Current.RootVisual as FrameworkElement;
            if (rootVisual == null)
            {
                return;
            }

            rootVisual.MouseMove += OnRootVisualMouseMove;
            rootVisual.SizeChanged += OnRootVisualSizeChanged;
            rootVisual.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnRootVisualMouseLeftButtonDown), true);
        }

        private static void SetToolTipInternal(DependencyObject element, ToolTip toolTip)
        {
            if (element == null)
                throw new ArgumentNullException("element");

            if (toolTip == null)
            {
                elementsAndToolTips.Remove(element);
                return;
            }

            toolTip.SizeChanged += OnToolTipSizeChanged;

            var control = element as Control;
            if (control != null)
            {
                control.IsEnabledChanged += OnElementIsEnabledChanged;
            }

            if (elementsAndToolTips.ContainsKey(element))
            {
                elementsAndToolTips.Remove(element);
            }

            if (toolTip.CloseAnimation != null)
            {
                toolTip.CloseAnimation.Completed += (s, args) =>
                {
                    toolTip.IsOpen = false;
                    isCloseAnimationInProgress = false;
                };
            }
            if (toolTip.OpenAnimation != null)
            {
                toolTip.OpenAnimation.Completed += (s, args) => isOpenAnimationInProgress = false;
            }

            elementsAndToolTips.Add(element, toolTip);

            //element.SetValue(ToolTipProperty, toolTip);
        }

        ///<summary>
        /// Gets the PlacementProperty value for the ToolTip element.
        ///</summary>
        ///<param name="element">The ToolTip element.</param>
        ///<returns>The value for the PlacementProperty of the ToolTip element.</returns>
        ///<exception cref="ArgumentNullException">The DependencyObject can not be null.</exception>
        public static PlacementMode GetPlacement(DependencyObject element)
        {
            if (element == null)
                throw new ArgumentNullException("element", "The DependencyObject could not be found.");

            return (PlacementMode)element.GetValue(PlacementProperty);
        }

        ///<summary>
        /// Gets the PlacementTargetProperty value for the ToolTip element.
        ///</summary>
        ///<param name="element">The ToolTip element.</param>
        ///<returns>The value for the PlacementTargetProperty.</returns>
        public static UIElement GetPlacementTarget(DependencyObject element)
        {
            return element == null ? null : (UIElement)element.GetValue(PlacementTargetProperty);
        }

        ///<summary>
        /// Sets the PlacementProperty for the ToolTip element.
        ///</summary>
        ///<param name="element">The ToolTip element.</param>
        ///<param name="value">The value for the PlacementProperty.</param>
        public static void SetPlacement(DependencyObject element, PlacementMode value)
        {
            element.SetValue(PlacementProperty, value);
        }

        /// <summary>
        /// Sets the PlacementTargetProperty value for the ToolTip element.
        /// </summary>
        /// <param name="element">The ToolTip element.</param>
        /// <param name="value">The value for the PlacementTargetProperty.</param>
        public static void SetPlacementTarget(DependencyObject element, UIElement value)
        {
            element.SetValue(PlacementTargetProperty, value);
        }
    }
}
