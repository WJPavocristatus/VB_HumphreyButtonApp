Imports Phidget22
Imports Phidget22.Events
Imports System.Threading
Imports System.Windows.Threading
Imports System.Media
Imports System.IO
'Imports System.Windows.Forms
Imports VB_HumphreyButtonApp.StimulusSequence


''' <summary>
''' WORKING MVP VERSION OF APP!!!!
''' </summary>
Public Class MainWindow

    ' -----------------------------
    ' Phidget Channels
    ' -----------------------------
    Private bc As New DigitalInput()   ' Button Channel
    Private cc As New DigitalOutput()  ' Clicker (rumble)
    Private fc As New DigitalOutput()  ' Feeder Channel
    Private flc As New DigitalOutput() ' Feeder LED

    ' -----------------------------
    ' Timer & State Variables
    ' -----------------------------
    Friend WithEvents timer As New System.Timers.Timer(1) ' 1 ms tick
    Private uiTimer As New System.Windows.Threading.DispatcherTimer With {
        .Interval = TimeSpan.FromMilliseconds(50)
    }
    Private devMode As Boolean = False
    Private rumbleCts As CancellationTokenSource
    Private rumbleTask As Task = Nothing
    Private TargetTime As Integer
    Private HoldLimit As Integer = 5000
    Private btnCount As Integer = 0
    Private trialCount As Integer = 0
    Private idx As Integer = 0
    Private isLockout As Boolean = False
    Private animationPlayed As Boolean = False
    Private isRunning As Boolean = False ' Pre-start flag
    Private aPressCt As Integer = 0
    Private bPressCt As Integer = 0
    Private trialReady As Boolean = False
    Private TrainingMode As Boolean = False
    Private sessionStartTimeStamp As DateTime
    Private colorWatchOn As Boolean = False
    ' One-shot guard to prevent multiple saves on multi-channel disconnect
    Private hasSavedOnDisconnect As Boolean = False

    Private manualSave As Boolean = False
    Private manualTrialSave As Boolean = False
    ' -----------------------------
    ' Stopwatches
    ' -----------------------------
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
        cc.Channel = 13 'change to 14 for real device
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
        uiTimer.Start()

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
        timer.Start()
    End Sub

    ' -------------------------------------------------------
    ' Window Placement
    ' -------------------------------------------------------
    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Dim screens = System.Windows.Forms.Screen.AllScreens

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

                ResearcherView.Width = New GridLength(screenWidth)
                ResearcherView.BringIntoView()
                SubjectView.Width = New GridLength(0)
            Else
                devMode = False
                Return
            End If
        Else
            Dim researcherScreen = screens(0)
            Dim subjectScreen = screens(1)

            ' Total window spans both monitors
            Dim totalWidth = researcherScreen.Bounds.Width + subjectScreen.Bounds.Width
            Dim maxHeight = Math.Max(researcherScreen.Bounds.Height, subjectScreen.Bounds.Height)

            Me.WindowStyle = WindowStyle.None
            Me.ResizeMode = ResizeMode.NoResize
            Me.Left = researcherScreen.Bounds.Left
            Me.Top = researcherScreen.Bounds.Top
            Me.Width = totalWidth
            Me.Height = maxHeight

            ' Resize columns to fit each monitor exactly
            ResearcherView.Width = New GridLength(researcherScreen.Bounds.Width)
            SubjectView.Width = New GridLength(subjectScreen.Bounds.Width)
        End If
    End Sub



    ' -------------------------------------------------------
    ' UI Initialization
    ' -------------------------------------------------------
    'Private Sub InitMainWindow() Handles MyBase.Initialized
    '    MainWin.Width = SystemParameters.PrimaryScreenWidth * 2
    '    MainWin.Height = SystemParameters.PrimaryScreenHeight
    '    MainWin.Top = 0
    '    MainWin.Left = 0
    '    MainWin.WindowStyle = WindowStyle.None
    '    MainWin.ResizeMode = ResizeMode.NoResize
    'End Sub


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
            ' do small periodic checks (e.g. ActiveStimWatch HoldLimit enforcement)
        End If
    End Sub
    ' -------------------------------------------------------
    ' Button → Stimulus Logic
    ' -------------------------------------------------------
    Private Sub ButtonStim_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Sub()

                              If Not trialReady Then Return

                              ' Pre-start or lockout: ignore presses
                              If isLockout OrElse Not isRunning Then
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  Latency.Stop()
                                  Return
                              ElseIf e.State = True Then
                                  ' Button pressed → hide ready overlay
                                  If isRunning AndAlso Not isLockout Then
                                      HideReadyIndicator()
                                  End If

                                  ' Button pressed
                                  Latency.Stop()
                                  ActivateOut(cc, 35)

                                  If ActiveStimWatch.ElapsedMilliseconds < 5000 Then
                                      btnCount += 1
                                      ActiveStimWatch.Reset()
                                      SetGridColor(btnCount)
                                  Else
                                      ' Over 5sec -> reset visual and stim
                                      ActiveStimWatch.Reset()
                                      StimAWatch.Reset()
                                      StimBWatch.Reset()
                                      cc.State = False
                                      ResetGridVisuals()
                                  End If

                              Else
                                  ' Button released
                                  If Not isLockout Then
                                      RecordData()
                                  End If
                                  cc.State = False
                                  Latency.Start()
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  ResetGridVisuals()
                              End If
                          End Sub)
    End Sub


    ' -------------------------------------------------------
    ' Button → Rumble Loop
    ' -------------------------------------------------------
    Private Sub ButtonRumble_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        ' Handle rumble lifecycle safely and observe the task.
        If e.State Then
            ' Cancel and dispose any existing CTS and wait for previous task to complete (non-blocking).
            Try
                If rumbleCts IsNot Nothing Then
                    rumbleCts.Cancel()
                    rumbleCts.Dispose()
                    rumbleCts = Nothing
                End If
            Catch ex As Exception
                Console.WriteLine($"Error cancelling previous rumble CTS: {ex.Message}")
            End Try

            rumbleCts = New CancellationTokenSource()
            Dim ct = rumbleCts.Token

            ' Start rumble task and keep reference so we can observe exceptions and await cancellation if needed.
            rumbleTask = Task.Run(Function() RumblePak(ct))
            ' Observe exceptions in continuation to avoid unobserved exceptions.
            rumbleTask.ContinueWith(Sub(t)
                                        If t.Exception IsNot Nothing Then
                                            Dispatcher.BeginInvoke(Sub() Console.WriteLine($"Rumble task error: {t.Exception.Flatten().Message}"))
                                        End If
                                    End Sub, TaskContinuationOptions.OnlyOnFaulted)
        Else
            ' Request cancellation and attach continuation to ensure hardware is turned off.
            Try
                If rumbleCts IsNot Nothing Then
                    rumbleCts.Cancel()
                End If
            Catch ex As Exception
                Console.WriteLine($"Error cancelling rumble CTS: {ex.Message}")
            End Try

            ' After requesting cancellation, observe/cleanup the task asynchronously.
            Dim tRef = rumbleTask
            rumbleTask = Nothing

            If tRef IsNot Nothing Then
                tRef.ContinueWith(Sub(t)
                                      ' Dispose CTS after task completes to avoid race with token usage.
                                      Try
                                          rumbleCts?.Dispose()
                                      Catch
                                      End Try
                                      rumbleCts = Nothing
                                      ' Ensure hardware off.
                                      Dispatcher.BeginInvoke(Sub() cc.State = False)
                                  End Sub)
            Else
                Try
                    rumbleCts?.Dispose()
                Catch
                End Try
                rumbleCts = Nothing
                Dispatcher.BeginInvoke(Sub() cc.State = False)
            End If
        End If
    End Sub

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
            Dispatcher.BeginInvoke(Sub() Console.WriteLine($"Rumble error: {ex.Message}"))
        End Try
    End Function

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
                                  Console.WriteLine($"Error saving on phidget error: {ex.Message}")
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

        If TargetTimeInput.Text = "" Then
            TargetTimeInput.Text = "3"
        End If
        TargetTime = CInt(TargetTimeInput.Text) * 1000

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
            If Not bc.State Then
                ActiveStimWatch.Stop()
                StimAWatch.Stop()
                StimBWatch.Stop()
                EndColorWatch()
                Return
            End If

            Dim totalPress As Long = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds

            If totalPress >= TargetTime Then
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
                animationPlayed = True
                Await LockOut()

                isLockout = False
                animationPlayed = False
                ' Show ready overlay again after LockOut
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
        If isLockout OrElse Not isRunning Then Return


        ActiveStimWatch.Start()
        If TrainingMode = True Then
            If count Mod 2 = 0 Then
                StimAWatch.Start()
                aPressCt += 1
                StimGrid.Background = Brushes.Gray
                StimSpy.Background = Brushes.Gray
                ShowOverlay(StimGridOverlay, "Assets/invert_hd-wallpaper-7939241_1280.png")
            Else
                StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.LightGray
                StimSpy.Background = Brushes.LightGray
                ShowOverlay(StimGridOverlay, "Assets/waves-9954690_1280.png")
            End If
        Else

            Select Case trialCount
                Case 0
                    TrialToggler(idx, StimulusSequence.Trial0)
                Case 1
                    TrialToggler(idx, StimulusSequence.Trial1)
                Case 2
                    TrialToggler(idx, StimulusSequence.Trial2)
                Case 3
                    TrialToggler(idx, StimulusSequence.Trial3)
                Case 4
                    TrialToggler(idx, StimulusSequence.Trial4)
                Case 5
                    TrialToggler(idx, StimulusSequence.Trial5)
                Case 6
                    TrialToggler(idx, StimulusSequence.Trial6)
                Case 7
                    TrialToggler(idx, StimulusSequence.Trial7)
                Case 8
                    TrialToggler(idx, StimulusSequence.Trial8)
                Case 9
                    TrialToggler(idx, StimulusSequence.Trial9)
            End Select
        End If
    End Sub

    Private Sub TrialToggler(xidx As Integer, stimSeq As StimulusSequence)
        Select Case xidx
            Case 0
                StimAWatch.Start()
                RunColorWatch(stimSeq.Color1)
                aPressCt += 1
                StimGrid.Background = stimSeq.Color1
                StimSpy.Background = stimSeq.Color1
                xidx += 1
            Case 1
                StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.White
                StimSpy.Background = Brushes.White
                xidx += 1
            Case 2
                StimAWatch.Start()
                RunColorWatch(stimSeq.Color2)
                aPressCt += 1
                StimGrid.Background = stimSeq.Color2
                StimSpy.Background = stimSeq.Color2
                xidx += 1
            Case 3
                StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.White
                StimSpy.Background = Brushes.White
                xidx += 1
            Case 4
                StimAWatch.Start()
                RunColorWatch(stimSeq.Color3)
                aPressCt += 1
                StimGrid.Background = stimSeq.Color3
                StimSpy.Background = stimSeq.Color3
                xidx += 1
            Case 5
                StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.White
                StimSpy.Background = Brushes.White
                xidx += 1
            Case 6
                StimAWatch.Start()
                RunColorWatch(stimSeq.Color4)
                aPressCt += 1
                StimGrid.Background = stimSeq.Color4
                StimSpy.Background = stimSeq.Color4
                xidx += 1
            Case 7
                StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.White
                StimSpy.Background = Brushes.White
                xidx += 1
            Case 8
                StimAWatch.Start()
                RunColorWatch(stimSeq.Color5)
                aPressCt += 1
                StimGrid.Background = stimSeq.Color5
                StimSpy.Background = stimSeq.Color5
                xidx += 1
            Case 9
                StimBWatch.Start()
                bPressCt += 1
                StimGrid.Background = Brushes.White
                StimSpy.Background = Brushes.White
                xidx = 0
        End Select
        idx = (xidx + 1) Mod 10
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
        StimSpy.Background = Brushes.Black
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
    End Sub

    Private Sub RecordData()
        If TrainingMode Then
            TextBox1.Text &= $"Start Time: {sessionStartTimeStamp.ToFileTimeUtc}, " &
                $"{SubjectName.Text}, " &
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
            TextBox1.Text &= $"Start Time: {sessionStartTimeStamp.ToFileTimeUtc}, " &
                $"{SubjectName.Text}, " &
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
        If TrainingMode Then
            TrialDataBox.Text &= $"Start Time: {sessionStartTimeStamp.ToFileTimeUtc}, " &
                $"{SubjectName.Text}, " &
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
                $"{SubjectName.Text}, " &
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


    Private Sub StartButton_Click(sender As Object, e As RoutedEventArgs) Handles StBtn.Click
        sessionStartTimeStamp = DateTime.Now()
        trialReady = True
        RecordData()
        RecordTrial()
        If Not isRunning Then
            isRunning = True
            StBtn.Content = "Stop"
            StBtn.Background = Brushes.Red
            ShowReadyIndicator()
        Else
            isRunning = False
            StBtn.Content = "Start"
            StBtn.Background = Brushes.Blue
            HideReadyIndicator()
        End If

        Latency.Reset()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
        btnCount = 0
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
        Dim save As New Microsoft.Win32.SaveFileDialog With {
            .FileName = $"{SubjectName.Text}_StimA-{StimAName.Text}_StimB-{StimBName.Text}_{Date.Now.ToFileTimeUtc}.csv",
            .DefaultExt = ".csv"
        }
        If save.ShowDialog() Then
            IO.File.WriteAllText(save.FileName, TextBox1.Text)
        End If
        manualSave = True
    End Sub

    Private Sub Save_Trial_Click(sender As Object, e As RoutedEventArgs) Handles TrialSave.Click
        Dim save As New Microsoft.Win32.SaveFileDialog With {
            .FileName = $"{SubjectName.Text}_Trials_{Date.Now.ToFileTimeUtc}.csv",
            .DefaultExt = ".csv"
        }
        If save.ShowDialog() Then
            IO.File.WriteAllText(save.FileName, TrialDataBox.Text)
        End If
        manualTrialSave = True
    End Sub

    ' -----------------------------
    ' Non-interactive autosave helpers
    ' -----------------------------
    Private Sub SaveDataAuto()
        Try
            Dim folder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhidgetData/autosave")
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            Dim file As String = Path.Combine(folder, $"{SubjectName.Text}_StimA-{StimAName.Text}_StimB-{StimBName.Text}_{Date.Now.ToFileTimeUtc}.csv")
            IO.File.WriteAllText(file, TextBox1.Text)
            Console.WriteLine($"Autosaved data to {file}")
        Catch ex As Exception
            Console.WriteLine($"Error autosaving data: {ex.Message}")
        End Try
    End Sub

    Private Sub SaveTrialDataAuto()
        Try
            Dim folder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhidgetData/autosave")
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            Dim file As String = Path.Combine(folder, $"{SubjectName.Text}_Trials_{Date.Now.ToFileTimeUtc}.csv")
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
        MyBase.OnClosed(e)
    End Sub
End Class
