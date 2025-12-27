Imports System.IO
Imports System.Media
Imports System.Threading
Imports System.Windows.Threading
Imports VB_HumphreyButtonApp.StimulusSequence

Imports Phidget22
Imports Phidget22.Events

Public Class MainWindow

    ' -----------------------------
    ' Phidget Channels, Etc.
    ' -----------------------------
    Private bc As New DigitalInput()   ' Button Channel
    Private cc As New DigitalOutput()  ' Clicker (rumble)
    Private fc As New DigitalOutput()  ' Feeder Channel
    Private flc As New DigitalOutput() ' Feeder LED
    ' -----------------------------
    ' Timers & Stopwatches
    ' -----------------------------
    Friend WithEvents timer As New System.Timers.Timer(1) ' 1 ms tick
    Private uiTimer As New System.Windows.Threading.DispatcherTimer With {
        .Interval = TimeSpan.FromMilliseconds(50)
    }
    Private Latency As New Stopwatch()
    Private ActiveStimWatch As New Stopwatch()
    Private StimAWatch As New Stopwatch()
    Private StimBWatch As New Stopwatch() '<--- also used for the white/control stimulus during test
    Private MasterWatch As New Stopwatch()
    Private BlueWatch As New Stopwatch()
    Private GreenWatch As New Stopwatch()
    Private YellowWatch As New Stopwatch()
    Private OrangeWatch As New Stopwatch()
    Private RedWatch As New Stopwatch()

    ' -----------------------------
    ' State Variables
    ' -----------------------------
    Private sessionStartTimeStamp As DateTime
    Private rumbleCts As CancellationTokenSource
    Private logWriter As StreamWriter = Nothing
    Private logLock As New Object()
    Private logFilePath As String = String.Empty

    Private TargetTime As Integer = 0
    Private HoldLimit As Integer = 5000
    Private btnCount As Integer = 0
    Private trialCount As Integer = 0
    Private idx As Integer = 0
    Private aPressCt As Integer = 0
    Private bPressCt As Integer = 0

    Private devMode As Boolean = False
    Private isLockout As Boolean = False
    Private animationPlayed As Boolean = False
    Private isRunning As Boolean = False ' Pre-start flag
    Private trialReady As Boolean = False
    Private TrainingMode As Boolean = False
    Private colorWatchOn As Boolean = False
    Private hasSavedOnDisconnect As Boolean = False
    Private manualSave As Boolean = False
    Private manualTrialSave As Boolean = False
    Private progressControllerTotalPress As ProgressBarController
    Private progressControllerActiveStim As ProgressBarController
    ' -------------------------------------------------------
    ' Constructor
    ' -------------------------------------------------------
    Public Sub New()
        InitializeComponent()

        ' Assign device serial
        bc.DeviceSerialNumber = 705599
        cc.DeviceSerialNumber = 705599
        fc.DeviceSerialNumber = 705599
        flc.DeviceSerialNumber = 705599

        bc.Channel = 8
        cc.Channel = 14
        fc.Channel = 15
        flc.Channel = 9

        ' Events - Attach / Detach / Error
        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler cc.Attach, AddressOf OnAttachHandler
        AddHandler fc.Attach, AddressOf OnAttachHandler
        AddHandler flc.Attach, AddressOf OnAttachHandler

        AddHandler bc.Detach, AddressOf OnDetachHandler
        AddHandler cc.Detach, AddressOf OnDetachHandler
        AddHandler fc.Detach, AddressOf OnDetachHandler
        AddHandler flc.Detach, AddressOf OnDetachHandler

        AddHandler bc.Error, AddressOf OnErrorHandler
        AddHandler cc.Error, AddressOf OnErrorHandler
        AddHandler fc.Error, AddressOf OnErrorHandler
        AddHandler flc.Error, AddressOf OnErrorHandler

        AddHandler bc.StateChange, AddressOf ButtonStim_StateChanged
        AddHandler bc.StateChange, AddressOf ButtonRumble_StateChanged

        AddHandler uiTimer.Tick, AddressOf UiTimer_Tick

        ' Open hardware
        Try
            cc.Open()
            bc.Open()
            fc.Open()
            flc.Open()
        Catch ex As Exception
            Console.WriteLine($"Error opening channels: {ex.Message}")
            ' If open fails, ensure we save what we have
            HandleDisconnectSave("Error opening channels: " & ex.Message)
        End Try

        ' Timer
        uiTimer.Start()
        timer.Start()

        Try
            Dim folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhidgetData", "logs")
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            logFilePath = Path.Combine(folder, $"phidget_log_{DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")}.log")
            logWriter = New StreamWriter(New FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read)) With {
                .AutoFlush = True
            }
            Log($"Log started: {DateTime.UtcNow:o}")
        Catch ex As Exception
            Console.WriteLine($"Failed to open log file: {ex.Message}")
        End Try
    End Sub

    ' -------------------------------------------------------
    ' Window Placement
    ' -------------------------------------------------------
    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Dim screens = Screen.AllScreens

        If screens.Length < 2 Then
            ' Use WPF MessageBox (System.Windows.MessageBox) and keep Screen.AllScreens for monitor detection.
            Dim res = System.Windows.MessageBox.Show(
                "One monitor detected. Dev Mode?",
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            )
            If res = MessageBoxResult.Yes Then
                devMode = True
                Dim screen = screens(0)
                Dim screenWidth = screen.Bounds.Width ' keep WinForms screen measurement

                ResearcherView.Width = screenWidth
                ResearcherView.BringIntoView()
                SubjectView.Width = 0
            Else
                devMode = False
                Return
            End If
        Else
            Dim researcherScreen = screens(0)
            Dim subjectScreen = screens(1)

            Me.WindowStyle = WindowStyle.None
            Me.ResizeMode = ResizeMode.NoResize
            Me.WindowStartupLocation = WindowStartupLocation.Manual

            Me.Left = researcherScreen.Bounds.Left
            Me.Top = researcherScreen.Bounds.Top
            Me.Width = researcherScreen.Bounds.Width + subjectScreen.Bounds.Width
            Me.Height = Math.Max(researcherScreen.Bounds.Height, subjectScreen.Bounds.Height)
            StimSpy.Fill = New VisualBrush(StimGrid)
            'Me.Height = maxHeight

            ' Resize columns to fit each monitor exactly
            ResearcherCol.Width = New GridLength(researcherScreen.Bounds.Width)
            SubjectCol.Width = New GridLength(subjectScreen.Bounds.Width)
        End If

        InitializeProgressBars()
    End Sub


    ' -------------------------------------------------------
    ' UI Initialization
    ' -------------------------------------------------------
    Private Sub InitMainWindow() Handles MyBase.Initialized

        MainWin.WindowStyle = WindowStyle.None
        MainWin.ResizeMode = ResizeMode.NoResize
    End Sub

    ' Add to MainWindow_Loaded or after InitializeComponent:
    Private Sub InitializeProgressBars()
        ' Only read TargetTime here if the ComboBox control exists AND has a selection.
        If TargetTimeInput IsNot Nothing AndAlso TargetTimeInput.SelectedItem IsNot Nothing Then
            Try
                Dim seconds = CInt(CType(TargetTimeInput.SelectedItem, ComboBoxItem).Content)
                TargetTime = seconds * 1000
            Catch ex As Exception
                ' don't override TargetTime here; leave it as 0 to be resolved at Start
                Log($"InitializeProgressBars: parse error, leaving TargetTime unchanged: {ex.Message}")
            End Try
        End If

        ' Total Press (StimA + StimB combined) with TargetTime threshold
        progressControllerTotalPress = New ProgressBarController(
            ProgressBar0, ' placeholder stopwatch (we will push combined elapsed manually)
            New Stopwatch(),
            TargetTime ' if zero/invalid, controller will use safe min internally
        )

        ' Active Stimulus with HoldLimit threshold
        progressControllerActiveStim = New ProgressBarController(
            ProgressBar1,
            ActiveStimWatch,
            HoldLimit
        )
    End Sub

    ' -------------------------------------------------------
    ' Clock tick → UI dispatcher → control loop
    ' -------------------------------------------------------
    Private Sub Clock() Handles timer.Elapsed
        Application.Current.Dispatcher.BeginInvoke(AddressOf ControlLoop)
    End Sub


    Private Sub UiTimer_Tick(sender As Object, e As EventArgs)
        ' Lightweight UI updates only. Avoid heavy logic here.
        If isRunning Then
            InitWatches()

            ' Update progress bars based on stopwatch values
            If progressControllerTotalPress IsNot Nothing Then
                ' Create synthetic elapsed combining StimA + StimB and pass to controller.
                Dim combinedElapsed = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds
                progressControllerTotalPress.SetElapsed(combinedElapsed)
            End If

            If progressControllerActiveStim IsNot Nothing Then
                progressControllerActiveStim.Update()
            End If
        End If
    End Sub

    ' -------------------------------------------------------
    ' Button → Stimulus Logic (Debounce Removed)
    ' -------------------------------------------------------
    Private Sub ButtonStim_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dim arrivalTs = DateTime.UtcNow
        Log($"{arrivalTs:o} - Button state changed: {e.State}")

        ' Marshal directly to UI thread without debounce
        Dispatcher.Invoke(Sub()
                              Log($"{DateTime.UtcNow:o} - Processing button state: {e.State}")

                              ' Guard conditions
                              If Not trialReady Then
                                  Log($"{DateTime.UtcNow:o} - Ignored: trial not ready")
                                  Return
                              End If

                              If isLockout Then
                                  Log($"{DateTime.UtcNow:o} - Ignored: in lockout")
                                  Return
                              End If

                              If Not isRunning Then
                                  Log($"{DateTime.UtcNow:o} - Ignored: session not running")
                                  Return
                              End If

                              ' Process button state
                              If e.State = True Then
                                  ' ========== BUTTON PRESSED ==========
                                  Log($"{DateTime.UtcNow:o} - Button PRESSED")
                                  btnCount += 1
                                  If btnCount > 0 Then
                                      HideReadyIndicator()
                                  End If

                                  ' Activate progress bars on press (total also activated at session start)
                                  progressControllerTotalPress?.Activate()
                                  progressControllerActiveStim?.Activate()

                                  ' Button pressed
                                  Latency.Stop()
                                  ActivateOut(cc, 35) ' fire-and-forget

                                  If ActiveStimWatch.ElapsedMilliseconds < HoldLimit Then
                                      ActiveStimWatch.Reset()
                                      SetGridColor(btnCount)
                                      Log($"{DateTime.UtcNow:o} - Processed press: btnCount={btnCount}")
                                  Else
                                      ' Over HoldLimit -> reset visual and stim
                                      rumbleCts?.Cancel()
                                      cc.State = False
                                      ActiveStimWatch.Stop()
                                      StimAWatch.Stop()
                                      StimBWatch.Stop()
                                      EndColorWatch()
                                      ResetGridVisuals()
                                      ActiveStimWatch.Reset()
                                      Log($"{DateTime.UtcNow:o} - HoldLimit exceeded; reset visuals")
                                  End If

                              Else
                                  ' Button released
                                  ' Deactivate active-stim progress tracking on release
                                  progressControllerActiveStim?.Deactivate()

                                  If Not isLockout Then
                                      RecordData()
                                  End If
                                  cc.State = False
                                  Latency.Start()
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  ResetGridVisuals()
                                  Log($"{DateTime.UtcNow:o} - Processed release")
                              End If

                              Log($"{DateTime.UtcNow:o} - UI handler end for state={bc.State}")
                          End Sub)

    End Sub
    ' -------------------------------------------------------
    ' Button → Rumble Loop
    ' -------------------------------------------------------
    Private Sub ButtonRumble_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        ' Do not block the UI thread with Invoke + async. Perform minimal dispatcher work.
        If e.State Then
            ' Cancel and dispose any existing CTS first
            Try
                rumbleCts?.Cancel()
                rumbleCts?.Dispose()
            Catch
            End Try
            rumbleCts = New CancellationTokenSource()
            ' Start rumble on background thread (fire-and-forget)
            Task.Run(Function() RumblePak(rumbleCts.Token))
        Else
            ' Stop rumble
            Try
                rumbleCts?.Cancel()
                rumbleCts?.Dispose()
            Catch
            End Try
            rumbleCts = Nothing
            ' Ensure the hardware is turned off on UI thread
            Dispatcher.BeginInvoke(Sub() cc.State = False)
        End If
    End Sub

    ' -------------------------------------------------------
    ' Phidget Attached
    ' -------------------------------------------------------
    Private Sub OnAttachHandler(sender As Object, e As AttachEventArgs)
        Dispatcher.Invoke(Sub()
                              Console.WriteLine($"Phidget {sender} attached!")
                          End Sub)
    End Sub

    ' -------------------------------------------------------
    ' Phidget Detach (disconnect) handler
    ' -------------------------------------------------------
    Private Sub OnDetachHandler(sender As Object, e As DetachEventArgs)
        Dispatcher.Invoke(Sub()
                              Console.WriteLine($"Phidget detached: {sender}")

                              ' Save only once even if multiple channels detach
                              If hasSavedOnDisconnect Then
                                  Return
                              End If
                              hasSavedOnDisconnect = True

                              Try
                                  ' perform non-interactive autosave of both logs
                                  SaveDataAuto()
                                  SaveTrialDataAuto()
                              Catch ex As Exception
                                  Console.WriteLine($"Error saving on detach: {ex.Message}")
                              End Try

                          End Sub)
    End Sub

    ' -------------------------------------------------------
    ' Phidget Error handler (device-level errors)
    ' -------------------------------------------------------
    Private Sub OnErrorHandler(sender As Object, e As Events.ErrorEventArgs)
        Dispatcher.Invoke(Sub()
                              Console.WriteLine($"Phidget error on {sender}: {e.Description}")

                              ' Save only once on first error that we treat as critical
                              If hasSavedOnDisconnect Then
                                  Return
                              End If
                              hasSavedOnDisconnect = True

                              Try
                                  SaveDataAuto()
                                  SaveTrialDataAuto()
                              Catch ex As Exception
                                  Console.WriteLine($"Error autosaving trial data on error: {ex.Message}")
                              End Try

                          End Sub)
    End Sub


    ' -------------------------------------------------------
    ' CONTROL LOOP
    ' -------------------------------------------------------
    Private Async Sub ControlLoop()
        'If Not bc.Attached Then
        '    If devMode Then Return
        '    HideReadyIndicator()
        '    MsgBox("Button Channel not attached. Please check connections.")
        '    'Return
        'End If

        If Not trialReady Then
            HideReadyIndicator()
        End If

        ' Pre-start: behave like lockout but no outputs
        If Not isRunning Then
            HideReadyIndicator()
            ActiveStimWatch.Stop()
            EndColorWatch()
            StimAWatch.Stop()
            StimBWatch.Stop()
            Latency.Stop()
            Return
        End If

        ' Handle ComboBox defaults
        If SubjectName.SelectedItem Is Nothing Then
            SubjectName.SelectedIndex = SubjectName.Items.Count - 1
        End If

        If TargetTimeInput.SelectedItem Is Nothing Then
            TargetTimeInput.SelectedIndex = 0
        End If

        'TargetTime = CInt(CType(TargetTimeInput.SelectedItem, ComboBoxItem).Content) * 1000

        ' Auto stop at HoldLimit sec
        If ActiveStimWatch.ElapsedMilliseconds >= HoldLimit Then
            rumbleCts?.Cancel()
            cc.State = False
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            EndColorWatch()
            ResetGridVisuals()
            ActiveStimWatch.Reset()
        End If

        If Not isLockout Then
            If devMode Then Return
            If Not bc.Attached Then Return
            If Not bc.State Then
                ActiveStimWatch.Stop()
                StimAWatch.Stop()
                StimBWatch.Stop()
                EndColorWatch()
                ' Keep total progress visible throughout trial; only stop active-stim tracking.
                progressControllerActiveStim?.Deactivate()
                'progressControllerStimA.Deactivate()
                Return
            End If

            ' Check progress bar threshold instead of raw stopwatch
            If progressControllerTotalPress.IsThresholdReached() Then
                ' Play chime
                PlaySound(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\beep.wav"))

                ' Enter lockout
                isLockout = True
                rumbleCts?.Cancel()
                cc.State = False
                ActiveStimWatch.Stop()
                EndColorWatch()
                StimAWatch.Stop()
                StimBWatch.Stop()

                ' Deactivate all progress bars during lockout
                progressControllerTotalPress.Deactivate()
                progressControllerActiveStim.Deactivate()
                'progressControllerStimA.Deactivate()

                animationPlayed = True
                Await LockOut()

                isLockout = False
                animationPlayed = False
                ShowReadyIndicator()
                Return
            End If
        End If

        If btnCount < 1 Then
            Latency.Reset()
            Latency.Stop()
        End If

        InitWatches()
    End Sub


    ' -------------------------------------------------------
    ' Play WAV file
    ' -------------------------------------------------------
    Private Sub PlaySound(fileName As String)
        Try
            Dim player As New SoundPlayer(fileName)
            player.Play()
        Catch ex As Exception
            Console.WriteLine($"Error playing sound: {ex.Message}")
        End Try
    End Sub


    ' -------------------------------------------------------
    ' logger
    ' -------------------------------------------------------
    Private Sub Log(message As String)
        Dim line = $"{DateTime.UtcNow:o} - {message}"
        SyncLock logLock
            Try
                If logWriter IsNot Nothing Then
                    logWriter.WriteLine(line)
                Else
                    ' fallback to console if writer not available
                    Console.WriteLine(line)
                End If
            Catch ex As Exception
                Console.WriteLine($"Logging error: {ex.Message}")
            End Try
        End SyncLock
    End Sub

    ' -------------------------------------------------------
    ' Update UI watch labels
    ' -------------------------------------------------------
    Private Sub InitWatches()
        PressWatchVal.Content = $"{(StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds) / 1000} secs"
        ActiveStimVal.Content = $"{ActiveStimWatch.ElapsedMilliseconds / 1000} secs"
        StimAWatchVal.Content = $"{StimAWatch.ElapsedMilliseconds / 1000} secs"
        StimBWatchVal.Content = $"{StimBWatch.ElapsedMilliseconds / 1000} secs"
        LatencyVal.Content = $"{Latency.ElapsedMilliseconds / 1000} secs"
    End Sub


    ' -------------------------------------------------------
    ' Alternating colors + overlays
    ' -------------------------------------------------------
    Private Sub SetGridColor(count As Integer)
        'If isLockout OrElse Not isRunning Then
        '    Log($"{DateTime.UtcNow:o} - SetGridColor ignored: isLockout={isLockout}, isRunning={isRunning}")
        '    Return
        ' End If

        Log($"{DateTime.UtcNow:o} - SetGridColor activating for press #{count}")
        ActiveStimWatch.Start()

        If TrainingMode = True Then
            If count Mod 2 = 0 Then
                If StimBWatch.IsRunning Then StimBWatch.Stop()
                If Not StimAWatch.IsRunning Then StimAWatch.Start()
                aPressCt += 1
                StimGrid.Background = Brushes.Gray
                ShowOverlay(StimGridOverlay, "Assets/invert_hd-wallpaper-7939241_1280.png")
                Log($"{DateTime.UtcNow:o} - Stimulus A activated (Gray)")
            Else
                If StimAWatch.IsRunning Then StimAWatch.Stop()
                If Not StimBWatch.IsRunning Then StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.LightGray
                ShowOverlay(StimGridOverlay, "Assets/waves-9954690_1280.png")
            End If
        Else
            ' Use persisted idx as the authoritative step index.
            Select Case trialCount
                Case 0
                    TrialToggler(StimulusSequence.Trial0)
                Case 1
                    TrialToggler(StimulusSequence.Trial1)
                Case 2
                    TrialToggler(StimulusSequence.Trial2)
                Case 3
                    TrialToggler(StimulusSequence.Trial3)
                Case 4
                    TrialToggler(StimulusSequence.Trial4)
                Case 5
                    TrialToggler(StimulusSequence.Trial5)
                Case 6
                    TrialToggler(StimulusSequence.Trial6)
                Case 7
                    TrialToggler(StimulusSequence.Trial7)
                Case 8
                    TrialToggler(StimulusSequence.Trial8)
                Case 9
                    TrialToggler(StimulusSequence.Trial9)
            End Select
        End If
    End Sub

    ' Simplified TrialToggler: use persisted field `idx` and advance it exactly once.
    Private Sub TrialToggler(stimSeq As StimulusSequence)
        Select Case idx
            Case 0
                If StimBWatch.IsRunning Then StimBWatch.Stop()
                If Not StimAWatch.IsRunning Then StimAWatch.Start()
                RunColorWatch(stimSeq.Color1)
                aPressCt += 1
                StimGrid.Background = stimSeq.Color1
            Case 1
                If StimAWatch.IsRunning Then StimAWatch.Stop()
                If Not StimBWatch.IsRunning Then StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.White
            Case 2
                If StimBWatch.IsRunning Then StimBWatch.Stop()
                If Not StimAWatch.IsRunning Then StimAWatch.Start()
                RunColorWatch(stimSeq.Color2)
                aPressCt += 1
                StimGrid.Background = stimSeq.Color2
            Case 3
                If StimAWatch.IsRunning Then StimAWatch.Stop()
                If Not StimBWatch.IsRunning Then StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.White
            Case 4
                If StimBWatch.IsRunning Then StimBWatch.Stop()
                If Not StimAWatch.IsRunning Then StimAWatch.Start()
                RunColorWatch(stimSeq.Color3)
                aPressCt += 1
                StimGrid.Background = stimSeq.Color3
            Case 5
                If StimAWatch.IsRunning Then StimAWatch.Stop()
                If Not StimBWatch.IsRunning Then StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.White
            Case 6
                If StimBWatch.IsRunning Then StimBWatch.Stop()
                If Not StimAWatch.IsRunning Then StimAWatch.Start()
                RunColorWatch(stimSeq.Color4)
                aPressCt += 1
                StimGrid.Background = stimSeq.Color4
            Case 7
                If StimAWatch.IsRunning Then StimAWatch.Stop()
                If Not StimBWatch.IsRunning Then StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.White
            Case 8
                If StimBWatch.IsRunning Then StimBWatch.Stop()
                If Not StimAWatch.IsRunning Then StimAWatch.Start()
                RunColorWatch(stimSeq.Color5)
                aPressCt += 1
                StimGrid.Background = stimSeq.Color5
            Case 9
                StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.White
        End Select


    End Sub

    Private Sub RunColorWatch(brush As SolidColorBrush)
        colorWatchOn = True
        Dim c = brush.Color

        If c = Brushes.Blue.Color Then
            BlueWatch.Start()
        ElseIf c = Brushes.Green.Color Then
            GreenWatch.Start()
        ElseIf c = Brushes.Yellow.Color Then
            YellowWatch.Start()
        ElseIf c = Brushes.Orange.Color Then
            OrangeWatch.Start()
        ElseIf c = Brushes.Red.Color Then
            RedWatch.Start()
        End If

    End Sub

    Private Sub EndColorWatch()
        BlueWatch.Stop()
        GreenWatch.Stop()
        YellowWatch.Stop()
        OrangeWatch.Stop()
        RedWatch.Stop()
        colorWatchOn = False
    End Sub

    Private Sub ShowOverlay(img As Image, file As String)
        img.Source = New BitmapImage(New Uri(file, UriKind.Relative))
        img.Visibility = Visibility.Visible
    End Sub

    Private Sub ResetGridVisuals()
        StimGrid.Background = Brushes.Black
        StimGridOverlay.Visibility = Visibility.Collapsed
    End Sub

    ' -------------------------------------------------------
    ' READY overlay helpers
    ' -------------------------------------------------------
    Private Sub ShowReadyIndicator()
        If Not trialReady Then Return
        StimGridReadyOverlay.Source = New BitmapImage(New Uri("Assets/playbtn.png", UriKind.Relative))
        StimGridReadyOverlay.Visibility = Visibility.Visible
        MasterWatch.Start()
    End Sub

    Private Sub HideReadyIndicator()
        StimGridReadyOverlay.Visibility = Visibility.Collapsed
    End Sub


    ' -------------------------------------------------------
    ' Lockout sequence
    ' -------------------------------------------------------
    Public Async Function LockOut() As Task
        trialReady = False
        ResetGridVisuals()

        ' Deactivate all progress bars during lockout
        progressControllerTotalPress.Deactivate()
        progressControllerActiveStim.Deactivate()
        'progressControllerStimA.Deactivate()

        RecordData()
        RecordTrial()
        Try
            bc.Close()
        Catch
        End Try

        Latency.Stop()

        If Not animationPlayed Then
            animationPlayed = True
        End If
        Await PlayLockoutLEDSequence()
        Await ActivateOut(fc, 50)
        Await Task.Delay(3000)

        Try
            bc.Open()
        Catch
        End Try

        flc.State = False
        Latency.Reset()
        Latency.Stop()
        ResetTrial()
        trialReady = True
    End Function

    Private Async Function PlayLockoutLEDSequence() As Task
        For i = 1 To 5
            flc.State = True
            Await Task.Delay(150)
            flc.State = False
            Await Task.Delay(150)
        Next
    End Function

    Private Async Function ActivateOut(chan As DigitalOutput, ms As Integer) As Task
        chan.State = True
        Await Task.Delay(ms)
        chan.State = False
    End Function

    Private Async Function RumblePak(ct As CancellationToken) As Task
        Try
            While Not ct.IsCancellationRequested
                ' Set hardware state via Dispatcher (short non-blocking marshal)
                Dispatcher.BeginInvoke(Sub() cc.State = True)
                Await Task.Delay(100, ct)
                Dispatcher.BeginInvoke(Sub() cc.State = False)
                Await Task.Delay(999, ct)
            End While
        Catch ex As TaskCanceledException
            ' expected cancellation — ensure hardware off
            Dispatcher.BeginInvoke(Sub() cc.State = False)
        Catch ex As Exception
            ' log and surface critical error
            'Dispatcher.BeginInvoke(Sub() Console.WriteLine($"Rumble error: {ex.Message}"))
        End Try
    End Function

    ' -------------------------------------------------------
    ' Reset Trial
    ' -------------------------------------------------------
    Private Sub ResetTrial()
        MasterWatch.Stop()
        ActiveStimWatch.Stop()
        EndColorWatch()
        StimAWatch.Stop()
        StimBWatch.Stop()
        ResetGridVisuals()


        If Not isLockout Then
            RecordData()
        End If

        trialCount += 1
        ' Advance persisted index exactly once per test cycle.
        idx = (idx + 1) Mod 10
        'TrialSelect.Text = trialCount.ToString()
        btnCount = 0
        aPressCt = 0
        bPressCt = 0

        BlueWatch.Reset()
        GreenWatch.Reset()
        YellowWatch.Reset()
        OrangeWatch.Reset()
        RedWatch.Reset()
        MasterWatch.Reset()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()

        ' End of trial — clear total progress so next trial starts fresh.
        progressControllerTotalPress?.Deactivate()
    End Sub

    Private Sub RecordData()
        Dim subjectText = If(SubjectName.SelectedItem IsNot Nothing,
                             CType(SubjectName.SelectedItem, ComboBoxItem).Content.ToString(),
                             "Unknown")

        If TrainingMode Then
            TextBox1.Text &= $"Start Time: {sessionStartTimeStamp.ToFileTimeUtc}, " &
                $"{subjectText}, " &
                $"Training Mode?: {TrainingMode}, " &
                $"Trial Timer: {MasterWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Trial: {trialCount}, " &
                $"Button Presses: {btnCount}, " &
                $"Press duration: {ActiveStimWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Total StimA: {StimAWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Total StimB: {StimBWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Total Button Down time: {(StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds) / 1000} secs, " &
                $"Total Button Up time (Latency): {Latency.ElapsedMilliseconds / 1000} secs, " &
            Environment.NewLine
            TextBox1.ScrollToEnd()
        Else
            TextBox1.Text &= $"Start Time: {sessionStartTimeStamp}, " &
                $"{subjectText}, " &
                $"Training Mode?: {TrainingMode}, " &
                $"Trial Timer: {MasterWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Trial: {trialCount}, " &
                $"Button Presses: {btnCount}, " &
                $"Press duration: {ActiveStimWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Total StimA: {StimAWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Total StimB: {StimBWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Blue Time: {BlueWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Green Time: {GreenWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Yellow Time: {YellowWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Orange Time: {OrangeWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Red Time: {RedWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Total Button Down time: {(StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds) / 1000} secs, " &
                $"Total Button Up time (Latency): {Latency.ElapsedMilliseconds / 1000} secs, " &
            Environment.NewLine
            TextBox1.ScrollToEnd()
        End If
    End Sub

    Private Sub RecordTrial()
        Dim subjectText = If(SubjectName.SelectedItem IsNot Nothing,
                             CType(SubjectName.SelectedItem, ComboBoxItem).Content.ToString(),
                             "Unknown")

        If TrainingMode Then
            TrialDataBox.Text &= $"Start Time: {sessionStartTimeStamp.ToFileTimeUtc}, " &
                $"{subjectText}, " &
                $"Training Mode?: {TrainingMode}, " &
                $"Trial: {trialCount}, " &
                $"Button Presses: {btnCount}, " &
                $"Trial Duration: {MasterWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Target Hold Time: {TargetTime}, " &
                $"Stim A Presses: {aPressCt},  " &
                $"Total StimA: {StimAWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Stim B Presses: {bPressCt}, " &
                $"Total StimB: {StimBWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Time to first press (Master - [Up + Down]): {(MasterWatch.ElapsedMilliseconds - (StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds)) / 1000} secs" &
                Environment.NewLine
            TrialDataBox.ScrollToEnd()
        Else
            TrialDataBox.Text &= $"Start Time: {sessionStartTimeStamp.ToFileTimeUtc}, " &
                $"{subjectText}, " &
                $"Training Mode?: {TrainingMode}, " &
                $"Trial: {trialCount}, " &
                $"Button Presses: {btnCount}, " &
                $"Trial Duration: {MasterWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Target Hold Time: {TargetTime}, " &
                $"Stim A Presses: {aPressCt},  " &
                $"Total StimA: {StimAWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Stim B Presses: {bPressCt}, " &
                $"Total StimB: {StimBWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Blue Time: {BlueWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Green Time: {GreenWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Yellow Time: {YellowWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Orange Time: {OrangeWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Red Time: {RedWatch.ElapsedMilliseconds / 1000} secs, " &
                $"Time to first press (Master - [Up + Down]): {(MasterWatch.ElapsedMilliseconds - (StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds)) / 1000} secs" &
                Environment.NewLine
            TrialDataBox.ScrollToEnd()
        End If
    End Sub

    ' -------------------------------------------------------
    ' buttons
    ' -------------------------------------------------------
    Private Sub SetMode() Handles TrainingToggle.Click
        If TrainingToggle.IsChecked Then
            TrainingMode = True
            TrialSelect.Visibility = Visibility.Collapsed
        ElseIf Not TrainingToggle.IsChecked Then
            TrainingMode = False
            TrialSelect.Visibility = Visibility.Visible
        End If
    End Sub

    Private Sub StartButton_Click(sender As Object, e As EventArgs) Handles StBtn.Click
        sessionStartTimeStamp = DateTime.Now()

        If Not isRunning Then
            ' START SESSION
            'Log($"{DateTime.UtcNow:o} - START button clicked: starting session")
            isRunning = True
            trialReady = True

            StBtn.Content = "Stop"
            StBtn.Background = Brushes.Red

            ' Reset timers and counters for new session
            Latency.Reset()
            ActiveStimWatch.Reset()
            StimAWatch.Reset()
            StimBWatch.Reset()
            btnCount = 0
            idx = 0

            ' Determine TargetTime at session start:
            If TargetTimeInput Is Nothing OrElse TargetTimeInput.SelectedItem Is Nothing Then
                ' Only force default here if control is missing / no selection at start
                TargetTime = 3000
                Log($"{DateTime.UtcNow:o} - TargetTimeInput missing/unselected — defaulting to 3000 ms")
            Else
                Try
                    Dim seconds = CInt(CType(TargetTimeInput.SelectedItem, ComboBoxItem).Content)
                    TargetTime = seconds * 1000
                Catch ex As Exception
                    ' fallback if parsing fails
                    TargetTime = 3000
                    Log($"{DateTime.UtcNow:o} - Failed to parse TargetTimeInput at Start, defaulting to 3000 ms: {ex.Message}")
                End Try
            End If

            ' Ensure the total-progress controller uses the runtime TargetTime value
            If progressControllerTotalPress IsNot Nothing Then
                progressControllerTotalPress.UpdateMaxThreshold(TargetTime)
            End If

            ' Ensure total progress is tracking for this trial/session.
            progressControllerTotalPress?.Activate()

            ShowReadyIndicator()
            Log($"{DateTime.UtcNow:o} - Session started: ready for button presses (TargetTime={TargetTime} ms)")

        Else
            ' STOP SESSION
            Log($"{DateTime.UtcNow:o} - STOP button clicked: stopping session")
            isRunning = False
            trialReady = False

            StBtn.Content = "Start"
            StBtn.Background = Brushes.PaleGreen

            HideReadyIndicator()
            ' Stop total progress when session stopped.
            progressControllerTotalPress?.Deactivate()
            Log($"{DateTime.UtcNow:o} - Session stopped")
        End If
    End Sub

    Private Sub XButton_Click(sender As Object, e As RoutedEventArgs) Handles ExitButton.Click
        If manualSave AndAlso manualTrialSave Then
            System.Windows.Application.Current.Dispatcher.InvokeShutdown()
        Else
            SaveDataAuto()
            SaveTrialDataAuto()
            System.Windows.Application.Current.Dispatcher.InvokeShutdown()
        End If
    End Sub

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs) Handles BtnSave.Click
        Dim subjectText = If(SubjectName.SelectedItem IsNot Nothing,
                             CType(SubjectName.SelectedItem, ComboBoxItem).Content.ToString(),
                             "Unknown")
        Dim stimAText = StimAName.Text
        Dim stimBText = StimBName.Text

        Dim save As New Microsoft.Win32.SaveFileDialog With {
            .FileName = $"{subjectText}_StimA-{stimAText}_StimB-{stimBText}_{Date.Now.ToFileTimeUtc}.csv",
            .DefaultExt = ".csv"
        }
        If save.ShowDialog() Then
            IO.File.WriteAllText(save.FileName, TextBox1.Text)
        End If
        manualSave = True
    End Sub

    Private Sub SaveDataAuto()
        Try
            Dim subjectText = If(SubjectName.SelectedItem IsNot Nothing,
                                 CType(SubjectName.SelectedItem, ComboBoxItem).Content.ToString(),
                                 "Unknown")
            Dim stimAText = StimAName.Text
            Dim stimBText = StimBName.Text

            Dim folder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhidgetData/autosave")
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            Dim file As String = Path.Combine(folder, $"{subjectText}_StimA-{stimAText}_StimB-{stimBText}_{Date.Now.ToFileTimeUtc}.csv")
            IO.File.WriteAllText(file, TextBox1.Text)
            Console.WriteLine($"Autosaved data to {file}")
        Catch ex As Exception
            Console.WriteLine($"Error autosaving data: {ex.Message}")
        End Try
    End Sub

    Private Sub SaveTrialDataAuto()
        Try
            Dim subjectText = If(SubjectName.SelectedItem IsNot Nothing,
                                 CType(SubjectName.SelectedItem, ComboBoxItem).Content.ToString(),
                                 "Unknown")

            Dim folder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhidgetData/autosave")
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            Dim file As String = Path.Combine(folder, $"{subjectText}_Trials_{Date.Now.ToFileTimeUtc}.csv")
            IO.File.WriteAllText(file, TrialDataBox.Text)
            Console.WriteLine($"Autosaved trial data to {file}")
        Catch ex As Exception
            Console.WriteLine($"Error autosaving trial data: {ex.Message}")
        End Try
    End Sub

    Private Sub HandleDisconnectSave(reason As String)
        ' Helper to call autosave and show overlay from non-UI threads
        Dispatcher.Invoke(Sub()
                              If hasSavedOnDisconnect Or manualSave AndAlso manualTrialSave Then Return
                              hasSavedOnDisconnect = True
                              Try
                                  SaveDataAuto()
                                  SaveTrialDataAuto()
                              Catch ex As Exception
                                  Console.WriteLine($"Error saving on disconnect helper: {ex.Message}")
                              End Try
                              Console.WriteLine($"Handled disconnect save: {reason}")
                          End Sub)
    End Sub

    ' -------------------------------------------------------
    ' Clean Shutdown
    ' -------------------------------------------------------
    Protected Overrides Sub OnClosed(e As EventArgs)
        ' Ensure we try to save on normal shutdown when manual slave flags not set to true
        If Not hasSavedOnDisconnect Or (manualSave AndAlso manualTrialSave) Then
            Try
                SaveDataAuto()
                SaveTrialDataAuto()
            Catch
            End Try
        End If

        bc?.Close()
        cc?.Close()
        fc?.Close()
        flc?.Close()
        ' Close log writer
        Try
            If logWriter IsNot Nothing Then
                Log($"Log closed: {DateTime.UtcNow:o}")
                logWriter.Flush()
                logWriter.Close()
                logWriter.Dispose()
                logWriter = Nothing
            End If
        Catch
        End Try
        MyBase.OnClosed(e)
    End Sub
End Class