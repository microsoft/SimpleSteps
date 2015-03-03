/*	
The MIT License (MIT)
Copyright (c) 2015 Microsoft

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Lumia.Sense;
using System.Threading.Tasks;
using Windows.UI.Popups;
using System.Threading;

namespace SimpleSteps
{
    /// <summary>
    /// Application main page
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Constant definitions
        /// <summary>
        /// Data point interval in graph (in minutes)
        /// </summary>
        /// <remarks>Value must be at least 5 as that is the Step counter data interval.</remarks>
        private const int DataPointInterval = 15;

        /// <summary>
        /// Steps scale based on current zoom level
        /// </summary>
        private readonly static uint[] MaxSteps = new uint[] { 10000, 5000, 2500, 1000 };

        /// <summary>
        /// List of step counts per data point interval for the currently selected day
        /// </summary>
        private Dictionary<DateTime, StepCount> _daySteps = null;
        #endregion

        #region Private members
        /// <summary>
        /// Step counter instance
        /// </summary>
        StepCounter _stepCounter = null;

        /// <summary>
        /// Current zoom level
        /// </summary>
        int _currentZoomLevel = 0;

        /// <summary>
        /// Synchronization object
        /// </summary>
        private SemaphoreSlim _sync = new SemaphoreSlim( 1 );

        /// <summary>
        /// Currently selected day
        /// </summary>
        DateTime _selectedDay = DateTime.Now.Date;

        /// <summary>
        /// check to see launching finished or not
        /// </summary>
        private bool iLaunched = false;
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            TopLimitLabel.Text = ( MaxSteps[ _currentZoomLevel ].ToString() );
            CenterLimitLabel.Text = ( ( MaxSteps[ _currentZoomLevel ] / 2 ).ToString() );

            Window.Current.VisibilityChanged += async ( oo, ee ) =>
            {
                if( !ee.Visible && _stepCounter != null )
                {
                    await CallSenseApiAsync( async () =>
                    {
                        await _stepCounter.DeactivateAsync();
                    } );
                }
                else if(_stepCounter !=null)
                {
                    await CallSenseApiAsync( async () =>
                    {
                        await _stepCounter.ActivateAsync();
                    } );

                    // Refresh screen
                    await SetSelectedDayAsync( _selectedDay );
                }
            };
        }

        /// <summary>
        /// Sets currently selected day
        /// </summary>
        /// <param name="selectedDay">Day to view</param>
        private async Task SetSelectedDayAsync( DateTime selectedDay )
        {
            _selectedDay = selectedDay;
            _daySteps = await LoadDaySteps( _selectedDay );
            UpdateScreen( _daySteps );
        }

        /// <summary>
        /// Sets current zoom level
        /// </summary>
        /// <param name="zoomLevel">New zoom level</param>
        private void SetZoomLevel( int zoomLevel )
        {
            _currentZoomLevel = zoomLevel;
            TopLimitLabel.Text = ( MaxSteps[ _currentZoomLevel ].ToString() );
            CenterLimitLabel.Text = ( ( MaxSteps[ _currentZoomLevel ] / 2 ).ToString() );
            UpdateScreen( _daySteps );
        }

        /// <summary>
        /// Initializes sensor
        /// </summary>
        /// <returns>Asynchronous task</returns>
        private async Task Initialize()
        {
            if( !await StepCounter.IsSupportedAsync() )
            {
                MessageDialog dlg = new MessageDialog( "Unfortunately this device does not support step counting" );
                await dlg.ShowAsync();
            }
            else
            {
                MotionDataSettings settings = await SenseHelper.GetSettingsAsync();
                // Starting from version 2 of Motion data settings Step counter and Acitivity monitor are always available. In earlier versions system
                // location setting and Motion data had to be enabled.
                if( settings.Version < 2 )
                {
                    if( !settings.LocationEnabled )
                    {
                        MessageDialog dlg = new MessageDialog( "In order to count steps you need to enable location in system settings. Do you want to open settings now?", "Information" );
                        dlg.Commands.Add( new UICommand( "Yes", new UICommandInvokedHandler( async ( cmd ) => await SenseHelper.LaunchLocationSettingsAsync() ) ) );
                        dlg.Commands.Add( new UICommand( "No" ) );
                        await dlg.ShowAsync();
                    }
                    else if( !settings.PlacesVisited )
                    {
                        MessageDialog dlg = new MessageDialog( "In order to count steps you need to enable Motion data collection in Motion data settings. Do you want to open settings now?", "Information" );
                        dlg.Commands.Add( new UICommand( "Yes", new UICommandInvokedHandler( async ( cmd ) => await SenseHelper.LaunchSenseSettingsAsync() ) ) );
                        dlg.Commands.Add( new UICommand( "No" ) );
                        await dlg.ShowAsync();
                    }
                }
            }

           if( !await CallSenseApiAsync( async () =>
           {
                _stepCounter = await StepCounter.GetDefaultAsync();
           } ) )
           {
                Application.Current.Exit();
           }
           await SetSelectedDayAsync( _selectedDay );
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override async void OnNavigatedTo( NavigationEventArgs e )
        {
            // Make sure the sensors are instantiated
            if( !iLaunched )
            {
                iLaunched = true;
                await Initialize();
            }
        }

        /// <summary>
        /// Loads steps for the given day
        /// </summary>
        /// <param name="fetchDate">Date to fetch steps for</param>
        /// <returns>List of step counts per data point interval for the given day</returns>
        private async Task<Dictionary<DateTime, StepCount>> LoadDaySteps( DateTime fetchDate )
        {
            Dictionary<DateTime, StepCount> daySteps = new Dictionary<DateTime, StepCount>();
            try
            {
                int totalMinutesPerDay = 24 * 60;
                int dataPoints = totalMinutesPerDay / DataPointInterval;
                for( int i = 0; i <= dataPoints; i++ )
                {
                    DateTime fetchFrom = fetchDate.Date + TimeSpan.FromMinutes( i * DataPointInterval );
                    if( fetchFrom > DateTime.Now )
                        break;
                    TimeSpan fetchRange = TimeSpan.FromMinutes( DataPointInterval );
                    if( ( fetchFrom + fetchRange ) > DateTime.Now )
                        fetchRange = DateTime.Now - fetchFrom;
                    StepCount stepCount = await _stepCounter.GetStepCountForRangeAsync(
                        fetchFrom,
                        fetchRange
                        );
                    if( stepCount != null )
                        daySteps.Add( fetchFrom, stepCount );
                }
            }
            catch( Exception )
            {
            }
            return daySteps;
        }

        /// <summary>
        /// Updates screen
        /// </summary>
        /// <param name="daySteps">List of steps per data point interval for the selected day</param>
        private void UpdateScreen( Dictionary<DateTime, StepCount> daySteps )
        {
            //clear graph for walking, running and total steps
            walkGraphSegments.Clear();
            runGraphSegments.Clear();
            totalStepsGraphSegments.Clear();
            if( daySteps.Count == 0 )
            {
                DateLabel.Text = "";
                return;
            }
            DateLabel.Text = daySteps.ElementAt( 0 ).Key.ToString( "d" );

            double graphHeight = GraphBox.ActualHeight - GraphBox.StrokeThickness * 2;
            double graphWidth = GraphBox.ActualWidth - GraphBox.StrokeThickness * 2;
            int totalMinutesPerDay = 24 * 60;
            int dataPoints = totalMinutesPerDay / DataPointInterval;

            // Handle first data point. 
            KeyValuePair<DateTime, StepCount> stepCount = daySteps.ElementAt( 0 );
            uint totalSteps = stepCount.Value.RunningStepCount + stepCount.Value.WalkingStepCount;
            uint totalWalkSteps = stepCount.Value.WalkingStepCount;
            uint totalRunSteps = stepCount.Value.RunningStepCount;
            //if steps are greater than zoom level then cut it zoom level
            totalStepsGraph.StartPoint = new Point(
                GraphBox.StrokeThickness, GraphBox.StrokeThickness + graphHeight - ( graphHeight * Math.Min( MaxSteps[ _currentZoomLevel ], 
                stepCount.Value.RunningStepCount + stepCount.Value.WalkingStepCount ) / MaxSteps[ _currentZoomLevel ] ) );
            walkGraph.StartPoint = new Point(
                GraphBox.StrokeThickness,
                GraphBox.StrokeThickness + graphHeight - ( graphHeight * Math.Min( MaxSteps[ _currentZoomLevel ], stepCount.Value.WalkingStepCount ) / MaxSteps[ _currentZoomLevel ] ) );
            runGraph.StartPoint = new Point(
                GraphBox.StrokeThickness,
                GraphBox.StrokeThickness + graphHeight - ( graphHeight * Math.Min( MaxSteps[ _currentZoomLevel ], stepCount.Value.RunningStepCount ) / MaxSteps[ _currentZoomLevel ] ) );

            // Handle rest of the data points
            for( int i = 1; i < daySteps.Count; i++ )
            {
                stepCount = daySteps.ElementAt( i );
                totalWalkSteps += stepCount.Value.WalkingStepCount;
                totalRunSteps += stepCount.Value.RunningStepCount;
                totalSteps += ( stepCount.Value.RunningStepCount + stepCount.Value.WalkingStepCount );

                double x = GraphBox.StrokeThickness + graphWidth * ( stepCount.Key - _selectedDay ).TotalMinutes / ( 24 * 60 );
                double walkY = GraphBox.StrokeThickness + graphHeight - ( graphHeight * Math.Min( MaxSteps[ _currentZoomLevel ], stepCount.Value.WalkingStepCount ) / MaxSteps[ _currentZoomLevel ] );
                double runY = GraphBox.StrokeThickness + graphHeight - ( graphHeight * Math.Min( MaxSteps[ _currentZoomLevel ], stepCount.Value.RunningStepCount ) / MaxSteps[ _currentZoomLevel ] );
                double totalStepsY = GraphBox.StrokeThickness + graphHeight - ( graphHeight * Math.Min( MaxSteps[ _currentZoomLevel ], totalSteps ) / MaxSteps[ _currentZoomLevel ] );

                walkGraphSegments.Add( new LineSegment() { Point = new Point( x, walkY ) } );
                runGraphSegments.Add( new LineSegment() { Point = new Point( x, runY ) } );
                totalStepsGraphSegments.Add( new LineSegment() { Point = new Point( x, totalStepsY ) } );
            }
            WalkStepsLabel.Text = ( totalWalkSteps ).ToString();
            RunStepsLabel.Text = ( totalRunSteps ).ToString();
            TotalStepsLabel.Text = ( totalSteps ).ToString();
        }

        /// <summary>
        /// Performs asynchronous SensorCore SDK operation and handles any exceptions
        /// </summary>
        /// <param name="action"></param>
        /// <returns><c>true</c> if call was successful, <c>false</c> otherwise</returns>
        private async Task<bool> CallSenseApiAsync( Func<Task> action )
        {
            Exception failure = null;
            try
            {
                await action();
            }
            catch( Exception e )
            {
                failure = e;
            }

            if( failure != null )
            {
                MessageDialog dialog = null;
                switch( SenseHelper.GetSenseError( failure.HResult ) )
                {
                    case SenseError.LocationDisabled:
                        dialog = new MessageDialog( "Location has been disabled. Do you want to open Location settings now?", "Information" );
                        dialog.Commands.Add( new UICommand( "Yes", new UICommandInvokedHandler( async ( cmd ) => await SenseHelper.LaunchLocationSettingsAsync() ) ) );
                        dialog.Commands.Add( new UICommand( "No" ) );
                        await dialog.ShowAsync();
                        return true;

                    case SenseError.SenseDisabled:
                        dialog = new MessageDialog( "Motion data has been disabled. Do you want to open Motion data settings now?", "Information" );
                        dialog.Commands.Add( new UICommand( "Yes", new UICommandInvokedHandler( async ( cmd ) => await SenseHelper.LaunchSenseSettingsAsync() ) ) );
                        dialog.Commands.Add( new UICommand( "No" ) );
                        await dialog.ShowAsync();
                        return true;

                    case SenseError.IncompatibleSDK:
                        dialog = new MessageDialog( "This application has become outdated. Please update to the latest version.", "Information" );
                        await dialog.ShowAsync();
                        return false;

                    default:
                        dialog = new MessageDialog( "Failure: " + SenseHelper.GetSenseError( failure.HResult ), "" );
                        await dialog.ShowAsync();
                        return false;
                }
            }
            else
            {
                return true;
            }
        }

        #region Event handlers
        /// <summary>
        /// Screen tapped event handler
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">Event arguments</param>
        private async void Screen_Tapped( object sender, TappedRoutedEventArgs e )
        {
            await _sync.WaitAsync();
            try
            {
                SetZoomLevel( ( _currentZoomLevel + 1 ) % MaxSteps.Length );
            }
            finally
            {
                _sync.Release();
            }
        }

        /// <summary>
        /// Previous day button click event handler
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">Event arguments</param>
        private async void GoToPreviousDay( object sender, RoutedEventArgs e )
        {
            await _sync.WaitAsync();
            try
            {
                if( ( DateTime.Now.Date - _selectedDay ) < TimeSpan.FromDays( 6 ) )
                {
                    await SetSelectedDayAsync( _selectedDay - TimeSpan.FromDays( 1 ) );
                }
                else
                {
                    MessageDialog dialog = new MessageDialog( "This application displays only last seven days of steps.", "Steps" );
                    await dialog.ShowAsync();
                }
            }
            finally
            {
                _sync.Release();
            }
        }

        /// <summary>
        /// Next day button click event handler
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">Event arguments</param>
        private async void GoToNextDay( object sender, RoutedEventArgs e )
        {
            await _sync.WaitAsync();
            try
            {
                if( _selectedDay.Date < DateTime.Now.Date )
                {
                    await SetSelectedDayAsync( _selectedDay + TimeSpan.FromDays( 1 ) );
                }
                else
                {
                    MessageDialog dialog = new MessageDialog( "Can't display future steps", "Steps" );
                    await dialog.ShowAsync();
                }
            }
            finally
            {
                _sync.Release();
            }
        }
        #endregion
    }
}

// end of file
