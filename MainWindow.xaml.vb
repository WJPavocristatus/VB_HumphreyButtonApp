Imports Phidget22
Imports Phidget22.Events
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Threading
Imports System.Diagnostics
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Media

Public Class MainWindow

    ' -----------------------------
    ' Phidget Channels
    ' -----------------------------
    Private bc As New DigitalInput()   ' Button Channel
    Private cc As New DigitalOutput()  ' Clicker (rumble)
    Private fc As New DigitalOutput()  ' Feeder Channel
    Private flc As New DigitalOutput() ' Feeder LED
    'Private llc As New DigitalOutput() ' Lockout LED
    Private btnLED As New DigitalOutput()

    ' -----------------------------
    ' Timer & State Variables
    ' -----------------------------
    Friend WithEvents timer As New System.Timers.Timer(1) ' 1 ms tick

    Private rumbleCts As CancellationTokenSource
    Private TargetTime As Integer
    Private btnCount As Integer = 0
    Private trialCount As Integer = 0
    Private isLockout As Boolean = False
    Private animationPlayed As Boolean = False
    Private isRunning As Boolean = False ' Pre-start flag

    ' NEW: require button release before a new trial is allowed
    Private newTrialReady As Boolean = True

    ' -----------------------------
    ' Stopwatches
    ' -----------------------------
    Private Latency As New Stopwatch()
    Private ActiveStimWatch As New Stopwatch()
    Private StimAWatch As New Stopwatch()
    Private StimBWatch As New Stopwatch()
    Private MasterStopWatch As New Stopwatch()

    ' -------------------------------------------------------
    ' Constructor
    ' -------------------------------------------------------
    Public Sub New()
        InitializeComponent()

        ' Assign device serial
        bc.DeviceSerialNumber = 705800
        cc.DeviceSerialNumber = 705800
        fc.DeviceSerialNumber = 705800
        flc.DeviceSerialNumber = 705800
        'llc.DeviceSerialNumber = 705800

        bc.Channel = 0
        cc.Channel = 6
        fc.Channel = 7
        flc.Channel = 9
        'llc.Channel = 8

        ' Events
        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler fc.Attach, AddressOf OnAttachHandler
        AddHandler cc.Attach, AddressOf OnAttachHandler
        AddHandler bc.StateChange, AddressOf ButtonStim_StateChanged
        AddHandler bc.StateChange, AddressOf ButtonRumble_StateChanged

        ' Open hardware
        cc.Open()
        bc.Open()
        fc.Open()
        flc.Open()
        'llc.Open()

        ' Timer
        timer.Start()
    End Sub


    ' -------------------------------------------------------
    ' UI Initialization
    ' -------------------------------------------------------
    Private Sub InitMainWindow() Handles MyBase.Initialized
        MainWin.Width = SystemParameters.PrimaryScreenWidth * 2
        MainWin.Height = SystemParameters.PrimaryScreenHeight
        MainWin.Top = 0
        MainWin.Left = 0
        MainWin.WindowStyle = WindowStyle.None
        MainWin.ResizeMode = ResizeMode.NoResize
    End Sub


    ' -------------------------------------------------------
    ' Clock tick → UI dispatcher → control loop
    ' -------------------------------------------------------
    Private Sub Clock() Handles timer.Elapsed
        Application.Current.Dispatcher.BeginInvoke(AddressOf ControlLoop)
    End Sub


    ' -------------------------------------------------------
    ' Button → Stimulus Logic
    ' -------------------------------------------------------
    Private Sub ButtonStim_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Sub()

                              ' ----- NEW: require release before new trial can start -----
                              If Not newTrialReady Then
                                  ' If release detected, mark ready; otherwise ignore
                                  If e.State = False Then
                                      newTrialReady = True
                                  End If
                                  Return
                              End If
                              ' ----------------------------------------------------------

                              ' Pre-start or lockout: ignore presses
                              If isLockout OrElse Not isRunning Then
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  Return
                              End If

                              If e.State = True Then
                                  ' Button pressed → hide ready overlay
                                  If isRunning AndAlso Not isLockout Then
                                      HideReadyIndicator()
                                  End If

                                  ' Button pressed
                                  Latency.Stop()
                                  ActivateOut(cc, 35)

                                  If ActiveStimWatch.ElapsedMilliseconds < 10000 Then
                                      btnCount += 1
                                      ActiveStimWatch.Reset()
                                      SetGridColor(btnCount)
                                  Else
                                      ' Over 10sec -> reset visual and stim
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

                                  ' If this release comes after a lockout (or after reaching TargetTime),
                                  ' it will set newTrialReady at the top of this handler on next events.
                              End If

                          End Sub)
    End Sub


    ' -------------------------------------------------------
    ' Button → Rumble Loop
    ' -------------------------------------------------------
    Private Sub ButtonRumble_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Async Function()
                              ' ----- NEW: require release before new trial can start -----
                              If Not newTrialReady Then
                                  If e.State = False Then
                                      newTrialReady = True
                                  End If
                                  Return
                              End If
                              ' ----------------------------------------------------------

                              If e.State Then
                                  ' Pre-start or lockout: ignore
                                  If isLockout OrElse Not isRunning Then Return
                                  rumbleCts = New CancellationTokenSource()
                                  Await RumblePak(rumbleCts.Token)
                              Else
                                  rumbleCts?.Cancel()
                                  cc.State = False
                              End If
                          End Function)
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
    ' CONTROL LOOP
    ' -------------------------------------------------------
    Private Async Sub ControlLoop()
        ' Pre-start: behave like lockout but no outputs
        If Not isRunning Then
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            InitWatches()
            Return
        End If

        If isRunning Then
            MasterStopWatch.Start()
        End If

        TargetTime = CInt(TargetTimeInput.Value) * 1000

        ' Auto stop at 10 sec
        If ActiveStimWatch.ElapsedMilliseconds >= 10000 Then
            rumbleCts?.Cancel()
            cc.State = False
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            ResetGridVisuals()
            ActiveStimWatch.Reset()
        End If

        If Not isLockout Then
            If Not bc.State Then
                ActiveStimWatch.Stop()
                StimAWatch.Stop()
                StimBWatch.Stop()
                InitWatches()
                Return
            End If

            Dim totalPress As Long = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds

            If totalPress >= TargetTime Then
                ' Play chime
                PlaySound(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\beepBeep.wav"))

                ' Enter lockout
                isLockout = True
                cc.State = False
                rumbleCts?.Cancel()
                RecordData()
                MasterStopWatch.Stop()
                ActiveStimWatch.Stop()
                StimAWatch.Stop()
                StimBWatch.Stop()
                animationPlayed = True
                ' NEW: require release after lockout before allowing a new trial
                newTrialReady = False

                Await LockOut()
                ResetTrial()
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
        LatencyVal.Content = $"{Latency.ElapsedMilliseconds} msec"
    End Sub


    ' -------------------------------------------------------
    ' Alternating colors + overlays
    ' -------------------------------------------------------
    Private Sub SetGridColor(count As Integer)
        If isLockout OrElse Not isRunning Then Return

        Dim even As Boolean = (count Mod 2 = 0)
        ActiveStimWatch.Start()
        If even Then
            StimAWatch.Start()
            StimGrid.Background = Brushes.Gray
            StimSpy.Background = Brushes.Gray
            ShowOverlay(StimGridOverlay, "Assets/invert_hd-wallpaper-7939241_1280.png")
        Else
            StimBWatch.Start()
            StimGrid.Background = Brushes.LightGray
            StimSpy.Background = Brushes.LightGray
            ShowOverlay(StimGridOverlay, "Assets/waves-9954690_1280.png")
        End If
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
        StimGridReadyOverlay.Source = New BitmapImage(New Uri("Assets/playbtn.png", UriKind.Relative))
        StimGridReadyOverlay.Visibility = Visibility.Visible
    End Sub

    Private Sub HideReadyIndicator()
        StimGridReadyOverlay.Visibility = Visibility.Collapsed
    End Sub


    ' -------------------------------------------------------
    ' Lockout sequence
    ' -------------------------------------------------------
    Public Async Function LockOut() As Task
        RecordData()
        ResetGridVisuals()

        Try
            bc.Close()
        Catch
        End Try

        Latency.Stop()
        MasterStopWatch.Reset()

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
        'llc.State = False
        Latency.Reset()
        Latency.Stop()
    End Function

    Private Async Function PlayLockoutLEDSequence() As Task
        For i = 1 To 5
            flc.State = True
            'llc.State = True
            Await Task.Delay(150)
            flc.State = False
            'llc.State = False
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
                cc.State = True
                Await Task.Delay(100, ct)
                cc.State = False
                Await Task.Delay(999, ct)
            End While
        Catch ex As TaskCanceledException
        End Try
    End Function

    Private Sub ResetTrial()
        ActiveStimWatch.Stop()
        StimAWatch.Stop()
        StimBWatch.Stop()
        ResetGridVisuals()


        RecordData()


        trialCount += 1
        btnCount = 0
        Latency.Reset()
        Latency.Stop()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
    End Sub

    Private Sub RecordData()
        TextBox1.Text &= $"{SubjectName.Text}, " &
            $"Trial: {trialCount}, " &
            $"Button Presses: {btnCount}, " &
            $"Total Button Down time: {(StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds) / 1000} secs, " &
            $"Press duration: {ActiveStimWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Total StimA: {StimAWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Total StimB: {StimBWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Total Button Up time: {Latency.ElapsedMilliseconds / 1000} secs, " &
            $"MasterStopWatch Time: {MasterStopWatch.ElapsedMilliseconds / 1000} secs" &
        Environment.NewLine
        TextBox1.ScrollToEnd()
    End Sub

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs) Handles BtnSave.Click
        Dim save As New Microsoft.Win32.SaveFileDialog With {
            .FileName = $"{SubjectName.Text}_StimA-{StimAName.Text}_StimB-{StimBName.Text}_{Date.Now.ToFileTimeUtc}.csv",
            .DefaultExt = ".csv"
        }
        If save.ShowDialog() Then
            IO.File.WriteAllText(save.FileName, TextBox1.Text)
        End If
    End Sub

    ' -------------------------------------------------------
    ' Start button
    ' -------------------------------------------------------
    Private Sub StartButton_Click(sender As Object, e As RoutedEventArgs) Handles StBtn.Click
        If Not isRunning Then
            isRunning = True
            ShowReadyIndicator()
            StBtn.Content = "Stop"
            StBtn.Background = Brushes.Violet
        Else
            isRunning = False
            HideReadyIndicator()
            StBtn.Content = "Start"
            StBtn.Background = Brushes.Red
        End If

        Latency.Reset()
        Latency.Stop()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
        btnCount = 0

        ' NEW: when starting, allow immediate trial (no release required)
        newTrialReady = True
    End Sub

    ' -------------------------------------------------------
    ' Autosave
    ' -------------------------------------------------------
    Private Sub AutoSaveOnExit()
        Try
            Dim folder As String = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PhidgetData"
        )

            If Not IO.Directory.Exists(folder) Then
                IO.Directory.CreateDirectory(folder)
            End If

            Dim file As String = System.IO.Path.Combine(
            folder,
            $"{SubjectName.Text}_StimA-{StimAName.Text}_StimB-{StimBName.Text}_{Date.Now.ToFileTimeUtc}.csv"
        )

            IO.File.WriteAllText(file, TextBox1.Text)

        Catch ex As Exception
            ' Silent fail – do not block app closure
        End Try
    End Sub

    ' -------------------------------------------------------
    ' Clean Shutdown
    ' -------------------------------------------------------
    Protected Overrides Sub OnClosed(e As EventArgs)
        AutoSaveOnExit()
        bc?.Close()
        cc?.Close()
        fc?.Close()
        flc?.Close()
        'llc?.Close()
        MyBase.OnClosed(e)
    End Sub

End Class
